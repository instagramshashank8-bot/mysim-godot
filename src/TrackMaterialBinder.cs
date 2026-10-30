using Godot;
using System.Collections.Generic;
namespace MySim {
// Binds the original recovered textures to the track's FBX materials (name -> texture via recovered Unity material names).
public static class TrackMaterialBinder {
 static readonly Shader GroundShader = new Shader { Code = "shader_type spatial; render_mode cull_disabled; global uniform float mysim_surface_water; uniform float wet_darkening = 0.12; uniform float wet_roughness = 0.18; uniform float wet_specular = 0.45; uniform sampler2D tex : source_color, repeat_enable, filter_linear_mipmap_anisotropic; void fragment() { ALBEDO = texture(tex, UV).rgb; float water = clamp(mysim_surface_water, 0.0, 1.0); ALBEDO *= 1.0 - water * wet_darkening; ROUGHNESS = mix(1.0, wet_roughness, water); SPECULAR = mix(0.04, wet_specular, water);  }" };
 public static readonly System.Collections.Generic.List<string> UnboundNames = new System.Collections.Generic.List<string>();
 public static void Apply(Node3D track, string dir) {
  var map = new Dictionary<string, string>();
  using (var f = FileAccess.Open(dir + "tex_by_material.json", FileAccess.ModeFlags.Read)) { var d = Json.ParseString(f.GetAsText()).AsGodotDictionary(); foreach (var k in d.Keys) map[k.AsString()] = d[k].AsString(); }
  var missing=new List<object>(); var cache = new Dictionary<string, StandardMaterial3D>(); int bound = 0, total = 0, unbound = 0; UnboundNames.Clear();
  foreach (var n in track.FindChildren("*", "MeshInstance3D", true, false)) {
   var m = (MeshInstance3D)n;
   for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++) {
    total++; if (m.GetSurfaceOverrideMaterial(s) != null) { bound++; continue; } string name = m.Mesh.SurfaceGetMaterial(s)?.ResourceName ?? ""; if (name == "") continue;
    if (!cache.TryGetValue(name, out var mat)) {
     string tex = null; if (map.TryGetValue(name, out var t0)) tex = t0; else foreach (var kv in map) if (kv.Key.StartsWith(name + "_") || kv.Key.StartsWith(name)) { tex = kv.Value; break; }
     if (tex != null && ResourceLoader.Exists(dir + "textures/" + tex)) {
      mat = new StandardMaterial3D { AlbedoTexture = GD.Load<Texture2D>(dir + "textures/" + tex), Roughness = 1f, MetallicSpecular = 0.12f, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic };
      string ln = name.ToLowerInvariant(); if (ln.Contains("overlay") || ln.Contains("alpha") || ln.Contains("fence") || ln.Contains("crowd")) mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
     }
     if (mat == null) { string l2 = name.ToLowerInvariant(); if (!(l2.Contains("overlay") || l2.Contains("decal"))) { unbound++; mat = new StandardMaterial3D { AlbedoColor = new Color(0.36f, 0.36f, 0.34f), Roughness = 1f, MetallicSpecular = 0.1f }; if (UnboundNames.Count < 400) UnboundNames.Add(name); } }
     cache[name] = mat;
    }
    if(mat!=null&&mat.AlbedoTexture==null)missing.Add(new {renderer=m.Name.ToString(),surface=s,material=name});
    if (mat != null) { string mn2 = m.Name.ToString().ToLowerInvariant(); if (mat.AlbedoTexture!=null && (mn2.StartsWith("gnd_") || mn2.StartsWith("road"))) { var sm2 = new ShaderMaterial { Shader = GroundShader }; sm2.SetShaderParameter("tex", mat.AlbedoTexture); Conditions.ConfigureWetGround(sm2); m.SetSurfaceOverrideMaterial(s, sm2); bound++; continue; } 
    m.SetSurfaceOverrideMaterial(s, mat); bound++; }
   }
  }
  var audit=System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT");if(!string.IsNullOrEmpty(audit)){System.IO.Directory.CreateDirectory(audit);string safeName=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(audit,"bindings-"+safeName+".json"),System.Text.Json.JsonSerializer.Serialize(new {layer=track.Name.ToString(),fallbacks=missing},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
  GD.Print("TRACKBIND fallbackGrey=", unbound, " unboundNames=", string.Join("|", UnboundNames.GetRange(0, System.Math.Min(40, UnboundNames.Count))));
  GD.Print("TRACKBIND bound=", bound, " of ", total, " surfaces; distinct textured materials=", cache.Count);
 }
}}








