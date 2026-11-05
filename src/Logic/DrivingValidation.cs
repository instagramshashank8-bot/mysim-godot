using System;
using System.Collections.Generic;
using System.Globalization;
namespace MySimulator.Rebuild {
// Controlled continuous-driving validation: data-driven scripted-input scenarios with
// physics-rate evidence capture and fail-closed assertions. Pure logic with no Godot
// types so tools/CoreRunner (core.yml) compiles and unit-tests it. The Godot-side host
// (NativeRaceController) builds samples from the live simulation, applies the returned
// commands, and performs the mechanical reset between profiles.
public static class DrivingValidation {
    public const string ProfilesResourceDefault = "res://data/native_driving_validation_profiles.json";
    public static readonly string[] KnownReferences = { "brakeStop60Metres", "accel0To60Seconds", "steerActuatorRate", "vehicleMass" };
    public static readonly string[] KnownMetrics = {
        "pathLength","duration","startSpeed","endSpeed","endLongitudinalSpeed","speedRiseTime","speedDropTime",
        "trackLateralAbsMax","lateralDriftAbs","headingDriftAbs","longitudinalAccelMin","longitudinalAccelMax",
        "lateralAccelPeakAbs","yawRatePeakAbs","yawRateEndAbs","meanYawRate","meanYawRateAbsMin",
        "slipAnglePeakAbs","longitudinalSlipPeakAbs","wheelAnglePeakAbs","wheelAngleTargetPeakAbs",
        "steerResponseTime","steerTrackingRatio","steerSettleTime","steerSettleRatio","groundedFraction","chassisContactSamples",
        "nonSurfaceContactSamples","offTrackFraction","impactEventsDelta","maxDeltaVRise","positionJumpMax","suspensionCompressionMin",
        "suspensionExtensionMax","uprightMinCosine","impactTickPeakDeltaV","impactContactSeconds","impactImpulseEstimate","impactPeakForceEstimate",
        "wheelBandContactSamples","maxSpeed","throttleHoldSeconds","raceStartedFraction",
        "recoveryWindowCount","recoveryRecoveredFraction","recoveryMotionTimeMax","recoveryMotionTimeMean",
        "recoveryPathLengthMax","recoveryOffTrackFractionMax","recoveryThrottleFractionMean",
        "recoveryBrakeFractionMean","recoverySteerPeakAbsMax",
        "bodyPanelContactSamples","mixedContactSamples","wheelBandContactSeconds","bodyPanelContactSeconds",
        "wheelBandTickPeakDeltaV","bodyPanelTickPeakDeltaV"
    };
    // The recovery family, listed separately so the host emits it once per run (not per phase):
    // the windows span phases, so phase-keyed values would be misleading duplicates.
    public static readonly string[] RecoveryMetricNames = {
        "recoveryWindowCount","recoveryRecoveredFraction","recoveryMotionTimeMax","recoveryMotionTimeMean",
        "recoveryPathLengthMax","recoveryOffTrackFractionMax","recoveryThrottleFractionMean",
        "recoveryBrakeFractionMean","recoverySteerPeakAbsMax"
    };
    // The contact-archetype family (guard band vs body panel vs mixed): emitted once per run
    // when the profile sets setup.recordContactArchetypes; phase-scoped values stay available
    // to assertions through ComputeMetric.
    public static readonly string[] ContactArchetypeMetricNames = {
        "bodyPanelContactSamples","mixedContactSamples","wheelBandContactSeconds","bodyPanelContactSeconds",
        "wheelBandTickPeakDeltaV","bodyPanelTickPeakDeltaV"
    };
    public static readonly string[] KnownOps = { "between", "betweenReference", "lt", "lte", "gt", "gte" };

    public sealed class Sample {
        public string phaseId;
        public double t;
        public double speed, longitudinalSpeed, lateralSpeed, heading, yawRate, upY;
        public double wheelAngle, wheelAngleTarget, steerCommand, throttle, brake;
        public double x, z, trackLateral, progress, water;
        public bool onDrivingSurface;
        // True when any solver contact this tick is NOT the drive surface (wall/barrier hit).
        public bool nonSurfaceContact;
        // True when a contact part this tick belongs to the hub-mounted wheel-assembly band
        // (wheelguard/caliper): the arch-position proof for the restored collision members.
        public bool wheelBandContact;
        // True when a contact part this tick is a non-drive body panel (neither the drive
        // surface nor the wheel-assembly band): the second archetype of the guard-vs-panel
        // force characterization. A tick touching BOTH bands is mixed (both flags set) and
        // contributes to both archetypes' signatures; mixedContactSamples measures it.
        public bool bodyPanelContact;
        // Race-start sequence state (the grid/start validation).
        public bool raceStarted;public double raceClock,countdown;
        public int groundedMask, chassisContacts, impactEvents;
        public double maxDeltaV;
        public double[] loads, springLengths, longitudinalSlip, lateralSlipAngle;
    }

    public struct Command {
        public double Throttle, Brake, Steer;
        public bool Handbrake, Reverse;
        public static Command Neutral => new Command();
    }

    public sealed class Profile {
        public string Id, Title, Purpose;
        public bool RequireRotationalTyres, RequireDamage, RequireGridStart;
        public bool RecordContactArchetypes;
        public RecoverySpec Recovery;
        public double MaxDurationSeconds = 60, AbortLateralAbs = 25, AbortPositionJump = 2.5;
        public int SampleEveryTicks = 1, MaxSamples = 12000;
        public List<Phase> Phases = new List<Phase>();
        public List<Assertion> Assertions = new List<Assertion>();
    }

    public sealed class Phase {
        public string Id;
        public double Throttle, Brake, Steer;
        public bool Handbrake, Reverse;
        public double? ForSeconds, MinSeconds, MaxSeconds;
        public double? UntilSpeedAtLeast, UntilSpeedBelow;
        public int? UntilChassisContactsAtLeast, UntilImpactEventsAtLeast;
        public bool UntilNonSurfaceContact, UntilOffTrackObserved, UntilRaceStarted;
        public bool Terminates => ForSeconds.HasValue || UntilSpeedAtLeast.HasValue || UntilSpeedBelow.HasValue || UntilChassisContactsAtLeast.HasValue || UntilImpactEventsAtLeast.HasValue || UntilNonSurfaceContact || UntilOffTrackObserved || UntilRaceStarted;
    }

    public sealed class Assertion {
        public string Id, Metric, Op, Reference;
        public double Value, Min, Max, FactorMin, FactorMax;
        public bool HasMin, HasMax, HasFactorMin, HasFactorMax, HasValue;
    }

    public sealed class AssertionResult {
        public string Id, Metric, Op, Expected;
        public bool Passed;
        public double? Actual;
    }

    // Post-impact recovery-window forensics (the wall judgement sheet's R-window), flag-gated
    // by setup.recordRecovery: every rise of the cumulative impact counter opens a window that
    // records what the car then did under its own drive - the motion recovery time, the path
    // covered, the inputs applied and the surface state - until motion holds again or the
    // window's cap expires. Derived purely from recorded samples; a profile without the flag
    // produces null recovery metrics, which fail any assertion on them (never a silent zero).
    public sealed class RecoverySpec {
        public bool Enabled;
        public double MotionSpeed = 1.0, HoldSeconds = 1.0, MaxSeconds = 20;
    }

    public sealed class RecoveryWindow {
        public double startT, endT, startSpeed;
        public bool Recovered;
        public double? motionTime;
        public double pathLength, maxReverseSpeed, offTrackFraction, throttleFraction, brakeFraction, steerPeakAbs;
    }

    // Motion is the speed MAGNITUDE at or above MotionSpeed held for HoldSeconds: a deliberate
    // reverse-out counts as recovered motion (maxReverseSpeed records it); whether the car
    // drives away FORWARD stays the recover-phase assertions' job. The close time is the
    // moment the sustained interval completes (interpolated between samples; sample rate is
    // the physics rate or finer). MaxSeconds bounds only UNRESOLVED windows: an achieved
    // recovery always records its true motionTime even beyond the cap.
    public static List<RecoveryWindow> RecoveryWindows(List<Sample> s, RecoverySpec spec) {
        var windows = new List<RecoveryWindow>();
        if (s == null || s.Count < 2 || spec == null || !spec.Enabled) return windows;
        for (int i = 1; i < s.Count; i++) {
            if (s[i].impactEvents <= s[i - 1].impactEvents) continue;
            var w = new RecoveryWindow { startT = s[i].t, startSpeed = s[i].speed, endT = s[i].t };
            double path = 0, reverse = 0, steerPeak = 0; int off = 0, thr = 0, brk = 0, n = 0;
            int closeIndex = -1; double closeT = 0, runStart = 0; bool running = false;
            for (int j = i; j < s.Count; j++) {
                n++;
                if (j > i) path += Math.Sqrt((s[j].x - s[j - 1].x) * (s[j].x - s[j - 1].x) + (s[j].z - s[j - 1].z) * (s[j].z - s[j - 1].z));
                if (!s[j].onDrivingSurface) off++;
                if (s[j].throttle > 0.5) thr++;
                if (s[j].brake > 0.5) brk++;
                if (-s[j].longitudinalSpeed > reverse) reverse = -s[j].longitudinalSpeed;
                if (Math.Abs(s[j].steerCommand) > steerPeak) steerPeak = Math.Abs(s[j].steerCommand);
                if (j > i && s[j].speed >= spec.MotionSpeed) {
                    if (!running || s[j - 1].speed < spec.MotionSpeed) runStart = s[j].t;
                    running = true;
                    if (s[j].t - runStart >= spec.HoldSeconds) { closeIndex = j; closeT = runStart + spec.HoldSeconds; break; }
                } else running = false;
                if (spec.MaxSeconds > 0 && j > i && s[j].t - s[i].t >= spec.MaxSeconds) break;
            }
            w.pathLength = path; w.maxReverseSpeed = reverse; w.steerPeakAbs = steerPeak;
            w.offTrackFraction = n > 0 ? (double)off / n : 0;
            w.throttleFraction = n > 0 ? (double)thr / n : 0;
            w.brakeFraction = n > 0 ? (double)brk / n : 0;
            if (closeIndex >= 0) { w.Recovered = true; w.endT = closeT; w.motionTime = closeT - s[i].t; }
            else w.endT = s[i + n - 1].t;
            windows.Add(w);
        }
        return windows;
    }

    static List<RecoveryWindow> RecoveryWindowsOf(Profile profile, List<Sample> all) {
        if (profile == null || profile.Recovery == null || !profile.Recovery.Enabled || all == null || all.Count < 2) return null;
        return RecoveryWindows(all, profile.Recovery);
    }

    static double Num(System.Text.Json.JsonElement e, string key, double fallback) {
        return e.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetDouble() : fallback;
    }
    static string Str(System.Text.Json.JsonElement e, string key) {
        return e.TryGetProperty(key, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
    }

    public static Profile ParseProfile(System.Text.Json.JsonElement e) {
        var p = new Profile { Id = Str(e, "id"), Title = Str(e, "title") ?? "", Purpose = Str(e, "purpose") ?? "" };
        if (string.IsNullOrEmpty(p.Id)) throw new Exception("Driving validation profile is missing its id");
        if (e.TryGetProperty("requires", out var req)) {
            p.RequireRotationalTyres = req.TryGetProperty("rotationalTyres", out var rt) && rt.ValueKind == System.Text.Json.JsonValueKind.True;
            p.RequireDamage = req.TryGetProperty("damageEnabled", out var de) && de.ValueKind == System.Text.Json.JsonValueKind.True;
        }
        if (e.TryGetProperty("setup", out var setup)) {
            p.RequireGridStart = setup.TryGetProperty("requiresGridStart", out var gs) && gs.ValueKind == System.Text.Json.JsonValueKind.True;
            p.RecordContactArchetypes = setup.TryGetProperty("recordContactArchetypes", out var rca) && rca.ValueKind == System.Text.Json.JsonValueKind.True;
            if (setup.TryGetProperty("recordRecovery", out var rr)) {
                p.Recovery = new RecoverySpec { Enabled = true };
                if (rr.ValueKind == System.Text.Json.JsonValueKind.Object) {
                    p.Recovery.MotionSpeed = Num(rr, "motionSpeed", p.Recovery.MotionSpeed);
                    p.Recovery.HoldSeconds = Num(rr, "holdSeconds", p.Recovery.HoldSeconds);
                    p.Recovery.MaxSeconds = Num(rr, "maxSeconds", p.Recovery.MaxSeconds);
                }
                if (p.Recovery.MotionSpeed <= 0 || p.Recovery.HoldSeconds < 0 || p.Recovery.MaxSeconds <= 0)
                    throw new Exception("Driving validation profile " + p.Id + " has out-of-range recovery recording settings");
            }
        }
        if (e.TryGetProperty("abort", out var abort)) {
            p.MaxDurationSeconds = Num(abort, "maxDurationSeconds", p.MaxDurationSeconds);
            p.AbortLateralAbs = Num(abort, "lateralAbsBeyond", p.AbortLateralAbs);
            p.AbortPositionJump = Num(abort, "positionJumpBeyond", p.AbortPositionJump);
        }
        p.SampleEveryTicks = (int)Num(e, "sampleEveryTicks", 1);
        p.MaxSamples = (int)Num(e, "maxSamples", 12000);
        if (p.SampleEveryTicks < 1 || p.MaxSamples < 1) throw new Exception("Driving validation profile " + p.Id + " has invalid sampling settings");
        if (!e.TryGetProperty("phases", out var phases) || phases.ValueKind != System.Text.Json.JsonValueKind.Array || phases.GetArrayLength() == 0)
            throw new Exception("Driving validation profile " + p.Id + " has no phases");
        foreach (var pe in phases.EnumerateArray()) {
            var ph = new Phase { Id = Str(pe, "id") };
            if (string.IsNullOrEmpty(ph.Id)) throw new Exception("Driving validation profile " + p.Id + " has a phase without id");
            ph.Throttle = Num(pe, "throttle", 0); ph.Brake = Num(pe, "brake", 0); ph.Steer = Num(pe, "steer", 0);
            ph.Handbrake = pe.TryGetProperty("handbrake", out var hb) && hb.ValueKind == System.Text.Json.JsonValueKind.True;
            ph.Reverse = pe.TryGetProperty("reverse", out var rv) && rv.ValueKind == System.Text.Json.JsonValueKind.True;
            if (pe.TryGetProperty("forSeconds", out var fs) && fs.ValueKind == System.Text.Json.JsonValueKind.Number) ph.ForSeconds = fs.GetDouble();
            if (pe.TryGetProperty("minSeconds", out var ms) && ms.ValueKind == System.Text.Json.JsonValueKind.Number) ph.MinSeconds = ms.GetDouble();
            if (pe.TryGetProperty("maxSeconds", out var xs) && xs.ValueKind == System.Text.Json.JsonValueKind.Number) ph.MaxSeconds = xs.GetDouble();
            if (pe.TryGetProperty("until", out var until) && until.ValueKind == System.Text.Json.JsonValueKind.Object) {
                if (until.TryGetProperty("speedAtLeast", out var sal) && sal.ValueKind == System.Text.Json.JsonValueKind.Number) ph.UntilSpeedAtLeast = sal.GetDouble();
                if (until.TryGetProperty("speedBelow", out var sb) && sb.ValueKind == System.Text.Json.JsonValueKind.Number) ph.UntilSpeedBelow = sb.GetDouble();
                if (until.TryGetProperty("chassisContactsAtLeast", out var cca) && cca.ValueKind == System.Text.Json.JsonValueKind.Number) ph.UntilChassisContactsAtLeast = (int)cca.GetDouble();
                if (until.TryGetProperty("impactEventsAtLeast", out var iea) && iea.ValueKind == System.Text.Json.JsonValueKind.Number) ph.UntilImpactEventsAtLeast = (int)iea.GetDouble();
                if (until.TryGetProperty("nonSurfaceContact", out var nsc)) ph.UntilNonSurfaceContact = nsc.ValueKind == System.Text.Json.JsonValueKind.True;
                if (until.TryGetProperty("offTrackObserved", out var oto)) ph.UntilOffTrackObserved = oto.ValueKind == System.Text.Json.JsonValueKind.True;
                if (until.TryGetProperty("raceStarted", out var rs)) ph.UntilRaceStarted = rs.ValueKind == System.Text.Json.JsonValueKind.True;
            }
            if (!ph.Terminates) throw new Exception("Driving validation profile " + p.Id + " phase " + ph.Id + " has no forSeconds or until termination");
            if (ph.MinSeconds.HasValue && ph.ForSeconds.HasValue && ph.MinSeconds.Value > ph.ForSeconds.Value)
                throw new Exception("Driving validation profile " + p.Id + " phase " + ph.Id + " has minSeconds above forSeconds");
            if (ph.Throttle < 0 || ph.Throttle > 1 || ph.Brake < 0 || ph.Brake > 1 || !double.IsFinite(ph.Steer) || Math.Abs(ph.Steer) > 1)
                throw new Exception("Driving validation profile " + p.Id + " phase " + ph.Id + " has out-of-range command values");
            p.Phases.Add(ph);
        }
        if (!e.TryGetProperty("assertions", out var assertions) || assertions.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new Exception("Driving validation profile " + p.Id + " has no assertions");
        foreach (var ae in assertions.EnumerateArray()) {
            var a = new Assertion { Id = Str(ae, "id"), Metric = Str(ae, "metric"), Op = Str(ae, "op") };
            if (string.IsNullOrEmpty(a.Id) || string.IsNullOrEmpty(a.Metric))
                throw new Exception("Driving validation profile " + p.Id + " has an assertion without id or metric");
            if (Array.IndexOf(KnownOps, a.Op) < 0) throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " uses unknown op " + a.Op);
            var colon = a.Metric.LastIndexOf(':');
            if (colon <= 0 || colon == a.Metric.Length - 1) throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " metric must be <name>:<phaseId|all>");
            var name = a.Metric.Substring(0, colon);
            if (Array.IndexOf(KnownMetrics, name) < 0) throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " uses unknown metric " + name);
            var scope = a.Metric.Substring(colon + 1);
            if (scope != "all" && p.Phases.FindIndex(x => x.Id == scope) < 0)
                throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " references unknown phase " + scope);
            a.Value = Num(ae, "value", 0);
            a.HasValue = ae.TryGetProperty("value", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number;
            if (ae.TryGetProperty("min", out var mn) && mn.ValueKind == System.Text.Json.JsonValueKind.Number) { a.Min = mn.GetDouble(); a.HasMin = true; }
            if (ae.TryGetProperty("max", out var mx) && mx.ValueKind == System.Text.Json.JsonValueKind.Number) { a.Max = mx.GetDouble(); a.HasMax = true; }
            if (ae.TryGetProperty("factorMin", out var fmn) && fmn.ValueKind == System.Text.Json.JsonValueKind.Number) { a.FactorMin = fmn.GetDouble(); a.HasFactorMin = true; }
            if (ae.TryGetProperty("factorMax", out var fmx) && fmx.ValueKind == System.Text.Json.JsonValueKind.Number) { a.FactorMax = fmx.GetDouble(); a.HasFactorMax = true; }
            a.Reference = Str(ae, "reference");
            if (a.Op == "between" && (!a.HasMin || !a.HasMax)) throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " needs min and max");
            if (a.Op == "betweenReference" && (string.IsNullOrEmpty(a.Reference) || !a.HasFactorMin || !a.HasFactorMax || a.FactorMin >= a.FactorMax))
                throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " needs reference and factorMin<factorMax");
            if ((a.Op == "lt" || a.Op == "lte" || a.Op == "gt" || a.Op == "gte") && !a.HasValue)
                throw new Exception("Driving validation profile " + p.Id + " assertion " + a.Id + " needs a value");
            p.Assertions.Add(a);
        }
        return p;
    }

    public static List<Profile> ParseProfiles(string json) {
        using var document = System.Text.Json.JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("profiles", out var profiles) || profiles.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new Exception("Driving validation profiles JSON has no profiles array");
        var result = new List<Profile>();
        var seen = new HashSet<string>();
        foreach (var e in profiles.EnumerateArray()) {
            var p = ParseProfile(e);
            if (!seen.Add(p.Id)) throw new Exception("Duplicate driving validation profile id " + p.Id);
            result.Add(p);
        }
        if (result.Count == 0) throw new Exception("Driving validation profiles JSON is empty");
        return result;
    }

    public static List<string> ParseCaseProfiles(string caseJson, int car, int track) {
        using var document = System.Text.Json.JsonDocument.Parse(caseJson);
        if (!document.RootElement.TryGetProperty("cases", out var cases) || cases.ValueKind != System.Text.Json.JsonValueKind.Array)
            throw new Exception("Driving validation case manifest has no cases array");
        foreach (var c in cases.EnumerateArray()) {
            if (c.TryGetProperty("car", out var carE) && c.TryGetProperty("track", out var trackE) && carE.GetInt32() == car && trackE.GetInt32() == track) {
                if (!c.TryGetProperty("drivingValidation", out var list) || list.ValueKind != System.Text.Json.JsonValueKind.Array || list.GetArrayLength() == 0)
                    throw new Exception("Driving validation case for car " + car + " track " + track + " has no drivingValidation profiles");
                var ids = new List<string>();
                foreach (var id in list.EnumerateArray()) ids.Add(id.GetString());
                return ids;
            }
        }
        throw new Exception("Driving validation case manifest has no case for car " + car + " track " + track);
    }

    static double Wrap(double angle) { while (angle > Math.PI) angle -= 2 * Math.PI; while (angle < -Math.PI) angle += 2 * Math.PI; return angle; }
    static string Format(double? v) { return v.HasValue ? v.Value.ToString("0.####", CultureInfo.InvariantCulture) : "null"; }

    // Metrics read dt from sample timestamps, so decimated retention stays valid. An unknown
    // metric name must throw instead of silently passing: a typo in a case file fails the run.
    public static double? ComputeMetric(string name, string phaseId, List<Sample> all, Profile profile, IReadOnlyDictionary<string, double> references) {
        var s = phaseId == "all" ? all : all.FindAll(x => x.phaseId == phaseId);
        switch (name) {
            case "duration": return s.Count < 2 ? (double?)null : s[s.Count - 1].t - s[0].t;
            case "startSpeed": return s.Count == 0 ? (double?)null : s[0].speed;
            case "endSpeed": return s.Count == 0 ? (double?)null : s[s.Count - 1].speed;
            case "endLongitudinalSpeed": return s.Count == 0 ? (double?)null : s[s.Count - 1].longitudinalSpeed;
            case "pathLength": {
                // An unobserved measurement is null, never zero: a metric over absent data must fail.
                if (s.Count == 0) return null;
                double sum = 0; for (int i = 1; i < s.Count; i++) sum += Math.Sqrt((s[i].x - s[i - 1].x) * (s[i].x - s[i - 1].x) + (s[i].z - s[i - 1].z) * (s[i].z - s[i - 1].z)); return sum;
            }
            case "trackLateralAbsMax": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) m = Math.Max(m, Math.Abs(v.trackLateral)); return m; }
            case "lateralDriftAbs": return s.Count < 2 ? (double?)null : Math.Abs(s[s.Count - 1].trackLateral - s[0].trackLateral);
            case "headingDriftAbs": { if (s.Count < 2) return null; return Math.Abs(Wrap(s[s.Count - 1].heading - s[0].heading)); }
            case "longitudinalAccelMin":
            case "longitudinalAccelMax": {
                double? best = null;
                for (int i = 1; i < s.Count; i++) {
                    var dt = s[i].t - s[i - 1].t; if (dt <= 0) continue;
                    var a = (s[i].longitudinalSpeed - s[i - 1].longitudinalSpeed) / dt; if (!double.IsFinite(a)) continue;
                    best = best == null ? a : (name == "longitudinalAccelMin" ? Math.Min(best.Value, a) : Math.Max(best.Value, a));
                }
                return best;
            }
            case "lateralAccelPeakAbs": {
                double? best = null;
                for (int i = 1; i < s.Count; i++) {
                    var dt = s[i].t - s[i - 1].t; if (dt <= 0) continue;
                    var a = Math.Abs((s[i].lateralSpeed - s[i - 1].lateralSpeed) / dt);
                    if (double.IsFinite(a)) best = Math.Max(best ?? 0, a);
                }
                return best;
            }
            case "yawRatePeakAbs": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) m = Math.Max(m, Math.Abs(v.yawRate)); return m; }
            case "yawRateEndAbs": {
                if (s.Count == 0) return null;
                var cutoff = s[s.Count - 1].t - Math.Min(.5, (s[s.Count - 1].t - s[0].t) * .2);
                double sum = 0; int n = 0;
                foreach (var v in s) if (v.t >= cutoff) { sum += Math.Abs(v.yawRate); n++; }
                return n == 0 ? (double?)null : sum / n;
            }
            case "meanYawRate": { if (s.Count == 0) return null; double sum = 0; foreach (var v in s) sum += v.yawRate; return sum / s.Count; }
            case "meanYawRateAbsMin": {
                if (phaseId != "all") return null;
                double? min = null;
                foreach (var phase in profile.Phases) {
                    if (!phase.Id.StartsWith("step_", StringComparison.Ordinal)) continue;
                    var sub = s.FindAll(x => x.phaseId == phase.Id); if (sub.Count == 0) continue;
                    double sum = 0; foreach (var v in sub) sum += v.yawRate;
                    var m = Math.Abs(sum / sub.Count);
                    min = min == null ? m : Math.Min(min.Value, m);
                }
                return min;
            }
            case "slipAnglePeakAbs": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) for (int i = 0; i < 4; i++) m = Math.Max(m, Math.Abs(v.lateralSlipAngle[i])); return m; }
            case "longitudinalSlipPeakAbs": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) for (int i = 0; i < 4; i++) m = Math.Max(m, Math.Abs(v.longitudinalSlip[i])); return m; }
            case "wheelAnglePeakAbs": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) m = Math.Max(m, Math.Abs(v.wheelAngle)); return m; }
            case "wheelAngleTargetPeakAbs": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) m = Math.Max(m, Math.Abs(v.wheelAngleTarget)); return m; }
            case "steerResponseTime": {
                if (s.Count == 0) return null;
                double? targetPeak = null; foreach (var v in s) targetPeak = Math.Max(targetPeak ?? 0, Math.Abs(v.wheelAngleTarget));
                if (targetPeak == null || targetPeak.Value <= 0.01) return null;
                foreach (var v in s) if (Math.Abs(v.wheelAngleTarget) > 0.01 && Math.Abs(v.wheelAngle) >= 0.05 * Math.Abs(v.wheelAngleTarget)) return v.t - s[0].t;
                return null;
            }
            case "steerTrackingRatio": {
                double? peak = null, target = null;
                foreach (var v in s) { peak = Math.Max(peak ?? 0, Math.Abs(v.wheelAngle)); target = Math.Max(target ?? 0, Math.Abs(v.wheelAngleTarget)); }
                return peak != null && target != null && target.Value > 0.01 ? peak / target : (double?)null;
            }
            case "steerSettleTime": {
                if (s.Count == 0) return null;
                double? targetPeak = null; foreach (var v in s) targetPeak = Math.Max(targetPeak ?? 0, Math.Abs(v.wheelAngleTarget));
                if (targetPeak == null || targetPeak.Value <= 0.01) return null;
                double medianDt = s.Count > 1 ? (s[s.Count - 1].t - s[0].t) / (s.Count - 1) : 1e-3;
                int hold = Math.Max(1, (int)Math.Ceiling(0.1 / Math.Max(medianDt, 1e-4)));
                for (int i = 0; i < s.Count; i++) {
                    bool ok = true; int end = Math.Min(s.Count, i + hold);
                    for (int j = i; j < end; j++) {
                        var tolerance = Math.Max(.05 * Math.Abs(s[j].wheelAngleTarget), .005);
                        if (Math.Abs(s[j].wheelAngle - s[j].wheelAngleTarget) > tolerance) { ok = false; break; }
                    }
                    if (ok) return s[i].t - s[0].t;
                }
                return null;
            }
            case "steerSettleRatio": {
                var settle = ComputeMetric("steerSettleTime", phaseId, s, profile, references);
                double? targetPeak = null; foreach (var v in s) targetPeak = Math.Max(targetPeak ?? 0, Math.Abs(v.wheelAngleTarget));
                if (settle == null || targetPeak == null || targetPeak.Value <= 0.01) return null;
                double rate = references != null && references.TryGetValue("steerActuatorRate", out var r) && r > 0 ? r : 3.2;
                return settle.Value / (targetPeak.Value / rate + .05);
            }
            case "groundedFraction": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (v.groundedMask == 15) n++; return (double)n / s.Count; }
            case "chassisContactSamples": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (v.chassisContacts > 0) n++; return (double)n; }
            case "nonSurfaceContactSamples": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (v.nonSurfaceContact) n++; return (double)n; }
            case "wheelBandContactSamples": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (v.wheelBandContact) n++; return (double)n; }
            case "maxSpeed": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) m = Math.Max(m, v.speed); return m; }
            case "throttleHoldSeconds": { if (s.Count == 0) return null; double sum = 0; for (int i = 0; i < s.Count; i++) if (s[i].throttle > 0.5) { if (i == 0) { if (s.Count > 1 && s[1].t > s[0].t) sum += s[1].t - s[0].t; } else sum += s[i].t - s[i - 1].t; } return sum; }
            case "raceStartedFraction": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (v.raceStarted) n++; return (double)n / s.Count; }
            case "offTrackFraction": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (!v.onDrivingSurface) n++; return (double)n / s.Count; }
            case "impactEventsDelta": return s.Count == 0 ? (double?)null : (double)(s[s.Count - 1].impactEvents - s[0].impactEvents);
            case "impactTickPeakDeltaV": {
                // Per-tick velocity jump measured inside the contact window (either sample touching a
                // non-drive surface): the raw material for the force estimate, from recorded samples.
                double? best = null;
                for (int i = 1; i < s.Count; i++) {
                    if (!s[i].nonSurfaceContact && !s[i - 1].nonSurfaceContact) continue;
                    var dt = s[i].t - s[i - 1].t; if (dt <= 0) continue;
                    var dl = s[i].longitudinalSpeed - s[i - 1].longitudinalSpeed;
                    var da = s[i].lateralSpeed - s[i - 1].lateralSpeed;
                    var dv = Math.Sqrt(dl * dl + da * da);
                    if (double.IsFinite(dv)) best = Math.Max(best ?? 0, dv);
                }
                return best;
            }
            case "impactContactSeconds": {
                if (s.Count == 0) return null;
                double sum = 0;
                for (int i = 0; i < s.Count; i++) {
                    if (!s[i].nonSurfaceContact) continue;
                    if (i == 0) { if (s.Count > 1 && s[1].t > s[0].t) sum += s[1].t - s[0].t; }
                    else sum += s[i].t - s[i - 1].t;
                }
                return sum;
            }
            case "impactImpulseEstimate": {
                var rise = ComputeMetric("maxDeltaVRise", phaseId, s, profile, references);
                if (rise == null) return null;
                if (references == null || !references.TryGetValue("vehicleMass", out var massA)) throw new Exception("Metric impactImpulseEstimate needs the vehicleMass reference");
                return massA * rise.Value;
            }
            case "impactPeakForceEstimate": {
                // vehicleMass * (per-tick velocity jump) / dt at that jump: an estimate derived from
                // recorded samples at sample rate; the true contact force profile is not observed.
                var jump = ComputeMetric("impactTickPeakDeltaV", phaseId, s, profile, references);
                if (jump == null) return null;
                if (references == null || !references.TryGetValue("vehicleMass", out var massB)) throw new Exception("Metric impactPeakForceEstimate needs the vehicleMass reference");
                for (int i = 1; i < s.Count; i++) {
                    if (!s[i].nonSurfaceContact && !s[i - 1].nonSurfaceContact) continue;
                    var dt = s[i].t - s[i - 1].t; if (dt <= 0) continue;
                    var dl = s[i].longitudinalSpeed - s[i - 1].longitudinalSpeed;
                    var da = s[i].lateralSpeed - s[i - 1].lateralSpeed;
                    var dv = Math.Sqrt(dl * dl + da * da);
                    if (double.IsFinite(dv) && dv.Equals(jump.Value)) return massB * dv / dt;
                }
                return null;
            }
            case "maxDeltaVRise": { if (s.Count == 0) return null; double m = 0; foreach (var v in s) m = Math.Max(m, v.maxDeltaV); return m - s[0].maxDeltaV; }
            case "positionJumpMax": { double m = 0; for (int i = 1; i < s.Count; i++) m = Math.Max(m, Math.Sqrt((s[i].x - s[i - 1].x) * (s[i].x - s[i - 1].x) + (s[i].z - s[i - 1].z) * (s[i].z - s[i - 1].z))); return m; }
            case "suspensionCompressionMin": {
                // Only grounded wheels carry a real spring length; non-supported wheels report the
                // airborne sentinel and must not pollute the travel measurement (clean-lap evidence:
                // sentinel 0.33 observed on airborne wheels, run 41799538756).
                double m = double.PositiveInfinity;
                foreach (var v in s) for (int i = 0; i < 4; i++) if ((v.groundedMask & (1 << i)) != 0) m = Math.Min(m, v.springLengths[i]);
                return double.IsFinite(m) ? (double)m : (double?)null;
            }
            case "suspensionExtensionMax": {
                double m = double.NegativeInfinity;
                foreach (var v in s) for (int i = 0; i < 4; i++) if ((v.groundedMask & (1 << i)) != 0) m = Math.Max(m, v.springLengths[i]);
                return double.IsFinite(m) ? (double)m : (double?)null;
            }
            case "uprightMinCosine": { if (s.Count == 0) return null; double m = 1; foreach (var v in s) m = Math.Min(m, v.upY); return m; }
            case "speedRiseTime": { if (s.Count == 0) return null; var start = s[0].speed; foreach (var v in s) if (v.speed >= start + .5) return v.t - s[0].t; return null; }
            case "speedDropTime": { if (s.Count == 0) return null; var start = s[0].speed; foreach (var v in s) if (v.speed <= start - .5) return v.t - s[0].t; return null; }
            // Recovery-window metrics always read the FULL stream regardless of the declared
            // scope: windows span phases (the impact lands in one phase, the drive-away lives
            // in later ones), so a phase-filtered slice would undercount. Declare them :all.
            case "recoveryWindowCount": { var w = RecoveryWindowsOf(profile, all); return w == null ? null : (double?)w.Count; }
            case "recoveryRecoveredFraction": {
                var w = RecoveryWindowsOf(profile, all); if (w == null || w.Count == 0) return null;
                int r = 0; foreach (var x in w) if (x.Recovered) r++; return (double)r / w.Count;
            }
            case "recoveryMotionTimeMax":
            case "recoveryMotionTimeMean": {
                var w = RecoveryWindowsOf(profile, all);
                double max = 0, sum = 0; int n = 0;
                if (w != null) foreach (var x in w) if (x.motionTime.HasValue) { var v = x.motionTime.Value; if (n == 0 || v > max) max = v; sum += v; n++; }
                if (n == 0) return null;
                return name == "recoveryMotionTimeMax" ? max : sum / n;
            }
            case "recoveryPathLengthMax": {
                var w = RecoveryWindowsOf(profile, all); if (w == null || w.Count == 0) return null;
                double m = 0; foreach (var x in w) m = Math.Max(m, x.pathLength); return m;
            }
            case "recoveryOffTrackFractionMax": {
                var w = RecoveryWindowsOf(profile, all); if (w == null || w.Count == 0) return null;
                double m = 0; foreach (var x in w) m = Math.Max(m, x.offTrackFraction); return m;
            }
            case "recoveryThrottleFractionMean":
            case "recoveryBrakeFractionMean": {
                var w = RecoveryWindowsOf(profile, all); if (w == null || w.Count == 0) return null;
                double sum = 0; foreach (var x in w) sum += name == "recoveryThrottleFractionMean" ? x.throttleFraction : x.brakeFraction;
                return sum / w.Count;
            }
            case "recoverySteerPeakAbsMax": {
                var w = RecoveryWindowsOf(profile, all); if (w == null || w.Count == 0) return null;
                double m = 0; foreach (var x in w) m = Math.Max(m, x.steerPeakAbs); return m;
            }
            // Contact-archetype family (guard band vs body panel): null unless the profile sets
            // setup.recordContactArchetypes (fail-closed, never a silent zero). A tick touching
            // BOTH the wheel band and a body panel is mixed and feeds both signatures; the
            // mixedContactSamples count makes that ambiguity measurable instead of hidden.
            case "bodyPanelContactSamples": {
                if (profile == null || !profile.RecordContactArchetypes) return null;
                int n = 0; foreach (var v in s) if (v.bodyPanelContact) n++; return (double)n;
            }
            case "mixedContactSamples": {
                if (profile == null || !profile.RecordContactArchetypes) return null;
                int n = 0; foreach (var v in s) if (v.wheelBandContact && v.bodyPanelContact) n++; return (double)n;
            }
            case "wheelBandContactSeconds":
            case "bodyPanelContactSeconds": {
                if (profile == null || !profile.RecordContactArchetypes) return null;
                if (s.Count == 0) return null;
                bool wantBand = name == "wheelBandContactSeconds";
                double sum = 0;
                for (int i = 0; i < s.Count; i++) {
                    bool hit = wantBand ? s[i].wheelBandContact : s[i].bodyPanelContact;
                    if (!hit) continue;
                    if (i == 0) { if (s.Count > 1 && s[1].t > s[0].t) sum += s[1].t - s[0].t; }
                    else sum += s[i].t - s[i - 1].t;
                }
                return sum;
            }
            case "wheelBandTickPeakDeltaV":
            case "bodyPanelTickPeakDeltaV": {
                if (profile == null || !profile.RecordContactArchetypes) return null;
                double? best = null;
                for (int i = 1; i < s.Count; i++) {
                    bool hit = name == "wheelBandTickPeakDeltaV"
                        ? (s[i].wheelBandContact || s[i - 1].wheelBandContact)
                        : (s[i].bodyPanelContact || s[i - 1].bodyPanelContact);
                    if (!hit) continue;
                    var dt = s[i].t - s[i - 1].t; if (dt <= 0) continue;
                    var dl = s[i].longitudinalSpeed - s[i - 1].longitudinalSpeed;
                    var da = s[i].lateralSpeed - s[i - 1].lateralSpeed;
                    var dv = Math.Sqrt(dl * dl + da * da);
                    if (double.IsFinite(dv)) best = Math.Max(best ?? 0, dv);
                }
                return best;
            }
            default: throw new Exception("Driving validation metric " + name + " is not implemented (fail closed)");
        }
    }

    public static AssertionResult Evaluate(Assertion a, List<Sample> all, Profile profile, IReadOnlyDictionary<string, double> references) {
        var colon = a.Metric.LastIndexOf(':');
        var name = a.Metric.Substring(0, colon);
        var scope = a.Metric.Substring(colon + 1);
        var raw = ComputeMetric(name, scope, all, profile, references);
        double? actual = raw as double?;
        string expected; bool passed;
        if (a.Op == "between") {
            expected = "[" + Format(a.Min) + "," + Format(a.Max) + "]";
            passed = actual.HasValue && actual.Value >= a.Min && actual.Value <= a.Max;
        } else if (a.Op == "betweenReference") {
            if (references == null || !references.TryGetValue(a.Reference, out var reference))
                throw new Exception("Driving validation assertion " + a.Id + " references unavailable anchor " + a.Reference);
            double lo = a.FactorMin * reference, hi = a.FactorMax * reference;
            expected = "[" + Format(lo) + "," + Format(hi) + "] (" + a.FactorMin + ".." + a.FactorMax + " x " + a.Reference + "=" + Format(reference) + ")";
            passed = actual.HasValue && actual.Value >= lo && actual.Value <= hi;
        } else if (a.Op == "lt") { expected = "< " + Format(a.Value); passed = actual.HasValue && actual.Value < a.Value; }
        else if (a.Op == "lte") { expected = "<= " + Format(a.Value); passed = actual.HasValue && actual.Value <= a.Value; }
        else if (a.Op == "gt") { expected = "> " + Format(a.Value); passed = actual.HasValue && actual.Value > a.Value; }
        else if (a.Op == "gte") { expected = ">= " + Format(a.Value); passed = actual.HasValue && actual.Value >= a.Value; }
        else throw new Exception("Driving validation assertion " + a.Id + " uses unknown op " + a.Op);
        if (!actual.HasValue) { passed = false; expected = "metric not observed; expected " + expected; }
        return new AssertionResult { Id = a.Id, Metric = a.Metric, Op = a.Op, Expected = expected, Actual = actual, Passed = passed };
    }

    // One profile's state machine: consumes samples of the live run, emits the scripted
    // command for the next physics tick, records evidence, evaluates assertions once done.
    public sealed class Run {
        public readonly Profile Profile;
        readonly IReadOnlyDictionary<string, double> references;
        readonly List<Sample> timeline = new List<Sample>();
        int phaseIndex, ticks, storedCount;
        double phaseStart;
        double impactBaseline; bool hasBaseline;
        public bool Completed; public bool Passed; public string AbortReason;
        public Command Current;
        public List<Sample> Timeline => timeline;
        public List<AssertionResult> Results = new List<AssertionResult>();
        public string FirstFailure { get; private set; }
        public bool Truncated => storedCount >= Profile.MaxSamples;

        public Run(Profile profile, IReadOnlyDictionary<string, double> references) { Profile = profile; this.references = references; }

        public void Start() {
            phaseIndex = 0; phaseStart = 0;
            var p = Profile.Phases[0];
            Current = new Command { Throttle = p.Throttle, Brake = p.Brake, Steer = p.Steer, Handbrake = p.Handbrake, Reverse = p.Reverse };
        }

        public void Abort(string reason) {
            if (Completed) return;
            AbortReason = reason; Completed = true; Current = Command.Neutral;
            if (FirstFailure == null) FirstFailure = reason;
        }

        public void Step(Sample s) {
            if (Completed) return;
            var phase = Profile.Phases[phaseIndex];
            s.phaseId = phase.Id;
            ticks++;
            if (storedCount < Profile.MaxSamples && ticks % Profile.SampleEveryTicks == 0) {
                timeline.Add(s); storedCount++;
                if (!hasBaseline) { hasBaseline = true; impactBaseline = s.impactEvents; }
            }
            double inPhase = s.t - phaseStart;
            bool minOk = !phase.MinSeconds.HasValue || inPhase >= phase.MinSeconds.Value;
            bool done = false;
            if (phase.ForSeconds.HasValue && inPhase >= phase.ForSeconds.Value) done = true;
            if (!done && phase.UntilSpeedAtLeast.HasValue && minOk && s.speed >= phase.UntilSpeedAtLeast.Value) done = true;
            if (!done && phase.UntilSpeedBelow.HasValue && minOk && s.speed < phase.UntilSpeedBelow.Value) done = true;
            if (!done && phase.UntilChassisContactsAtLeast.HasValue && minOk && s.chassisContacts >= phase.UntilChassisContactsAtLeast.Value) done = true;
            if (!done && phase.UntilNonSurfaceContact && minOk && s.nonSurfaceContact) done = true;
            if (!done && phase.UntilRaceStarted && minOk && s.raceStarted) done = true;
            if (!done && phase.UntilImpactEventsAtLeast.HasValue && minOk && hasBaseline && s.impactEvents - impactBaseline >= phase.UntilImpactEventsAtLeast.Value) done = true;
            if (!done && phase.UntilOffTrackObserved && minOk && !s.onDrivingSurface) done = true;
            if (!done && phase.MaxSeconds.HasValue && inPhase > phase.MaxSeconds.Value) { Abort("phase " + phase.Id + " exceeded maxSeconds without reaching its condition"); return; }
            if (!done) return;
            phaseIndex++;
            if (phaseIndex >= Profile.Phases.Count) {
                Completed = true; Current = Command.Neutral;
                EvaluateAssertions();
            } else {
                phaseStart = s.t;
                var p = Profile.Phases[phaseIndex];
                Current = new Command { Throttle = p.Throttle, Brake = p.Brake, Steer = p.Steer, Handbrake = p.Handbrake, Reverse = p.Reverse };
            }
        }

        public void EvaluateAssertions() {
            if (Results.Count > 0) return;
            bool all = true;
            foreach (var a in Profile.Assertions) {
                if (Completed && AbortReason != null && !CoversPhase(a.Metric)) {
                    Results.Add(new AssertionResult { Id = a.Id, Metric = a.Metric, Op = a.Op, Expected = "phase never completed: " + AbortReason, Passed = false, Actual = null });
                    all = false; continue;
                }
                var result = Evaluate(a, timeline, Profile, references);
                Results.Add(result);
                if (!result.Passed) { all = false; if (FirstFailure == null) FirstFailure = a.Id + " (" + a.Metric + " actual " + Format(result.Actual) + ", expected " + result.Expected + ")"; }
            }
            Passed = all;
        }

        bool CoversPhase(string metric) {
            var colon = metric.LastIndexOf(':'); var scope = metric.Substring(colon + 1);
            if (scope == "all") return timeline.Count > 0;
            return timeline.FindIndex(x => x.phaseId == scope) >= 0;
        }
    }

    // Sequences the profiles of one case: runs them one at a time, requests a mechanical
    // reset between profiles, aborts the remainder when one fails or aborts, and accumulates
    // the per-profile results for the final report.
    public sealed class Runner {
        readonly List<Run> runs;
        readonly IReadOnlyDictionary<string, double> references;
        int index = -1;
        bool started, needsReset;
        double clock;
        public bool Completed { get; private set; }
        public bool AllPassed { get; private set; }
        public int FailedRuns { get; private set; }
        public string FirstFailure { get; private set; }
        public string AbortReason { get; private set; }
        public bool NeedsReset => needsReset;
        public Command Current { get; private set; }
        public IReadOnlyDictionary<string, double> References => references;
        public IReadOnlyList<Run> Runs => runs;
        public double Clock => clock;

        public Runner(List<Run> runs, IReadOnlyDictionary<string, double> references) { this.runs = runs; this.references = references; AllPassed = true; }

        public void Abort(string reason) {
            if (Completed) return;
            AbortReason = reason;
            if (index >= 0 && index < runs.Count) runs[index].Abort(reason);
            for (int i = index + 1; i < runs.Count; i++) { runs[i].Abort("not reached: " + reason); FailedRuns++; }
            Completed = true; Current = Command.Neutral;
            if (AllPassed) { AllPassed = false; if (FirstFailure == null) FirstFailure = reason; }
        }

        public void ConfirmReset() { needsReset = false; }

        // Called every physics tick with a fresh sample of the state produced by the previous
        // command; returns/holds the command to apply next. The host applies Current itself.
        public void Advance(Sample s, double dt) {
            if (Completed || needsReset) return;
            if (!started) { started = true; index = 0; runs[0].Start(); Current = runs[0].Current; }
            s.t = clock; clock += dt;
            var run = runs[index];
            run.Step(s);
            if (run.Completed) {
                if (run.AbortReason != null) { FailedRuns++; if (AllPassed) { AllPassed = false; if (FirstFailure == null) FirstFailure = run.Profile.Id + ": " + run.AbortReason; } }
                else if (!run.Passed) { FailedRuns++; if (AllPassed) { AllPassed = false; if (FirstFailure == null) FirstFailure = run.Profile.Id + ": " + run.FirstFailure; } }
                if (run.AbortReason != null) { Abort("stopped after " + run.Profile.Id + " " + run.AbortReason); return; }
                index++;
                if (index >= runs.Count) { Completed = true; Current = Command.Neutral; }
                else { runs[index].Start(); Current = runs[index].Current; needsReset = true; }
            } else Current = run.Current;
        }
    }
}
}
