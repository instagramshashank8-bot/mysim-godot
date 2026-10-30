using Godot;
using System.Collections.Generic;
namespace MySim {
// Exact Unity material assignment: prefab renderer (by node name) -> .mat guid -> texture/colour/alpha settings, parsed offline into assets/unity_map.json.
public static class UnityMaterialBinder {
 static Godot.Collections.Dictionary map;
 static readonly Dictionary<string,bool> alphaCache=new();
 static bool HasAlpha(Texture2D texture) {
  if(texture==null)return false;string path=texture.ResourcePath;
  if(alphaCache.TryGetValue(path,out bool alpha))return alpha;
  var image=texture.GetImage();alpha=image!=null&&image.DetectAlpha()!=Image.AlphaMode.None;
  alphaCache[path]=alpha;return alpha;
 }

 static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();
 static readonly Shader GroundShader = new Shader { Code = @"shader_type spatial; render_mode cull_disabled; global uniform float mysim_surface_water; uniform float wet_darkening = 0.12; uniform float wet_roughness = 0.18; uniform float wet_specular = 0.45;
uniform sampler2D tex : source_color, repeat_enable, filter_linear_mipmap_anisotropic; uniform vec4 tint : source_color = vec4(1.0);
uniform float world_scale = 0.0; uniform float detail_scale = 24.0; uniform float detail_amp = 0.12; uniform float macro_scale = 0.35; uniform float macro_amp = 0.10; uniform vec3 macro_tint = vec3(1.0, 1.0, 1.0);
float h21(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f); return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 4; i++) { s += a * vnoise(p); p *= 2.03; a *= 0.5; } return s; }
void fragment() { vec3 wp0 = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz; vec2 tuv = world_scale > 0.0 ? wp0.xz * world_scale : UV; vec3 base = texture(tex, tuv).rgb * tint.rgb;
 vec3 wp = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz; float det = fbm(wp.xz * (detail_scale / 24.0) * 1.3) - 0.5; float mac = fbm(wp.xz * (macro_scale / 0.35) * 0.045 + 7.3) - 0.5;
 vec3 c = base * (1.0 + det * detail_amp * 2.0); c = mix(c, c * macro_tint, clamp(mac * macro_amp * 4.0 + 0.5, 0.0, 1.0) * 0.5); c *= (1.0 + mac * macro_amp);
 ALBEDO = c; float water = clamp(mysim_surface_water, 0.0, 1.0); ALBEDO *= 1.0 - water * wet_darkening; ROUGHNESS = mix(1.0, wet_roughness, water); SPECULAR = mix(0.04, wet_specular, water);  }" };
// RR3-style clean track: no skid-mark / dust / oil / wall-scuff decal overlays, and every asphalt uses its no-skids variant.
 static readonly System.Text.RegularExpressions.Regex DirtDecal = new System.Text.RegularExpressions.Regex("skid|dust|dirt_|oil|stain|scuff|grime|wall_damage|tyre_mark|rubber|ripple", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
 static void CleanTrack(Node3D layer) { int hidden = 0; foreach (var node in layer.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)node; string nm = mi.Name.ToString(); if (nm.ToLowerInvariant().StartsWith("overlay") && (DirtDecal.IsMatch(nm) || (System.Environment.GetEnvironmentVariable("MYSIM_NOOVERLAY") != null && !nm.ToLowerInvariant().Contains("start")))) { mi.Visible = false; hidden++; } } GD.Print("CLEANTRACK hidden dirt decals=", hidden); }
 static string NoSkid(string p) { if (p == null) return null; string f = System.IO.Path.GetFileName(p); if (f == "gnd_asphalt_01.png") return p.Replace(f, "gnd_asphalt_noskids_01.png"); if (f == "gnd_asphalt_03.png") return p.Replace(f, "gnd_asphalt_noskids_03.png"); if (f == "gnd_asphalt_dusty_01.png") return p.Replace(f, "gnd_asphalt_dusty_noskids_01.png"); if (f == "gnd_road_dark.png") return p.Replace("surface/road/gnd_road_dark.png", "surface/asphalt/gnd_asphalt_noskids_01.png"); return p; }
 // Per-surface detail: fine grain and large-scale variation tuned for each ground type so tiling reads as real surface instead of one repeated image.
 static void GroundClass(ShaderMaterial sm, string path) {
  if(System.Environment.GetEnvironmentVariable("MYSIM_AUTHORED_GROUND")=="1") { sm.SetShaderParameter("world_scale",0f);sm.SetShaderParameter("detail_amp",0f);sm.SetShaderParameter("macro_amp",0f);sm.SetShaderParameter("macro_tint",Vector3.One);return; }
  string n = System.IO.Path.GetFileName(path ?? "").ToLowerInvariant();
  if (System.Environment.GetEnvironmentVariable("MYSIM_UVROAD") == null && (n.StartsWith("gnd_asphalt") || n.StartsWith("gnd_grass") || n.StartsWith("gnd_sand") || n.StartsWith("gnd_gravel"))) sm.SetShaderParameter("world_scale", n.StartsWith("gnd_asphalt") ? 0.16f : 0.12f);
  if (n.Contains("asphalt") || n.Contains("road")) { sm.SetShaderParameter("detail_scale", 90f); sm.SetShaderParameter("detail_amp", 0.10f); sm.SetShaderParameter("macro_scale", 0.18f); sm.SetShaderParameter("macro_amp", 0.07f); sm.SetShaderParameter("macro_tint", new Vector3(0.96f, 0.96f, 0.98f)); }
  else if (n.Contains("grass")) { sm.SetShaderParameter("detail_scale", 60f); sm.SetShaderParameter("detail_amp", 0.22f); sm.SetShaderParameter("macro_scale", 0.30f); sm.SetShaderParameter("macro_amp", 0.30f); sm.SetShaderParameter("macro_tint", new Vector3(1.10f, 1.04f, 0.78f)); }
  else if (n.Contains("concrete") || n.Contains("pit")) { sm.SetShaderParameter("detail_scale", 70f); sm.SetShaderParameter("detail_amp", 0.14f); sm.SetShaderParameter("macro_scale", 0.22f); sm.SetShaderParameter("macro_amp", 0.10f); sm.SetShaderParameter("macro_tint", new Vector3(1.0f, 0.98f, 0.94f)); }
  else if (n.Contains("gravel") || n.Contains("sand") || n.Contains("dirt")) { sm.SetShaderParameter("detail_scale", 110f); sm.SetShaderParameter("detail_amp", 0.28f); sm.SetShaderParameter("macro_scale", 0.25f); sm.SetShaderParameter("macro_amp", 0.18f); sm.SetShaderParameter("macro_tint", new Vector3(1.05f, 0.98f, 0.88f)); }
  else { sm.SetShaderParameter("detail_scale", 50f); sm.SetShaderParameter("detail_amp", 0.10f); sm.SetShaderParameter("macro_scale", 0.2f); sm.SetShaderParameter("macro_amp", 0.08f); }
 }
 static string Res(string p) { if (p == null) return null; int i = p.IndexOf("Assets/RealRacing/"); return i < 0 ? null : "res://assets/" + p.Substring(i + "Assets/".Length); }
 static string Strip(string n) { int i = n.LastIndexOf('_'); return n; }
 public static int Apply(Node3D layer, int variant, out int total) {
  total = 0; int bound = 0;
  if (map == null) { if (!FileAccess.FileExists("res://assets/unity_map.json")) { GD.Print("UMAP file missing, using name matching"); return 0; } using var f = FileAccess.Open("res://assets/unity_map.json", FileAccess.ModeFlags.Read); map = Json.ParseString(f.GetAsText()).AsGodotDictionary(); }
  var tracks = map["tracks"].AsGodotDictionary(); string vk = variant.ToString(); if (!tracks.ContainsKey(vk)) { GD.Print("UMAP no variant ", vk); return 0; }
  var byName = tracks[vk].AsGodotDictionary(); CleanTrack(layer); var mats = map["materials"].AsGodotDictionary(); var used = new Dictionary<string, int>();
  foreach (var node in layer.FindChildren("*", "MeshInstance3D", true, false)) {
   var mi = (MeshInstance3D)node; string nm = mi.Name.ToString(); Godot.Collections.Array lists = null;
   if (byName.ContainsKey(nm)) lists = byName[nm].AsGodotArray(); else { int k = nm.LastIndexOf('_'); string alt = nm; while (alt.Length > 0 && char.IsDigit(alt[alt.Length - 1])) alt = alt.Substring(0, alt.Length - 1); if (byName.ContainsKey(alt)) lists = byName[alt].AsGodotArray(); }
   if (lists == null || lists.Count == 0) { total += mi.Mesh.GetSurfaceCount(); continue; }
   used.TryGetValue(nm, out int ui); used[nm] = ui + 1; var guids = lists[ui % lists.Count].AsGodotArray();
   bool placeholderBrand = false; { var gl = guids.Count > 0 ? guids[0].AsString() : null; if (gl != null && mats.ContainsKey(gl)) { var gm = mats[gl].AsGodotDictionary(); if (gm.ContainsKey("tex")) { string gt = System.IO.Path.GetFileName(gm["tex"].AsString()).ToLowerInvariant(); if (System.Text.RegularExpressions.Regex.IsMatch(gt, "^(mat_branding_[a-z_]*base(_[a-z])?|rr3(white|black)_(banners|billboard_[a-z]|concrete\\.rgb))\\.png$")) { var rp = Res(gm["tex"].AsString()); if (rp != null && ResourceLoader.Exists(rp) && GD.Load<Texture2D>(rp).GetWidth() <= 64) placeholderBrand = true; } } } }
   if (placeholderBrand) { mi.Visible = false; continue; } // sponsor-slot placeholder sheets (big black A/B/C letters in RR3 are replaced by sponsor art at runtime)
   bool ground = nm.ToLowerInvariant().StartsWith("gnd_") || nm.ToLowerInvariant().StartsWith("road") || nm.ToLowerInvariant().StartsWith("geo_terrain");
   for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++) {
    total++; if (guids.Count == 0) continue; string g = guids[s < guids.Count ? s : guids.Count - 1].AsString(); string key = g + (ground ? "|g" : "") + (nm.StartsWith("org_",System.StringComparison.OrdinalIgnoreCase)||nm.StartsWith("crowd",System.StringComparison.OrdinalIgnoreCase)?"|organic":"|structure");
    if (!cache.TryGetValue(key, out var mat)) {
     mat = null; if (mats.ContainsKey(g)) { var mi2 = mats[g].AsGodotDictionary(); string tp = mi2.ContainsKey("tex") ? Res(NoSkid(mi2["tex"].AsString())) : null; Texture2D tex = tp != null && ResourceLoader.Exists(tp) ? GD.Load<Texture2D>(tp) : null;
      Color col = Colors.White; if (mi2.ContainsKey("color")) { var c = mi2["color"].AsGodotArray(); col = new Color((float)c[0], (float)c[1], (float)c[2], (float)c[3]); }
      if (tp != null && System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(tp).ToLowerInvariant(), "^(mat_branding_[a-z_]*base(_[a-z])?|rr3(white|black)_(banners|billboard_[a-z]|concrete\\.rgb))\\.png$")) { tex = null; col = new Color(0.66f, 0.67f, 0.69f); }
      if (col.R + col.G + col.B < 0.06f) col = new Color(1, 1, 1, col.A < 0.01f ? 1f : col.A); bool texAlpha = HasAlpha(tex); bool clip = texAlpha || mi2.ContainsKey("alphaClip") && mi2["alphaClip"].AsBool(); bool trans = mi2.ContainsKey("transparent") && mi2["transparent"].AsBool(); float cut = mi2.ContainsKey("cutoff") ? (float)mi2["cutoff"].AsDouble() : 0.5f; if (cut <= 0.01f) cut = 0.5f;
      if (tex != null || col != Colors.White) {
       if (ground && tex != null) { var sm = new ShaderMaterial { Shader = GroundShader }; Conditions.ConfigureWetGround(sm); sm.SetShaderParameter("tex", tex); GroundClass(sm, tp); sm.SetShaderParameter("tint", new Color(col.R, col.G, col.B)); mat = sm; }
       else { var st = new StandardMaterial3D { AlbedoTexture = tex, AlbedoColor = col, Roughness = 1f, MetallicSpecular = 0.12f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic }; if (clip) { st.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor; st.AlphaScissorThreshold = cut > 0 ? cut : 0.5f; st.AlphaAntialiasingMode = BaseMaterial3D.AlphaAntiAliasing.AlphaToCoverage; st.AlphaAntialiasingEdge = 0.3f; st.CullMode = BaseMaterial3D.CullModeEnum.Disabled; } else if (trans) st.Transparency = BaseMaterial3D.TransparencyEnum.Alpha; if (System.Environment.GetEnvironmentVariable("MYSIM_NOUVFIX") != "1" && !(nm.ToLowerInvariant().StartsWith("org_") || nm.ToLowerInvariant().StartsWith("crowd") || (tp != null && System.IO.Path.GetFileName(tp).ToLowerInvariant().StartsWith("org_")))) { st.Uv1Scale = new Vector3(-1f, -1f, 1f); st.Uv1Offset = new Vector3(1f, 1f, 0f); } mat = st; }
      } }
     cache[key] = mat;
    }
    if (mat != null) { mi.SetSurfaceOverrideMaterial(s, mat); bound++; }
   }
  }
  GD.Print("UMAP variant ", variant, " bound=", bound, " of ", total, " surfaces");
  return bound;
 }
}}









