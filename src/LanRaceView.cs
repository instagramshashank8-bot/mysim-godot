using Godot;
using System;
using MySimulator.Rebuild;
namespace MySim {
// LAN phase-2 race-view adapters (the granted Main.cs scope): the GODOT half of the
// race-session bridge. LanCarView owns ONE persistent car visual - created on first sight,
// posed in place from every snapshot, never recreated per packet (the central-risk rule);
// LanRaceView creates them under the world and carries the race-state readout;
// LanClientRunner ticks the session, drives the applier and follows the local car with a
// simple chase camera. The single-player path never constructs any of this: the runner
// exists only while a LAN session handed over from the lobby is live. MVP limits (stated
// in the LAN plan): pure snapshot rendering, no local physics, no prediction.
public partial class LanCarView : Node3D, ICarView {
    Node3D visual;
    public string Driver;
    public float Damage, Fuel, Speed, ServiceRemaining;
    public int CompletedLaps;
    public bool Finished, PitActive, Servicing;
    public void EnsureCreated(int carModel, string driver) {
        Driver = driver;
        if (visual != null || carModel < 0 || carModel >= CarCatalog.Count) return;
        visual = CarLoader.Load(CarCatalog.Folders[carModel]);
        AddChild(visual);
        CarLoader.ApplyExact(visual, carModel);
    }
    public void ApplyPose(float x, float z, float heading, bool hasBodyPose, float bodyHeight, float bodyQx, float bodyQy, float bodyQz, float bodyQw) {
        // Logic and world coordinates are 1:1 for X/Z (Contact.ToWorld is the same mapping
        // the single-player visual path uses); Y comes from the body pose when the host
        // simulates natively, else the ground plane.
        Position = new Vector3(x, hasBodyPose ? bodyHeight : 0f, z);
        if (hasBodyPose) {
            var q = new Quaternion(bodyQx, bodyQy, bodyQz, bodyQw);
            if (q.LengthSquared() > 0.8f && q.LengthSquared() < 1.2f) Transform = new Transform3D(new Basis(q), Position);
            else Rotation = new Vector3(0, heading, 0);
        } else Rotation = new Vector3(0, heading, 0);
    }
    public void ApplyState(float damage, float fuel, float speed, int completedLaps, bool finished, float finishTime, bool pitActive, bool servicing, float serviceRemaining) {
        Damage = damage; Fuel = fuel; Speed = speed; CompletedLaps = completedLaps; Finished = finished;
        PitActive = pitActive; Servicing = servicing; ServiceRemaining = serviceRemaining;
    }
}
public sealed class LanRaceView : IRaceView {
    readonly Node3D world;
    public float Clock, Countdown, Wetness;
    public bool Started;
    public int TrackVariant, TargetLaps;
    public LanRaceView(Node3D world) { this.world = world; }
    public ICarView CreateCarView(int id) {
        var view = new LanCarView { Name = "LanCar" + id };
        world?.AddChild(view);
        return view;
    }
    public void ApplyRaceState(float clock, float countdown, bool started, int trackVariant, int targetLaps, float wetness) {
        Clock = clock; Countdown = countdown; Started = started; TrackVariant = trackVariant; TargetLaps = targetLaps; Wetness = wetness;
    }
}
public partial class LanClientRunner : Node {
    public GameSession Session;
    public Node3D World;
    public Camera3D Cam;
    public Action OnExit;
    public bool EscWas;
    // Two-instance test automation (the owner-run script): QuitAfter>0 ends the client with
    // a PASS/FAIL summary line; SyncObserved records that the REMOTE (host) car was seen
    // moving above the motion threshold - the sync proof for the unattended run.
    public double QuitAfter;
    public bool SyncObserved;
    double quitTimer, syncTimer;
    SnapshotApplier applier;
    LanRaceView raceView;
    Label status;
    int lastAppliedSequence = -1;
    public override void _Ready() {
        raceView = new LanRaceView(World);
        applier = new SnapshotApplier(raceView);
        status = new Label { Position = new Vector2(48, 24), Text = "LAN" };
        status.AddThemeFontSizeOverride("font_size", 24);
        var layer = new CanvasLayer { Layer = 25 };
        layer.AddChild(status);
        AddChild(layer);
    }
    public override void _Process(double delta) {
        if (Session == null) return;
        try { Session.Tick((float)delta); }
        catch (Exception e) { status.Text = "LAN session error: " + e.Message; return; }
        // GameSession swaps in a new Race per STATE packet; the applier keeps the car views
        // persistent and updates them in place. Apply only on a new sequence number.
        if (Session.race != null && Session.lastSequence != lastAppliedSequence) {
            lastAppliedSequence = Session.lastSequence;
            applier.Apply(Session.race);
            if (Cam != null && applier.ViewFor(Session.localId) is LanCarView mine && mine.Position.LengthSquared() > 0.01f) {
                var behind = -mine.GlobalTransform.Basis.Z;
                Cam.GlobalPosition = mine.GlobalPosition + new Vector3(0, 5f, 0) + behind * 9f;
                Cam.LookAt(mine.GlobalPosition + Vector3.Up);
            }
            // The sync proof: car 0 is the HOST's car on the client; motion above the
            // threshold at the client means states are flowing and applied.
            if (!SyncObserved && applier.ViewFor(0) is LanCarView hostCar && hostCar.Speed > 3f) {
                SyncObserved = true;
                GD.Print("PASS LAN_CLIENT sync observed: host car speed=", hostCar.Speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), " at t=", raceView.Clock.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            }
        }
        syncTimer += delta;
        if (syncTimer >= 2) {
            syncTimer = 0;
            var hostCar = applier.ViewFor(0) as LanCarView;
            GD.Print("LAN_SYNC t=", raceView.Clock.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), " phase=", (raceView.Started ? "RACE" : "GRID"), " hostPos=", hostCar == null ? "n/a" : hostCar.Position.ToString("0.0"), " hostSpeed=", hostCar == null ? "n/a" : hostCar.Speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture), " syncObserved=", SyncObserved);
        }
        status.Text = (raceView.Started ? "RACE" : "GRID") + "  laps " + raceView.TargetLaps + "  t " + raceView.Clock.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + (raceView.Started ? "" : "  countdown " + raceView.Countdown.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
        bool escNow = Input.IsKeyPressed(Key.Escape);
        if (escNow && !EscWas) { Session.Dispose(); Session = null; OnExit?.Invoke(); return; }
        EscWas = escNow;
        if (QuitAfter > 0) {
            quitTimer += delta;
            if (quitTimer >= QuitAfter) {
                GD.Print(SyncObserved ? "PASS LAN_CLIENT exit: sync observed" : "FAIL LAN_CLIENT exit: remote motion never observed");
                Session.Dispose(); Session = null;
                CleanExit.Request(this, SyncObserved ? 0 : 1);
            }
        }
    }
    public override void _ExitTree() { Session?.Dispose(); Session = null; }
}
}
