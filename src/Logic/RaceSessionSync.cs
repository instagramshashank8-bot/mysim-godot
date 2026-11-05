using System;
using System.Collections.Generic;
namespace MySimulator.Rebuild {
// LAN race-sync bridge, logic half (the adopted LAN plan's central-risk mitigation):
// Wire.ReadSnapshot builds a NEW Race for every STATE packet, so the view layer must be
// keyed by car id and updated IN PLACE - each car's view is created exactly once and every
// later snapshot updates it. Pure logic with no Godot types: the host app implements
// IRaceView/ICarView over its persistent scene nodes; unit-tested in CoreRunner.
public interface ICarView {
    void EnsureCreated(int carModel, string driver);
    void ApplyPose(float x, float z, float heading, bool hasBodyPose, float bodyHeight, float bodyQx, float bodyQy, float bodyQz, float bodyQw);
    void ApplyState(float damage, float fuel, float speed, int completedLaps, bool finished, float finishTime, bool pitActive, bool servicing, float serviceRemaining);
}
public interface IRaceView {
    ICarView CreateCarView(int id);
    void ApplyRaceState(float clock, float countdown, bool started, int trackVariant, int targetLaps, float wetness);
}
public sealed class SnapshotApplier {
    readonly IRaceView view;
    readonly Dictionary<int, ICarView> views = new Dictionary<int, ICarView>();
    public int AppliedSnapshots { get; private set; }
    public int CarViews { get { return views.Count; } }
    public bool SawRaceState { get; private set; }
    public SnapshotApplier(IRaceView view) { this.view = view ?? throw new ArgumentNullException(nameof(view)); }
    // Applies one parsed snapshot (the client pipeline: Wire.Snapshot -> ReadSnapshot ->
    // Apply). Idempotent per car: the first sight of an id creates its view, later sights
    // update it in place. A car the snapshot stops sending keeps its last view (no churn).
    public void Apply(Race snapshot) {
        if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
        view.ApplyRaceState(snapshot.clock, snapshot.countdown, snapshot.started, snapshot.track.variant, snapshot.targetLaps, snapshot.wetness);
        SawRaceState = true;
        foreach (var car in snapshot.cars) {
            if (!views.TryGetValue(car.id, out var carView)) { carView = view.CreateCarView(car.id); views[car.id] = carView; }
            carView.EnsureCreated(car.carModel, car.driver);
            carView.ApplyPose(car.position.x, car.position.z, car.heading, car.hasBodyPose, car.bodyHeight, car.bodyQx, car.bodyQy, car.bodyQz, car.bodyQw);
            carView.ApplyState(car.damage, car.fuel, car.speed, car.completedLaps, car.finished, car.finishTime, car.pitActive, car.servicing, car.serviceRemaining);
        }
        AppliedSnapshots++;
    }
    // The view for a car id (null before its first snapshot): the host app uses this to
    // find the LOCAL player's view for camera follow and HUD binding.
    public ICarView ViewFor(int id) { return views.TryGetValue(id, out var carView) ? carView : null; }
}
}
