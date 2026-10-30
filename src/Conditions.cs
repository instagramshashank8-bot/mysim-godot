using Godot;
namespace MySim {
// Time-of-day and weather presets (menu settings "tod": 0 noon, 1 morning, 2 sunset, 3 night; "wet": 0 clear, 1 overcast, 2 rain).
public static class Conditions {
 static System.Text.Json.JsonDocument lightingCatalogue;
 static System.Text.Json.JsonElement LightingProfiles {get{lightingCatalogue??=System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString("res://data/lighting_profiles.json"));return lightingCatalogue.RootElement.GetProperty("profiles");}}
 static System.Text.Json.JsonElement Lighting=>LightingProfiles[Tod];
 public static string[] TodNames {get{var names=new System.Collections.Generic.List<string>();foreach(var p in LightingProfiles.EnumerateArray())names.Add(p.GetProperty("name").GetString());return names.ToArray();}}
 static Color LightTint(string key){var v=Lighting.GetProperty(key);return new Color(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle());}
 static System.Text.Json.JsonDocument catalogue;static System.Text.Json.JsonElement Profiles {get{catalogue??=System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString("res://data/weather_profiles.json"));return catalogue.RootElement.GetProperty("profiles");}}
 public static string[] WetNames {get{var names=new System.Collections.Generic.List<string>();foreach(var p in Profiles.EnumerateArray())names.Add(p.GetProperty("name").GetString());return names.ToArray();}}
 static System.Text.Json.JsonElement Profile=>Profiles[Wet];
 static float Value(string key)=>Profile.GetProperty(key).GetSingle();
 static Color Tint(string key){var v=Profile.GetProperty(key);return new Color(v[0].GetSingle(),v[1].GetSingle(),v[2].GetSingle());}
 public static bool Raining=>Value("rainAmount")>0;
 public static bool IsNight=>Lighting.GetProperty("night").GetBoolean();
 public static MySimulator.Rebuild.SurfaceWeather BeginWeather()=>new MySimulator.Rebuild.SurfaceWeather(Value("initialWater"));
 public static void Advance(MySimulator.Rebuild.SurfaceWeather state,float seconds)=>state.Step(seconds,Value("rainRate"),Value("dryingRate"));
 public static int Tod => Mathf.Clamp(System.Environment.GetEnvironmentVariable("MYSIM_TOD") != null ? int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_TOD")) : Settings.Get("tod", 0), 0, LightingProfiles.GetArrayLength()-1);
 public static int Wet => Mathf.Clamp(System.Environment.GetEnvironmentVariable("MYSIM_WET") != null ? int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_WET")) : Settings.Get("wet", 0), 0, Profiles.GetArrayLength()-1);
 public static float WetnessValue => Value("initialWater");
 public static void Apply(Godot.Environment env, DirectionalLight3D sun, ProceduralSkyMaterial sky) {
  int tod = Tod, wet = Wet;
  Color amb=LightTint("ambientColor"),fog=LightTint("fogColor"),top=LightTint("skyTop"),hor=LightTint("horizon"),sunCol=LightTint("sunColor");
  float sunE=Lighting.GetProperty("sunEnergy").GetSingle(),ambE=Lighting.GetProperty("ambientEnergy").GetSingle(),expo=Lighting.GetProperty("exposure").GetSingle();
  var rotation=Lighting.GetProperty("rotation");Vector3 rot=new Vector3(rotation[0].GetSingle(),rotation[1].GetSingle(),rotation[2].GetSingle());
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
