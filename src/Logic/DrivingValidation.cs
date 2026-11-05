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
    public static readonly string[] KnownReferences = { "brakeStop60Metres", "accel0To60Seconds", "steerActuatorRate" };
    public static readonly string[] KnownMetrics = {
        "pathLength","duration","startSpeed","endSpeed","endLongitudinalSpeed","speedRiseTime","speedDropTime",
        "trackLateralAbsMax","lateralDriftAbs","headingDriftAbs","longitudinalAccelMin","longitudinalAccelMax",
        "lateralAccelPeakAbs","yawRatePeakAbs","yawRateEndAbs","meanYawRate","meanYawRateAbsMin",
        "slipAnglePeakAbs","longitudinalSlipPeakAbs","wheelAnglePeakAbs","wheelAngleTargetPeakAbs",
        "steerResponseTime","steerTrackingRatio","steerSettleTime","steerSettleRatio","groundedFraction","chassisContactSamples",
        "nonSurfaceContactSamples","offTrackFraction","impactEventsDelta","maxDeltaVRise","positionJumpMax","suspensionCompressionMin",
        "suspensionExtensionMax","uprightMinCosine"
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
        public bool RequireRotationalTyres, RequireDamage;
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
        public bool UntilNonSurfaceContact, UntilOffTrackObserved;
        public bool Terminates => ForSeconds.HasValue || UntilSpeedAtLeast.HasValue || UntilSpeedBelow.HasValue || UntilChassisContactsAtLeast.HasValue || UntilImpactEventsAtLeast.HasValue || UntilNonSurfaceContact || UntilOffTrackObserved;
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
            case "offTrackFraction": { if (s.Count == 0) return null; int n = 0; foreach (var v in s) if (!v.onDrivingSurface) n++; return (double)n / s.Count; }
            case "impactEventsDelta": return s.Count == 0 ? (double?)null : (double)(s[s.Count - 1].impactEvents - s[0].impactEvents);
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
