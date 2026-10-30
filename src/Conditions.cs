using Godot;
namespace MySim {
// Time-of-day and weather presets (menu settings "tod": 0 noon, 1 morning, 2 sunset, 3 night; "wet": 0 clear, 1 overcast, 2 rain).
public static class Conditions {
 public static readonly string[] TodNames = { "Noon", "Morning", "Sunset", "Night" };
 static System.Text.Json.JsonDocument catalogue;static System.Text.Json.JsonElement Profiles {get{catalogue??=System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString("res://data/weather_profiles.json"));return catalogue.RootElement.GetProperty("profiles");}}
 public static string[] WetNames {get{var names=new System.Collections.Generic.List<string>();foreach(var p in Profiles.EnumerateArray())names.Add(p.GetProperty("name").GetString());return names.ToArray();}}
 static System.Text.Json.JsonElement Profile=>Profiles[Wet];
 static float Value(string key)=>Profile.GetProperty(key).GetSingle();
 static Color Tint(string key){var v=Profile.GetProperty(key);return new Color(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle());}
 public static bool Raining=>Value("rainAmount")>0;
 public static bool IsNight=>Tod==3;
 public static MySimulator.Rebuild.SurfaceWeather BeginWeather()=>new MySimulator.Rebuild.SurfaceWeather(Value("initialWater"));
 public static void Advance(MySimulator.Rebuild.SurfaceWeather state,float seconds)=>state.Step(seconds,Value("rainRate"),Value("dryingRate"));
 public static int Tod => Mathf.Clamp(System.Environment.GetEnvironmentVariable("MYSIM_TOD") != null ? int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_TOD")) : Settings.Get("tod", 0), 0, 3);
 public static int Wet => Mathf.Clamp(System.Environment.GetEnvironmentVariable("MYSIM_WET") != null ? int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_WET")) : Settings.Get("wet", 0), 0, Profiles.GetArrayLength()-1);
 public static float WetnessValue => Value("initialWater");
 public static void Apply(Godot.Environment env, DirectionalLight3D sun, ProceduralSkyMaterial sky) {
  int tod = Tod, wet = Wet;
  Color amb = new Color(0.62f, 0.66f, 0.72f), fog = new Color(0.63f, 0.70f, 0.76f), top = new Color(0.38f, 0.55f, 0.82f), hor = new Color(0.72f, 0.80f, 0.88f), sunCol = new Color(1f, 0.97f, 0.92f); float sunE = 1.7f, ambE = 1.0f, expo = 1.0f; Vector3 rot = new Vector3(-48, -35, 0);
  if (tod == 1) { rot = new Vector3(-20, -60, 0); sunCol = new Color(1f, 0.86f, 0.68f); sunE = 1.5f; amb = new Color(0.56f, 0.6f, 0.7f); fog = new Color(0.82f, 0.78f, 0.74f); top = new Color(0.42f, 0.56f, 0.8f); hor = new Color(0.95f, 0.82f, 0.68f); }
  else if (tod == 2) { rot = new Vector3(-9, -120, 0); sunCol = new Color(1f, 0.58f, 0.32f); sunE = 1.3f; amb = new Color(0.48f, 0.42f, 0.5f); fog = new Color(0.86f, 0.62f, 0.45f); top = new Color(0.28f, 0.34f, 0.58f); hor = new Color(1f, 0.6f, 0.34f); }
  else if (tod == 3) { rot = new Vector3(-38, -35, 0); sunCol = new Color(0.6f, 0.7f, 1f); sunE = 0.08f; amb = new Color(0.3f, 0.35f, 0.55f); ambE = 0.18f; expo = 1.0f; fog = new Color(0.05f, 0.07f, 0.12f); top = new Color(0.01f, 0.02f, 0.06f); hor = new Color(0.05f, 0.07f, 0.13f); }
  float fogD = 0.00012f;
  sunE*=Value("sunScale");fogD=Value("fogDensity");
  if(wet>0){float blend=IsNight?.2f:.7f;top=top.Lerp(Tint("skyTint"),blend);hor=hor.Lerp(Tint("horizonTint"),blend);fog=fog.Lerp(Tint("horizonTint"),blend);}
  env.AmbientLightColor = amb; env.AmbientLightEnergy = ambE; env.FogLightColor = fog; env.FogDensity = fogD; env.TonemapExposure = expo;
  sun.RotationDegrees = rot; sun.LightColor = sunCol; sun.LightEnergy = sunE;
  sky.SkyTopColor = top; sky.SkyHorizonColor = hor; sky.GroundHorizonColor = hor; sky.GroundBottomColor = hor.Darkened(0.3f);
 }
 public static void AddRain(Node3D parent) {
  var mat = new ParticleProcessMaterial { EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(28, 0.2f, 28), Direction = new Vector3(0.08f, -1, 0), Spread = 2f, InitialVelocityMin = 24f, InitialVelocityMax = 30f, Gravity = new Vector3(0, -6, 0) };
  var mesh = new BoxMesh { Size = new Vector3(0.012f, 0.55f, 0.012f), Material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.8f, 0.85f, 0.95f, 0.45f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha } };
  parent.AddChild(new GpuParticles3D { Amount = (int)Value("rainAmount"), Lifetime = 0.9, ProcessMaterial = mat, DrawPass1 = mesh, Position = new Vector3(0, 14, 0), VisibilityAabb = new Aabb(new Vector3(-40, -20, -40), new Vector3(80, 40, 80)), LocalCoords = false });
 }
}}
