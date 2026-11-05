using Godot;
namespace MySim {
// Persistent settings (user://settings.cfg): last track/car/laps/camera, audio mute (default muted as requested).
public static class Settings {
 const string P = "user://settings.cfg"; public const int SchemaVersion = 2; static ConfigFile cf = new ConfigFile(); static bool loaded;
 static int SafeInt(Variant v) { try { return v.VariantType == Variant.Type.Int ? v.As<int>() : 0; } catch { return 0; } } // Malformed marker/values never crash: non-int or unreadable → treated as absent/legacy, disk untouched.
 // Typed fallback policy (tested by the settings_persist fixture): a stored value is returned ONLY when its Variant type matches the requested type family — int/long↔Int, bool↔Bool, string↔String, double/float↔(Int|Float); anything else (including string→int coercion, which Godot silently converts to 0) returns the EXACT typed default and never rewrites the disk.
 static T TypedValue<T>(Variant v, T def) {
  var t = typeof(T);
  if (t == typeof(int)) return (T)(object)(v.VariantType == Variant.Type.Int ? v.As<int>() : def);
  if (t == typeof(long)) return (T)(object)(v.VariantType == Variant.Type.Int ? v.As<long>() : def);
  if (t == typeof(bool)) return (T)(object)(v.VariantType == Variant.Type.Bool ? v.As<bool>() : def);
  if (t == typeof(string)) return (T)(object)(v.VariantType == Variant.Type.String ? v.As<string>() : def);
  if (t == typeof(double)) return (T)(object)(v.VariantType == Variant.Type.Int || v.VariantType == Variant.Type.Float ? v.As<double>() : def);
  if (t == typeof(float)) return (T)(object)(v.VariantType == Variant.Type.Int || v.VariantType == Variant.Type.Float ? (float)v.As<double>() : def);
  return def;
 }
 static void Load() { if (loaded) return; loaded = true;
  if (cf.Load(P) != Error.Ok) { cf = new ConfigFile(); return; } // Unloadable file: fresh defaults in memory; nothing was recoverable from it; disk untouched until the next successful save.
  // Valid file WITHOUT an int schema_version = valid legacy state: PRESERVED in place; the marker is stamped in memory only and persisted by the next Set. Never erase valid legacy settings merely because the marker is missing; a FUTURE marker (>0) is kept as-is.
  if (SafeInt(cf.GetValue("game", "schema_version", 0)) == 0) cf.SetValue("game", "schema_version", SchemaVersion);
 }
 public static T Get<[MustBeVariant] T>(string key, T def) { Load(); return TypedValue<T>(cf.GetValue("game", key, Variant.From(def)), def); } // Malformed stored type: typed default returned, file never rewritten.
 public static bool Set<[MustBeVariant] T>(string key, T v) { Load(); cf.SetValue("game", key, Variant.From(v)); if (SafeInt(cf.GetValue("game", "schema_version", 0)) == 0) cf.SetValue("game", "schema_version", SchemaVersion);
  // Crash safety: the previous file is NEVER overwritten except by the atomic rename that fully replaces it. On ANY temp-save or rename failure the old file stays intact, the failure is reported and the sibling tmp copy is kept for manual recovery.
  string live = ProjectSettings.GlobalizePath(P);
  string tmp = live + ".tmp"; // Absolute same-directory sibling of the live file: the rename lands on the same volume and stays atomic, never a cross-volume copy.
  Error tmpSave = cf.Save(tmp);
  if (tmpSave != Error.Ok) { GD.PushError("SETTINGS_SAVE tmp=" + tmpSave + " old file kept, nothing replaced"); return false; }
  using var d = Godot.DirAccess.Open(System.IO.Path.GetDirectoryName(live));
  if (d == null) { GD.PushError("SETTINGS_SAVE dir_open_failed old file kept; recovery copy=" + tmp); return false; }
  string target = System.Environment.GetEnvironmentVariable("MYSIM_PERSIST_FAULT") == "rename" // Test-only deterministic fault seam: real failed rename syscall, env-gated, default absent in normal play.
   ? System.IO.Path.Combine(System.IO.Path.GetDirectoryName(live)!, "no-such-fault-dir", "blocked") : live;
  Error rename = d.Rename(tmp, target);
  if (rename != Error.Ok) { GD.PushError("SETTINGS_SAVE rename=" + rename + " old file kept; recovery copy=" + tmp); return false; }
  return true;
 }
 public static void ApplyAudio() { bool mute = Get("mute", true); AudioServer.SetBusMute(0, mute); }
 // Desktop defaults to physical inputs; an explicit saved choice also wins on touch hardware.
 public static bool ScreenControlsEnabled {
  get { string forced=System.Environment.GetEnvironmentVariable("MYSIM_TOUCH");return forced=="1"?true:forced=="0"?false:Get("screen_controls",DisplayServer.IsTouchscreenAvailable()); }
 }
}}
