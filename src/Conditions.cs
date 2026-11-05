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
 static int wetReflectLogged;
 public static void ConfigureWetGround(ShaderMaterial material){
  var profile=Profiles;profile=catalogue.RootElement.GetProperty("surfaceVisual");
  // One controlled wet-visibility candidate: MYSIM_WET_REFLECT=<m> scales the WET specular mix-end only
  // (a stronger reflection on the wet road); the dry end (0.04) and the darkening/roughness stay authored,
  // so a DRY render is unaffected by construction (the mix weight is zero at water=0). Default unset = exact.
  float reflect=EnvFloat("MYSIM_WET_REFLECT",1f);
  foreach(var key in new[]{"wet_darkening","wet_roughness","wet_specular"}){
   float value=profile.GetProperty(key).GetSingle();
   if(key=="wet_specular")value*=reflect;
   material.SetShaderParameter(key,value);
  }
  if(reflect!=1f&&wetReflectLogged++==0)GD.Print("WET_REFLECT m=",reflect," wet_specular→",(profile.GetProperty("wet_specular").GetSingle()*reflect).ToString("0.000"));
 }
 static float EnvFloat(string key,float fallback)=>float.TryParse(System.Environment.GetEnvironmentVariable(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float parsed)?parsed:fallback;
 public static void Apply(Godot.Environment env, DirectionalLight3D sun, ProceduralSkyMaterial sky) {
  RenderingServer.GlobalShaderParameterSet("mysim_surface_water",WetnessValue);
  int tod = Tod, wet = Wet;
  Color amb=LightTint("ambientColor"),fog=LightTint("fogColor"),top=LightTint("skyTop"),hor=LightTint("horizon"),sunCol=LightTint("sunColor");
  float sunE=Lighting.GetProperty("sunEnergy").GetSingle(),ambE=Lighting.GetProperty("ambientEnergy").GetSingle(),expo=Lighting.GetProperty("exposure").GetSingle();
  var rotation=Lighting.GetProperty("rotation");Vector3 rot=new Vector3(rotation[0].GetSingle(),rotation[1].GetSingle(),rotation[2].GetSingle());
  float fogD = 0.00012f;
  sunE*=Value("sunScale");fogD=Value("fogDensity");
  // Night-only, independently flag-gated candidates (default unset = exact current behaviour).
  // Ambient lift keeps the profile's night colour (bluish, not white) and only raises its energy;
  // exposure applies a measured multiplier to the profile's tonemap exposure. Data file untouched.
  if(IsNight){
   float ambientLift=EnvFloat("MYSIM_NIGHT_AMBIENT_LIFT",1f),exposureGain=EnvFloat("MYSIM_NIGHT_EXPOSURE",1f);
   if(ambientLift!=1f||exposureGain!=1f){ambE*=ambientLift;expo*=exposureGain;GD.Print("NIGHT_LIFT ambientEnergy=",ambE," exposure=",expo," ambientLift=",ambientLift," exposureGain=",exposureGain);}
  }
  if(wet>0){float blend=IsNight?.2f:.7f;top=top.Lerp(Tint("skyTint"),blend);hor=hor.Lerp(Tint("horizonTint"),blend);fog=fog.Lerp(Tint("horizonTint"),blend);}
  env.AmbientLightColor = amb; env.AmbientLightEnergy = ambE; env.FogLightColor = fog; env.FogDensity = fogD; env.TonemapExposure = expo;
  sun.RotationDegrees = rot; sun.LightColor = sunCol; sun.LightEnergy = sunE;
  sky.SkyTopColor = top; sky.SkyHorizonColor = hor; sky.GroundHorizonColor = hor; sky.GroundBottomColor = hor.Darkened(0.3f);
 }
 public static void AddRain(Node3D parent) {int amount=(int)Value("rainAmount");if(amount<=0)return;if(System.Environment.GetEnvironmentVariable("MYSIM_RAIN_VISIBILITY_CANDIDATE")=="1")parent.AddChild(new RainVisibilityCandidate{Amount=amount});else parent.AddChild(new RainVolume {Amount=amount});}
 public static System.Text.Json.JsonElement RainVisual=>catalogue.RootElement.GetProperty("rainVisual");
}}
