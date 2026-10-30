using Godot;
using MySimulator.Rebuild;
namespace MySim {
// Player drive loop: recovered Race simulation steps the car, the model is placed on the drive-surface mesh by raycast,
// chase camera 7.2 m back / 2.65 m up with speed lookahead (revised Unity values), no artificial shake.
public partial class DriveController : Node3D {
 public int CarIndex; public Race Race; public Vehicle Player; public Node3D Car; public Camera3D Cam; public bool AutoDrive; public float AutoSeconds = 8; public Hud Hud; float? lastGroundY, prevY; float groundSmooth; bool groundInit; float minY = 1e9f, maxY = -1e9f, maxDy; float steerCmd; public Vector3 CarSize = new Vector3(2, 1.2f, 4.7f); public int TrackIndex; public Node3D[] WheelPivots; float spinAngle; public int Laps = 3; public bool AiTest; string lastSt = ""; float finT;
 int CamMode = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_CAM") ?? "0"); bool cWas; bool tabWas = true; ulong pausedAt; float t, camYaw; int shownMode = -1; bool camInit;
 public void CycleCamera() { CamMode=(CamMode+1)%4; }
 public override void _Ready() {
  Race = new Race(TrackIndex, Laps, Conditions.WetnessValue); Player = Race.Add("PLAYER", true, Compound.Medium); Player.carModel = CarIndex; Race.countdown = 0; Race.started = true; Player.Place(Race.track, float.Parse(System.Environment.GetEnvironmentVariable("MYSIM_STARTP") ?? "0.988", System.Globalization.CultureInfo.InvariantCulture), 0f); { int nr = System.Environment.GetEnvironmentVariable("MYSIM_RIVALS") != null ? int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_RIVALS")) : (AutoDrive || AiTest ? 0 : Settings.Get("rivals", 5)); { var order = new System.Collections.Generic.List<int>(); for (int ci = 0; ci < CarCatalog.Count; ci++) if (ci != CarIndex && CarCatalog.RaceClass(ci) == CarCatalog.RaceClass(CarIndex)) order.Add(ci); float myS = MySimulator.Rebuild.RecoveredVehicleProfiles.Speed[CarIndex], myA = MySimulator.Rebuild.RecoveredVehicleProfiles.Acceleration[CarIndex]; order.Sort((x, y) => (Mathf.Abs(MySimulator.Rebuild.RecoveredVehicleProfiles.Speed[x] - myS) / 60f + Mathf.Abs(MySimulator.Rebuild.RecoveredVehicleProfiles.Acceleration[x] - myA)).CompareTo(Mathf.Abs(MySimulator.Rebuild.RecoveredVehicleProfiles.Speed[y] - myS) / 60f + Mathf.Abs(MySimulator.Rebuild.RecoveredVehicleProfiles.Acceleration[y] - myA))); RivalModels = order.ToArray(); } for (int ri = 0; ri < nr; ri++) { var rv = Race.Add("AI " + (ri + 1), false, Compound.Medium); rv.carModel = RivalModels[ri % RivalModels.Length]; rivals.Add(new Rival { V = rv, LastY = float.NaN }); } } CallDeferred(nameof(BindHud));
 }
 void BindHud() { if (Hud != null) { Hud.Race = Race; Hud.Player = Player; } }
 void ResolveWalls(Vec start) { if (Contact.Walls(GetWorld3D().DirectSpaceState, Player, start, Car.Position.Y, Contact.BodySize(CarSize),new Vector3(VehicleDimensions.CenterX[CarIndex],VehicleDimensions.CenterY[CarIndex],VehicleDimensions.CenterZ[CarIndex]))) WallHits++; }
 public int WallHits; int lastTrace = -1; bool floatScanned, settingsApplied, condsApplied; PedalController pedals = new PedalController(); bool padStartSeen, padStartStuck; float maxDelta, fpsT; int[] RivalModels; float[] basePivotY; float[] wheelTravel = new float[4]; MeshInstance3D treeTarget; int pushFrames, frames;
 Vector3 ObstructCam(Vector3 from, Vector3 want) { using var q = PhysicsRayQueryParameters3D.Create(from, want); q.CollisionMask = 2; q.HitBackFaces = true; var hit = GetWorld3D().DirectSpaceState.IntersectRay(q); if (hit.Count == 0) return want; var p = (Vector3)hit["position"]; var n = (Vector3)hit["normal"]; var dir = (want - from).Normalized(); float dist = Mathf.Max(0.8f, from.DistanceTo(p) - 0.35f); return from + dir * dist; }
float lastLift; Node3D crew; Node3D[] crewFig; Vector3[] crewHome;
 void BuildCrew() {
  crew = new Node3D { Visible = false }; AddChild(crew); var inv = Car.GlobalTransform.Basis.Orthonormalized().Transposed(); crewFig = new Node3D[6]; crewHome = new Vector3[6];
  var suit = new StandardMaterial3D { AlbedoColor = new Color(0.78f, 0.12f, 0.1f), Roughness = 0.9f }; var skin = new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.62f, 0.5f), Roughness = 0.9f }; var helm = new StandardMaterial3D { AlbedoColor = new Color(0.95f, 0.95f, 0.95f), Roughness = 0.4f };
  for (int i = 0; i < 6; i++) {
   var fig = new Node3D(); var body = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.17f, Height = 1.0f }, MaterialOverride = suit, Position = new Vector3(0, 0.5f, 0) }; var head = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f }, MaterialOverride = helm, Position = new Vector3(0, 1.12f, 0) };
   fig.AddChild(body); fig.AddChild(head); crew.AddChild(fig); crewFig[i] = fig;
   Vector3 home;
   if (i < 4 && WheelPivots != null) { var wp = inv * (WheelPivots[i].GlobalPosition - Car.GlobalPosition); float side = wp.X < 0 ? -1f : 1f; home = new Vector3(wp.X + side * 0.55f, 0, wp.Z); }
   else if (i == 4) home = new Vector3(1.9f, 0, CarSize.Z * 0.5f + 0.7f); else home = new Vector3(0, 0, CarSize.Z * 0.5f + 0.7f);
   crewHome[i] = home; fig.Position = home;
  }
 }
 void UpdateCrew(float e) {
  if (WheelPivots == null || Car == null) return; if (crew == null) BuildCrew(); crew.Visible = e >= 0 && e < 7.3f; if (!crew.Visible) return; crew.GlobalTransform = new Transform3D(Car.GlobalTransform.Basis.Orthonormalized(), new Vector3(Car.GlobalPosition.X, Car.GlobalPosition.Y - lastLift, Car.GlobalPosition.Z));
  for (int i = 0; i < 6; i++) {
   float lean = 0, off = 1.0f + i * 0.6f, on = 3.4f + i * 0.6f;
   if (i < 4) { if (e > off - 0.5f && e < on + 0.7f) lean = 0.45f; float side = WheelPivots[i].Position.X < 0 ? -1f : 1f; crewFig[i].Rotation = new Vector3(0, 0, side * lean); crewFig[i].Position = crewHome[i] + new Vector3(-side * lean * 0.25f, 0, 0); }
   else { lean = (i == 4 ? (e < 0.8f || e > 6.4f ? 0.5f : 0f) : (e < 7.3f ? 0.0f : 0f)); crewFig[i].Rotation = new Vector3(lean, 0, 0); }
  }
 }
AudioStreamPlayer engineSnd, whineSnd; AudioStreamPlayer[] sfxPool; int sfxNext; float lastE = -1f; bool mWas;
 static AudioStreamWav LoopWav(string path) { var w = GD.Load<AudioStreamWav>(path); if (w == null) return null; w = (AudioStreamWav)w.Duplicate(); int bytes = w.Format == AudioStreamWav.FormatEnum.Format16Bits ? 2 : 1; if (w.Stereo) bytes *= 2; w.LoopMode = AudioStreamWav.LoopModeEnum.Forward; w.LoopBegin = 0; w.LoopEnd = w.Data.Length / bytes - 1; return w; }
 void EnsureAudio() {
  if (engineSnd != null) return;
  engineSnd = new AudioStreamPlayer { Stream = LoopWav("res://assets/Resources/RealRacing/EngineIdle.wav"), VolumeDb = -30f }; AddChild(engineSnd); if (engineSnd.Stream != null) engineSnd.Play();
  whineSnd = new AudioStreamPlayer { Stream = LoopWav("res://assets/Resources/RealRacing/Audio/transmission_whine_on_mid.wav"), VolumeDb = -40f }; AddChild(whineSnd); if (whineSnd.Stream != null) whineSnd.Play();
  sfxPool = new AudioStreamPlayer[4]; for (int i = 0; i < 4; i++) { sfxPool[i] = new AudioStreamPlayer { VolumeDb = -4f }; AddChild(sfxPool[i]); }
 }
 void PlaySfx(string file) { var st = GD.Load<AudioStream>("res://assets/Resources/PitAudio/" + file); if (st == null) return; var pl = sfxPool[sfxNext++ % sfxPool.Length]; pl.Stream = st; pl.Play(); }
 void UpdateAudio(float e) {
  EnsureAudio(); if (Race.paused) { engineSnd.VolumeDb = -80f; whineSnd.VolumeDb = -80f; return; }
  bool mNow = ControlBindings.Held(7); if (mNow && !mWas) { Settings.Set("mute", !Settings.Get("mute", true)); Settings.ApplyAudio(); } mWas = mNow;
  float rpm = Mathf.Clamp(Player.rpm, 800f, 10000f), thr = Player.servicing ? 0f : Mathf.Clamp(Player.input.throttle, 0f, 1f);
  if (engineSnd.Stream != null) { engineSnd.PitchScale = Mathf.Clamp(0.55f + rpm / 2600f, 0.6f, 4.2f); engineSnd.VolumeDb = Mathf.Lerp(-22f, -8f, thr * 0.8f + Mathf.Clamp(rpm / 10000f, 0, 1) * 0.2f); }
  if (whineSnd.Stream != null) { whineSnd.PitchScale = Mathf.Clamp(0.5f + Player.speed / 40f, 0.5f, 3f); whineSnd.VolumeDb = Player.speed < 3f ? -60f : Mathf.Lerp(-34f, -22f, Mathf.Clamp(Player.speed / 70f, 0, 1)); }
  if (e >= 0 && lastE < 0) { PlaySfx("jack_engage.wav"); }
  if (e >= 0) { for (int i = 0; i < 4; i++) { float off = 1.0f + i * 0.6f, on = 3.4f + i * 0.6f; if (lastE < off && e >= off) PlaySfx("wheel_gun_loosen.wav"); if (lastE < off + 0.4f && e >= off + 0.4f) PlaySfx("wheel_set_down.wav"); if (lastE < on && e >= on) PlaySfx("wheel_gun_tighten.wav"); } if (lastE < 6.6f && e >= 6.6f) PlaySfx("jack_release.wav"); }
  lastE = e;
 }
 class Rival { public Vehicle V; public Node3D Node; public Node3D[] Pivots; public float LastY, Spin; public Vec Start; }
 readonly System.Collections.Generic.List<Rival> rivals = new System.Collections.Generic.List<Rival>();
 int loadedThisFrame; void EnsureRivals() { loadedThisFrame = 0;
  foreach (var r in rivals) { if (r.Node != null) continue; if (loadedThisFrame++ >= 1) break; int ci = r.V.carModel; r.Node = CarLoader.Load(CarCatalog.Folders[ci]); Car.GetParent().AddChild(r.Node); CarLoader.ApplyExact(r.Node, ci); r.Pivots = CarLoader.AttachWheels(r.Node, ci); r.Node.Visible = false; }
 }
 void UpdateRivals(float delta) {
  if (rivals.Count == 0) return; EnsureRivals(); var space = GetWorld3D().DirectSpaceState;
  foreach (var r in rivals) {
   if (r.Node == null) continue; var v = r.V; var w = ToWorld(v.position, 0); float refY = float.IsNaN(r.LastY) ? Car.Position.Y : r.LastY;if(float.IsNaN(r.LastY)){var seed=Contact.Ground(space,w,refY,float.MaxValue);if(!seed.found)continue;r.LastY=refY=seed.y;r.Node.Position=ToWorld(v.position,seed.y);Contact.MarkGood(v,true);}
   Contact.Walls(space,v,r.Start,refY,Contact.BodySize(v.carModel),new Vector3(VehicleDimensions.CenterX[v.carModel],VehicleDimensions.CenterY[v.carModel],VehicleDimensions.CenterZ[v.carModel]));w=ToWorld(v.position,0);
   var gr = Contact.Ground(space, w, refY, 1.0f);
   if(Contact.Contain(v,r.Start,gr.near,30f,Race.track)){w=ToWorld(v.position,0);gr=Contact.Ground(space,w,refY,1f);}Contact.MarkGood(v,gr.near&&Mathf.Abs(v.lateral)<=30f);
   float y = gr.found ? gr.y : refY; r.LastY = y; Vector3 n = gr.found && gr.normal.Y > 0.7f ? gr.normal : Vector3.Up;
   var fw = new Vector3(Mathf.Sin(v.heading), 0, Mathf.Cos(v.heading)); var f2 = (fw - n * fw.Dot(n)).Normalized(); var cx = n.Cross(f2).Normalized();
   r.Node.Position = new Vector3(w.X, y + 0.05f, w.Z); r.Node.Basis = new Basis(cx, n, f2); r.Node.Visible = true;
   if (r.Pivots != null) { r.Spin += Vec.Dot(v.velocity, v.Forward) * delta; for (int wi = 0; wi < 4; wi++) { var sp = r.Pivots[wi].GetNodeOrNull<Node3D>("Spin"); if (sp == null) continue; float sgn = r.Pivots[wi].Position.X < 0 ? -1f : 1f; sp.Rotation = new Vector3(r.Spin * sgn / (r.Pivots[wi].HasMeta("radius") ? r.Pivots[wi].GetMeta("radius").AsSingle() : .34f), 0, 0); } }
  }
 }
  float PitSequence(float e) {
  if (WheelPivots == null) return 0; if (e < 0) { foreach (var wp in WheelPivots) { wp.Visible = true;wp.GetNode<Node3D>("Spin").Visible=true; } return 0; }
  float lift = e < 0.8f ? 0.14f * (e / 0.8f) : e < 6.4f ? 0.14f : 0.14f * (1f - Mathf.Clamp((e - 6.4f) / 0.8f, 0, 1));
  // wheels come off one by one 1.0-3.4 s (front-left, front-right, rear-left, rear-right) and go back on 3.4-6.0 s in the same order
  for (int i = 0; i < 4; i++) { float off = 1.0f + i * 0.6f, on = 3.4f + i * 0.6f; WheelPivots[i].Visible=true;WheelPivots[i].GetNode<Node3D>("Spin").Visible = !(e >= off && e < on); }
  return lift;
 }
 static float DeadZone(float v, float dz) { float a = Mathf.Abs(v); if (a <= dz) return 0; return Mathf.Sign(v) * (a - dz) / (1f - dz); }
 static Vector3 ToWorld(Vec p, float y) => new Vector3(p.x, y, p.z);
 void CaptureShot(string fallback) {
  if (DisplayServer.GetName()=="headless") { GD.Print("SCREENSHOT_SKIPPED headless (logic check only)"); return; }
  string path=System.Environment.GetEnvironmentVariable("MYSIM_SHOT") ?? fallback;GD.Print("SCREENSHOT_BEGIN ",path);
  var image=GetViewport().GetTexture().GetImage();var error=image.SavePng(path);GD.Print("SCREENSHOT_END ",error," size=",image.GetWidth(),"x",image.GetHeight());if(error!=Error.Ok)throw new System.IO.IOException("Screenshot failed: "+error);
 }
 ulong lastHeartbeat; double worstRenderMs;
 public override void _Process(double delta) {
  worstRenderMs = System.Math.Max(worstRenderMs, delta * 1000);
  if (System.Environment.GetEnvironmentVariable("MYSIM_FPS") == null) return;
  ulong now = Time.GetTicksMsec(); if (now - lastHeartbeat < 2000) return;
  GD.Print("HEARTBEAT wallMs=", now, " paused=", Race?.paused, " sim=", Race?.clock, " fps=", Engine.GetFramesPerSecond(), " worstRenderMs=", worstRenderMs.ToString("0.0"));
  lastHeartbeat = now; worstRenderMs = 0;
 }
 public override void _PhysicsProcess(double delta) {
  if(!lastGroundY.HasValue){var seed=Contact.Ground(GetWorld3D().DirectSpaceState,ToWorld(Player.position,0),Car.Position.Y,float.MaxValue);if(!seed.found)return;lastGroundY=prevY=seed.y;groundSmooth=seed.y;groundInit=true;Car.Position=ToWorld(Player.position,seed.y);Contact.MarkGood(Player,true);GD.Print("SPAWN_GROUND y=",seed.y," clamped=",seed.clamped);}

  bool startBtn = false; { var pd = Input.GetConnectedJoypads(); if (pd.Count > 0) { bool raw = Input.IsJoyButtonPressed(pd[0], JoyButton.Start); if (!padStartSeen) { padStartSeen = true; padStartStuck = raw; } startBtn = raw && !padStartStuck; if (!raw) padStartStuck = false; } } bool tabNow = Input.IsKeyPressed(Key.Tab) || Input.IsKeyPressed(Key.Escape) || startBtn; if (tabNow && !tabWas) Race.paused = !Race.paused; tabWas = tabNow; if (Hud != null) Hud.Paused = Race.paused; if (System.Environment.GetEnvironmentVariable("MYSIM_PAUSETEST") != null) { if (!Race.paused && t > 2f && pausedAt == 0) { Race.paused = true; pausedAt = Time.GetTicksMsec(); } if (Race.paused && Time.GetTicksMsec() - pausedAt > 1500) { CaptureShot("pause.png"); CleanExit.Request(this); } } if (Race.paused) { UpdateAudio(-1f); return; }
  t += (float)delta;
  float thr = ControlBindings.Held(0) ? 1 : 0, brk = ControlBindings.Held(1) ? 1 : 0;
  float steer = (ControlBindings.Held(3) ? 1 : 0) - (ControlBindings.Held(2) ? 1 : 0);
  var pads = Input.GetConnectedJoypads(); if (pads.Count > 0) { int pid = pads[0]; float ax = DeadZone(Input.GetJoyAxis(pid, JoyAxis.LeftX), 0.08f), rt = Input.GetJoyAxis(pid, JoyAxis.TriggerRight), lt = Input.GetJoyAxis(pid, JoyAxis.TriggerLeft); if (Mathf.Abs(ax) > 0) steer = Mathf.Sign(ax) * Mathf.Pow(Mathf.Abs(ax), 1.35f); /* finer control around centre, full lock at the stop */ thr = Mathf.Max(thr, DeadZone(rt, 0.04f)); brk = Mathf.Max(brk, DeadZone(lt, 0.04f)); if (Input.IsJoyButtonPressed(pid, JoyButton.A)) Player.pitRequested = true; }
  if (AutoDrive && !AiTest) { thr = 1; brk = 0; steer = float.Parse(System.Environment.GetEnvironmentVariable("MYSIM_STEERBIAS") ?? "0", System.Globalization.CultureInfo.InvariantCulture); }
  if (AiTest) { Player.human = false; Player.pitRequested = System.Environment.GetEnvironmentVariable("MYSIM_NOPIT") == null; var st = (Player.pitActive ? "A" : "-") + (Player.servicing ? "S" : "-") + (Player.pitDone ? "D" : "-"); if (st != lastSt) { GD.Print("PITSTATE t=", t.ToString("0.0"), " ", st, " progress=", Player.progress.ToString("0.000"), " speed=", Player.speed.ToString("0.0"), " lap=", Player.completedLaps, " wear=", Player.Wear.ToString("0.00")); lastSt = st; } }
  if (TouchControls.Active && Settings.Get("tilt", false)) { var acc = Input.GetAccelerometer(); if (acc.LengthSquared() > 0.01f) { float tx = DeadZone(Mathf.Clamp(-acc.X / 5.0f, -1f, 1f), 0.05f); if (Mathf.Abs(tx) > 0) steer = tx; } }
  if (TouchControls.Active) { if (TouchControls.Throttle > 0) thr = 1; if (TouchControls.Brake > 0) brk = 1; if (TouchControls.Steer != 0) steer = TouchControls.Steer; }
  if (ControlBindings.Held(5)) Player.pitRequested = true;
  // Analog-style steering: keyboard digital input ramps up/down and the available lock shrinks with speed (no instant full-lock snap-spins).
  bool padSteer = Input.GetConnectedJoypads().Count > 0 && Mathf.Abs(Input.GetJoyAxis(Input.GetConnectedJoypads()[0], JoyAxis.LeftX)) > 0.08f;
  if (!padSteer && !AutoDrive) { float rate = Mathf.Abs(steer) > 0.01f ? 2.6f : 5.0f; if (Mathf.Sign(steer) != Mathf.Sign(steerCmd) && Mathf.Abs(steer) > 0.01f) rate = 5.0f; steerCmd = Mathf.MoveToward(steerCmd, steer, rate * (float)delta); float lim = Mathf.Clamp(1.0f - Player.speed / 120f, 0.35f, 1f); steer = steerCmd * lim; } else steerCmd = steer;
  Player.input = pedals.Update(thr, brk, steer, ControlBindings.Held(4), Vec.Dot(Player.velocity, Player.Forward), (float)delta, !AutoDrive && !AiTest && Player.human);

  var startPos = Player.position; foreach(var rival in rivals) rival.Start=rival.V.position; Race.Advance((float)delta); if (Player.dbgPush > 0.002f) pushFrames++; frames++; ResolveWalls(startPos); UpdateRivals((float)delta);
  var space = GetWorld3D().DirectSpaceState; var w = ToWorld(Player.position, 0);
  // Ground = drive-surface layer only (mask 1), continuous with the previous height. The old ray started at y=60 with the default (all-layer) mask, so it landed on barrier tops, bridges and stacked decks (car "flew").
  float refY = lastGroundY.HasValue ? lastGroundY.Value : Car.Position.Y;
  var gr = Contact.Ground(space, w, refY, 0.6f); if (Contact.Contain(Player, startPos, gr.near, 30f,Race.track)) { WallHits++; w = ToWorld(Player.position, 0); gr = Contact.Ground(space, w, refY, 0.6f); } Contact.MarkGood(Player, gr.near && Mathf.Abs(Player.lateral) <= 30f);
  var hit = gr.found ? new Godot.Collections.Dictionary() { { "p", 1 } } : new Godot.Collections.Dictionary();
  float y = gr.y; lastGroundY = y; if (y < minY) minY = y; if (y > maxY) maxY = y; if (prevY.HasValue) maxDy = Mathf.Max(maxDy, Mathf.Abs(y - prevY.Value)); prevY = y; Vector3 n = gr.normal;
  float h = Player.heading; var fwd = new Vector3(Mathf.Sin(h), 0, Mathf.Cos(h));
  // Four-wheel contact: sample the drive surface under each wheel, put the chassis on the average height and pitch/roll it from the wheel heights (no floating, no wheels in the ground).
  Vector3 carPos = new Vector3(w.X, y + 0.05f, w.Z);
  if (WheelPivots != null && WheelPivots.Length == 4) {
   var rotOnly = Car.Basis; var cp = new Vector3[4]; float avg = 0; int ok = 0;
   for (int wi = 0; wi < 4; wi++) { var off = rotOnly * WheelPivots[wi].Position; var wp = new Vector3(w.X + off.X, 0, w.Z + off.Z); var gi = Contact.Ground(space, wp, refY, 0.6f); cp[wi] = new Vector3(wp.X, gi.found ? gi.y : y, wp.Z); avg += cp[wi].Y; if (gi.found) ok++; }
   avg /= 4f; var front = (cp[0] + cp[1]) * 0.5f - (cp[2] + cp[3]) * 0.5f; var side = (cp[1] + cp[3]) * 0.5f - (cp[0] + cp[2]) * 0.5f;
   float tgtY = ok > 0 ? avg : y; groundSmooth = groundInit ? Mathf.Lerp(groundSmooth, tgtY, 1f - Mathf.Exp(-40f * (float)delta)) : tgtY; groundInit = true;
   carPos = new Vector3(w.X, groundSmooth, w.Z);
   var nn = front.Cross(side).Normalized(); if (nn.Y < 0) nn = -nn; if (ok == 4 && nn.Y > 0.7f) n = nn;
   { // keep every wheel at or above the surface: lower/raise the chassis so the lowest wheel just touches (no hovering wheel, no wheel in the road)
     var f3 = (fwd - n * fwd.Dot(n)).Normalized(); var cx = n.Cross(f3).Normalized(); var bas = new Basis(cx, n, f3); float minGap = 1e9f;
     for (int wi = 0; wi < 4; wi++) { var pl = WheelPivots[wi].Position; float gap = carPos.Y + (bas * pl).Y - pl.Y - cp[wi].Y; if (gap < minGap) minGap = gap; }
     if (ok == 4 && System.Environment.GetEnvironmentVariable("MYSIM_NOWHEELFIT") == null) carPos.Y -= Mathf.Clamp(minGap, -0.4f, 0.4f);
     // independent wheel travel: a wheel over a drop droops down to the road, one over a crest compresses, so no wheel hovers (simple suspension look)
     if (basePivotY == null) { basePivotY = new float[4]; for (int wi = 0; wi < 4; wi++) basePivotY[wi] = WheelPivots[wi].Position.Y; }
     for (int wi = 0; wi < 4; wi++) { var pl = WheelPivots[wi].Position; float rest = basePivotY[wi]; float gap = carPos.Y + (bas * new Vector3(pl.X, rest, pl.Z)).Y - rest - cp[wi].Y; float target = ok == 4 ? Mathf.Clamp(-gap, -0.12f, 0.3f) : 0f; wheelTravel[wi] = Mathf.Lerp(wheelTravel[wi], target, 1f - Mathf.Exp(-30f * (float)delta)); WheelPivots[wi].Position = new Vector3(pl.X, rest + wheelTravel[wi], pl.Z); } }
  }
  // Pit stop sequence (service time 7.5 s): jack up, remove and refit each wheel in turn, lower, release. Driven by the real service timer.
  if(WheelPivots!=null&&Player!=null&&(!WheelPivots[0].HasMeta("displayed_compound")||WheelPivots[0].GetMeta("displayed_compound").AsInt32()!=(int)Player.tyres[0].compound))TyreAppearance.Apply(WheelPivots,Player.tyres[0].compound);
  float lift = PitSequence(Player.servicing ? 7.5f - Player.serviceRemaining : -1f); carPos += new Vector3(0, lift, 0); lastLift = lift; UpdateCrew(Player.servicing ? 7.5f - Player.serviceRemaining : -1f); if (!condsApplied) { condsApplied = true; if (Conditions.Wet == 2) Conditions.AddRain(Cam); if (Conditions.Tod == 3 || Conditions.Wet == 2) { foreach (float sx in new[] { -0.7f, 0.7f }) { var hl = new SpotLight3D { SpotRange = 90f, SpotAngle = 32f, LightEnergy = 45f, SpotAttenuation = 0.6f, LightColor = new Color(1f, 0.96f, 0.85f), ShadowEnabled = false, Position = new Vector3(sx, 0.55f, CarSize.Z * 0.45f), RotationDegrees = new Vector3(-6, 180, 0) }; Car.AddChild(hl); } } } if (!settingsApplied && !AiTest) { settingsApplied = true; Player.abs = Settings.Get("abs", true); Player.traction = Settings.Get("tc", true); } UpdateAudio(Player.servicing ? 7.5f - Player.serviceRemaining : -1f);
  Car.Position = carPos;
  var f2 = (fwd - n * fwd.Dot(n)).Normalized(); var carX = n.Cross(f2).Normalized(); Car.Basis = new Basis(carX, n, f2); /* proper rotation (det +1): nose (+Z local) = travel direction. The old basis was left-handed and Orthonormalized() left it mirrored, so the nose faced the camera. */
  if (WheelPivots != null) { spinAngle += Vec.Dot(Player.velocity, Player.Forward) * (float)delta; for (int wi = 0; wi < 4; wi++) { var sp = WheelPivots[wi].GetNode<Node3D>("Spin"); float sgn = WheelPivots[wi].Position.X < 0 ? -1f : 1f; sp.Rotation = new Vector3(spinAngle * sgn / (WheelPivots[wi].HasMeta("radius") ? WheelPivots[wi].GetMeta("radius").AsSingle() : .34f), 0, 0); if (wi < 2) { var rot = WheelPivots[wi].Position.X < 0 ? Mathf.Pi : 0f; WheelPivots[wi].Rotation = new Vector3(0, rot + Mathf.Clamp(Player.steering, -0.34f, 0.34f), 0); } } }
  // chase camera
  bool cNow = ControlBindings.Held(6) || (Input.GetConnectedJoypads().Count > 0 && Input.IsJoyButtonPressed(Input.GetConnectedJoypads()[0], JoyButton.Y)); if (cNow && !cWas) CamMode = (CamMode + 1) % 4; cWas = cNow; if (CamMode != shownMode) { shownMode = CamMode; foreach (var mn in Car.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)mn; if (!mi.HasMeta("grp")) continue; string gp = mi.GetMeta("grp").AsString(); mi.Visible = !mi.HasMeta("wheel_hidden") && (gp == "int" ? CamMode == 2 : CamMode != 2); } }
  if (WheelPivots != null && CamMode == 2) foreach (var wp in WheelPivots) wp.Visible = false;
  if (System.Environment.GetEnvironmentVariable("MYSIM_CLOSE") != null) { var sideDir = new Vector3(fwd.Z, 0, -fwd.X); Cam.Position = Car.Position + sideDir * 4.2f + Vector3.Up * 0.45f - fwd * 0.8f; Cam.LookAt(Car.Position + Vector3.Up * 0.35f); Cam.Fov = 45f; if (AutoDrive && t >= AutoSeconds) { CaptureShot("close.png"); CleanExit.Request(this); } return; }
  if (System.Environment.GetEnvironmentVariable("MYSIM_TREEVIEW") != null) { if (treeTarget == null) { int want = int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_TREEVIEW")); var cands = new System.Collections.Generic.List<(float, MeshInstance3D)>(); foreach (var tn in GetTree().Root.FindChildren("*", "MeshInstance3D", true, false)) { var tmi = (MeshInstance3D)tn; string tnm = tmi.Name.ToString().ToLowerInvariant(); if (!tnm.StartsWith("org_tree")) continue; var tab = tmi.GlobalTransform * tmi.GetAabb(); if (tab.Size.Y < 3f) continue; cands.Add(((tab.GetCenter() - Car.GlobalPosition).Length(), tmi)); } cands.Sort((x, y) => x.Item1.CompareTo(y.Item1)); if (cands.Count > 0) { treeTarget = cands[Mathf.Min(want, cands.Count - 1)].Item2; var tb = treeTarget.GlobalTransform * treeTarget.GetAabb(); { var tm = treeTarget.Mesh; for (int si = 0; si < tm.GetSurfaceCount(); si++) { var arr = tm.SurfaceGetArrays(si); var vv = (Vector3[])arr[(int)Mesh.ArrayType.Vertex]; var mt = treeTarget.GetActiveMaterial(si) as StandardMaterial3D; GD.Print("TREESURF ", si, " verts=", vv.Length, " tex=", mt?.AlbedoTexture?.ResourcePath, " transp=", mt?.Transparency, " scissor=", mt?.AlphaScissorThreshold, " cull=", mt?.CullMode, " vis=", treeTarget.Visible, " layers=", treeTarget.Layers); } } GD.Print("TREEVIEW ", treeTarget.Name, " dist=", cands[Mathf.Min(want, cands.Count - 1)].Item1, " size=", tb.Size, " center=", tb.GetCenter()); } } if (treeTarget != null) { var tab2 = treeTarget.GlobalTransform * treeTarget.GetAabb(); var tc = tab2.GetCenter(); float dist = Mathf.Max(tab2.Size.Y * 1.8f, 12f); Cam.GlobalPosition = tc + new Vector3(dist * 0.7f, tab2.Size.Y * 0.1f, dist * 0.7f); Cam.LookAt(tc); } }
  float speed = Player.speed; float look = Mathf.Clamp(speed / 20f, 0, 1) * 4f + 3f;
  if (CamMode == 0 || CamMode == 1) {
   float len = Mathf.Max(CarSize.Z, 3.5f), hgt = Mathf.Max(CarSize.Y, 1.0f); float back = CamMode == 0 ? len * 0.95f : len * 1.8f, up = CamMode == 0 ? hgt * 1.15f + 0.2f : hgt * 2.2f; var target = ObstructCam(Car.Position + Vector3.Up * (hgt * 0.8f), Car.Position - fwd * back + Vector3.Up * up);
   if (!camInit) { Cam.Position = target; camInit = true; } Cam.Position = Cam.Position.Lerp(target, 1f - Mathf.Exp(-6f * (float)delta)); Cam.LookAt(Car.Position + fwd * look + Vector3.Up * 1.0f); Cam.Fov = Mathf.Lerp(Cam.Fov, 46f + Mathf.Clamp(speed / 80f, 0, 1) * 6f, 1f - Mathf.Exp(-3f * (float)delta));
  } else if (CamMode == 2) { Cam.Position = Car.GlobalTransform * new Vector3(-0.35f, 0.88f, 0.05f); Cam.GlobalTransform = new Transform3D(Car.GlobalTransform.Basis.Rotated(Car.GlobalTransform.Basis.Y.Normalized(), Mathf.Pi), Cam.Position); Cam.Fov = 75f; camInit = false; }
  else { Cam.Position = Car.GlobalTransform * new Vector3(0, 0.62f, 1.9f); Cam.GlobalTransform = new Transform3D(Car.GlobalTransform.Basis.Rotated(Car.GlobalTransform.Basis.Y.Normalized(), Mathf.Pi), Cam.Position); Cam.Fov = 70f; camInit = false; }
  if(AutoDrive&&AiTest&&AutoSeconds>250&&t>=AutoSeconds&&!Player.finished){GD.PrintErr("FAIL full-race simulation deadline track=",TrackIndex," progress=",Player.progress," lap=",Player.completedLaps," wallHits=",WallHits," damage=",Player.damage);CleanExit.Request(this,1);return;}
  if (AiTest && Player.finished) { finT += (float)delta; if (finT > (rivals.Count > 0 ? 14f : 1.5f)) { GD.Print("FINISHED t=", t.ToString("0.0"), " laps=", Player.completedLaps, " time=", Player.finishTime.ToString("0.00"), " best=", Player.bestLap.ToString("0.00"), " wallHits=", WallHits, " damage=", Player.damage.ToString("0.000000000",System.Globalization.CultureInfo.InvariantCulture)); CaptureShot("finish.png"); CleanExit.Request(this); } }
  if (!floatScanned && t > 0.6f && System.Environment.GetEnvironmentVariable("MYSIM_FINDFLOAT") != null) { floatScanned = true; var sp = GetWorld3D().DirectSpaceState; int rep = 0; foreach (var fn in GetTree().Root.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)fn; string nn = mi.Name.ToString().ToLowerInvariant(); if (!(nn.Contains("tree") || nn.Contains("bush") || nn.Contains("org_"))) continue; var ab = mi.GlobalTransform * mi.GetAabb(); var c = ab.GetCenter(); using var rq = PhysicsRayQueryParameters3D.Create(new Vector3(c.X, c.Y + 300f, c.Z), new Vector3(c.X, c.Y - 300f, c.Z), 1); var rr = sp.IntersectRay(rq); if (rr.Count == 0) continue; float gy = ((Vector3)rr["position"]).Y; float gap = ab.Position.Y - gy; if (gap > 2.5f && rep++ < 12) GD.Print("FLOAT ", mi.Name, " gap=", gap.ToString("0.0"), " center=", c, " size=", ab.Size); } }
  if (System.Environment.GetEnvironmentVariable("MYSIM_FPS") != null) { maxDelta = Mathf.Max(maxDelta, (float)delta); fpsT += (float)delta; if (fpsT > 2f) { GD.Print("FPS ", Engine.GetFramesPerSecond(), " maxFrameMs=", (maxDelta * 1000f).ToString("0"), " t=", t.ToString("0.0"), " speed=", Player.speed.ToString("0")); fpsT = 0; maxDelta = 0; } }
  if (AiTest && System.Environment.GetEnvironmentVariable("MYSIM_TRACE") != null && (int)(t / 4f) != lastTrace) { lastTrace = (int)(t / 4f); GD.Print("TR t=", t.ToString("0"), " p=", Player.progress.ToString("0.000"), " v=", Player.speed.ToString("0.0"), " lat=", Player.lateral.ToString("0.0"), " hits=", WallHits, " dmg=", Player.damage.ToString("0.00"), " push=", Player.dbgPush.ToString("0.0000"), " acc=", Player.dbgAcc.ToString("0.0"), " pushFrames=", pushFrames, "/", frames); }
  if (AutoDrive && !(AiTest && AutoSeconds > 250) && t >= AutoSeconds) { GD.Print("YRANGE min=", minY.ToString("0.00"), " max=", maxY.ToString("0.00"), " maxStepDy=", maxDy.ToString("0.00")); GD.Print("DRIVETEST t=", t, " speed=", Player.speed, " progress=", Player.progress, " lateral=", Player.lateral, " lap=", Player.completedLaps, " wallHits=", WallHits, " onSurface=", hit.Count > 0, " pos=", Car.Position); CaptureShot("drive.png"); CleanExit.Request(this); }
 }
}}






























