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
  var missing=new List<object>(); var resolutions=new List<object>(); var cache = new Dictionary<string, StandardMaterial3D>(); int bound = 0, total = 0, unbound = 0; UnboundNames.Clear();
  var trackMeshes=track.FindChildren("*", "MeshInstance3D", true, false);
  foreach (var n in trackMeshes) {
   var m = (MeshInstance3D)n;
   for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++) {
    total++; if (m.GetSurfaceOverrideMaterial(s) != null) { bound++; continue; } string name = m.Mesh.SurfaceGetMaterial(s)?.ResourceName ?? ""; if (name == "") continue;
    if (!cache.TryGetValue(name, out var mat)) {
     string tex = null;string resolution="missing";var candidates=new List<string>();
     if(map.TryGetValue(name,out var t0)){tex=t0;resolution="exact";}
     else {var textures=new HashSet<string>();foreach(var kv in map)if(kv.Key.StartsWith(name+"_",System.StringComparison.Ordinal)){candidates.Add(kv.Key);textures.Add(kv.Value);}if(textures.Count==1){foreach(var texture in textures)tex=texture;resolution="unique-delimited-prefix";}else if(textures.Count>1)resolution="ambiguous-prefix-rejected";}
     resolutions.Add(new {material=name,resolution,texture=tex,candidates});
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
  trackMeshes.Clear();
  TrackCutoutDiagnostic.Apply(track);
  var audit=System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT");if(!string.IsNullOrEmpty(audit)){System.IO.Directory.CreateDirectory(audit);string safeName=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(audit,"bindings-"+safeName+".json"),System.Text.Json.JsonSerializer.Serialize(new {layer=track.Name.ToString(),fallbacks=missing,resolutions},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
  GD.Print("TRACKBIND fallbackGrey=", unbound, " unboundNames=", string.Join("|", UnboundNames.GetRange(0, System.Math.Min(40, UnboundNames.Count))));
  GD.Print("TRACKBIND bound=", bound, " of ", total, " surfaces; distinct textured materials=", cache.Count);
  RoadVariationPass(track);
  GrassBalancePass(track);
  TreeBreakupPass(track);
  ForestTonePass(track);
  WallBuilder.AttachCollisionOverlay(track);
 }
 // Road MATERIAL definition candidates: env-gated profile selector (MYSIM_ROAD_MATERIAL=<name>), reversible,
 // addressed only to the ground ShaderMaterials whose texture is gnd_asphalt_* (the shader's own asphalt class).
 // WHAT THE ROAD MATERIAL IS (source): the visible road = the track layers' gnd_/road meshes bound to a
 // ShaderMaterial (UnityMaterialBinder's GroundShader for mapped tracks; this file's twin for the rest):
 // ALBEDO = the asphalt texture world-projected * tint + procedural fbm variation; ROUGHNESS constant 1.0 dry;
 // SPECULAR constant 0.04; no normal-map sampling (flat vertex normals). The separate DriveSurface = the
 // INVISIBLE collision mesh (layer 1). The profiles tune the shader's EXISTING uniforms only (spatial definition);
 // roughness/specular stay shader-code constants (an owner-side two-line expose is the follow-up), no texture
 // swap, no tiling change. Defaults untouched when unset; MYSIM_ROAD_VARIATION=1 maps to "definition".
 static (float detailScale,float detailAmp,float macroScale,float macroAmp,Vector3 macroTint)? RoadProfile(string name){
  switch(name){
   case "base":return (90f,0.10f,0.18f,0.07f,new Vector3(0.96f,0.96f,0.98f));
   case "definition":return (110f,0.16f,0.26f,0.11f,new Vector3(0.95f,0.97f,1.00f));
   case "worn":return (55f,0.10f,0.40f,0.14f,new Vector3(0.97f,0.98f,1.00f));
   default:return null;
  }
 }
 static void RoadVariationPass(Node3D track){
  string profileName=System.Environment.GetEnvironmentVariable("MYSIM_ROAD_MATERIAL");
  if(string.IsNullOrEmpty(profileName)&&System.Environment.GetEnvironmentVariable("MYSIM_ROAD_VARIATION")=="1")profileName="definition";
  if(string.IsNullOrEmpty(profileName))return;
  var profile=RoadProfile(profileName);
  if(profile==null){GD.Print("ROAD_MATERIAL unknown profile: ",profileName," (base|definition|worn)");return;}
  var pf=profile.Value;
  if(System.Environment.GetEnvironmentVariable("MYSIM_AUTHORED_GROUND")=="1"){GD.Print("ROAD_MATERIAL skipped: MYSIM_AUTHORED_GROUND owns ground variation");return;}
  var seen=new HashSet<Material>();var records=new List<object>();
  foreach(var node in track.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;if(mesh.Mesh==null)continue;
   for(int s=0;s<mesh.Mesh.GetSurfaceCount();s++){
    if(mesh.GetSurfaceOverrideMaterial(s) is not ShaderMaterial sm||sm.Shader==null)continue;
    if(!seen.Add(sm))continue;
    var tex=sm.GetShaderParameter("tex").AsGodotObject() as Texture2D;if(tex==null)continue;
    string name=System.IO.Path.GetFileName(tex.ResourcePath).ToLowerInvariant();
    if(!name.StartsWith("gnd_asphalt",System.StringComparison.Ordinal))continue;
    sm.SetShaderParameter("detail_scale",pf.detailScale);
    sm.SetShaderParameter("detail_amp",pf.detailAmp);
    sm.SetShaderParameter("macro_scale",pf.macroScale);
    sm.SetShaderParameter("macro_amp",pf.macroAmp);
    sm.SetShaderParameter("macro_tint",pf.macroTint);
    records.Add(new{renderer=mesh.GetPath().ToString(),surface=s,texture=tex.ResourcePath,profile=profileName,detailScale=pf.detailScale,detailAmp=pf.detailAmp,macroScale=pf.macroScale,macroAmp=pf.macroAmp,macroTint=pf.macroTint.ToString()});
   }
  }
  GD.Print("ROAD_MATERIAL profile=",profileName," materials=",records.Count," scope=gnd_asphalt* ground ShaderMaterials only; appearance only");
  if(System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT") is string dir&&records.Count>0){System.IO.Directory.CreateDirectory(dir);string safeName=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"road-material-"+safeName+".json"),System.Text.Json.JsonSerializer.Serialize(new{layer=track.Name.ToString(),profile=profileName,materials=records,scope="Road material profile candidate on gnd_asphalt ground materials; env-gated; uniforms only; no texture swap/tiling/roughness/spec change; not a promoted fix"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
 }
 // One controlled tree-line breakup candidate: env-gated (MYSIM_TREE_BREAKUP=1), reversible, addressed only to the
 // continuous backdrop wall the reviewer flagged: org_brandshatch_treeline_00..24, 25 separate backdrop strip nodes
 // sharing ONE source texture (org_brandshatch_tree_line_1.png is the only treeline texture on disk, so per-instance
 // texture variant swap is NOT supported by source data). Applies deterministic per-segment variation derived from
 // the node name (height scale 0.94..1.06 only; yaw removed - see note in body) with the world-space bottom of each strip kept
 // planted by compensating the position for the local AABB bottom. Transforms only: no mesh/texture/material change;
 // deterministic across runs; default rendering unchanged when the variable is unset.
 static void TreeBreakupPass(Node3D track){
  if(System.Environment.GetEnvironmentVariable("MYSIM_TREE_BREAKUP")!="1")return;
  var records=new List<object>();int varied=0;
  foreach(var node in track.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;if(mesh.Mesh==null)continue;
   string nm=mesh.Name.ToString();
   if(!nm.StartsWith("org_brandshatch_treeline_",System.StringComparison.Ordinal))continue;
   uint h=Fnv1a(nm);
   // Height-scale variation only. Yaw was removed after the run-41843204651 audit showed these FBX nodes carry
   // identity transforms with all geometry baked into vertices far from the node origin (worldOrigin=(0,0,0),
   // strips 80-470 m long): a yaw would sweep each strip around the WORLD origin at multi-km radius, not around
   // its own centre. Scale about the origin keeps the strip base planted (the strip base sits at the local y=0
   // terrain datum; the AABB-bottom compensation below is the exact form of that guarantee for any origin).
   float scaleY=0.94f+(((h/1000u)%1000u)/1000f)*0.12f;
   var aabb=mesh.GetAabb();float bottomLocal=aabb.GetCenter().Y-aabb.Size.Y*0.5f;
   var pos=mesh.Position;
   mesh.Scale=new Vector3(mesh.Scale.X,scaleY,mesh.Scale.Z);
   mesh.Position=new Vector3(pos.X,pos.Y-(scaleY-1f)*bottomLocal,pos.Z);
   varied++;
   records.Add(new{renderer=mesh.GetPath().ToString(),scaleY,bottomPin="position compensated by aabb bottom; scale-only (yaw removed: vertex-baked geometry pivots at the node origin)"});
  }
  GD.Print("TREE_BREAKUP segments=",varied," scope=org_brandshatch_treeline_* nodes only; deterministic scale-only; textures/meshes untouched");
  if(System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT") is string dir&&records.Count>0){System.IO.Directory.CreateDirectory(dir);string safeName=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"tree-breakup-"+safeName+".json"),System.Text.Json.JsonSerializer.Serialize(new{layer=track.Name.ToString(),segments=records,scope="One breakup candidate: deterministic per-segment scaleY on treeline strip nodes (yaw removed after the vertex-baked-geometry finding); variant swap unsupported (single shared source texture); not a promoted fix"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
 }
 // One controlled forest-tone candidate: env-gated (MYSIM_FOREST_TONE=<multiplier>, e.g. 1.35), reversible,
 // addressed only to the vegetation-backdrop materials the reviewer flagged (org_brandshatch_tree_line_* strips
 // and org_tree_*/org_shrub_*/org_bush_* instances). The measured source textures are intrinsically dark —
 // treeline RGB (51,53,45), pine billboard solid (37,43,26) — so the backdrop reads as a dark wall against the
 // bright sky. AlbedoColor multiplies AlbedoTexture in StandardMaterial3D, so a per-material lift raises the
 // displayed colour without touching any texture file. Default unset = untouched. Single knob; logged + audited.
 static void ForestTonePass(Node3D track){
  string toneText=System.Environment.GetEnvironmentVariable("MYSIM_FOREST_TONE");
  if(string.IsNullOrEmpty(toneText))return;
  if(!float.TryParse(toneText,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float tone)||tone<=0f||tone>3f)return;
  var seen=new HashSet<Material>();var records=new List<object>();
  foreach(var node in track.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;if(mesh.Mesh==null)continue;
   for(int s=0;s<mesh.Mesh.GetSurfaceCount();s++){
    if(mesh.GetSurfaceOverrideMaterial(s) is not StandardMaterial3D mat||mat.AlbedoTexture==null)continue;
    if(!seen.Add(mat))continue;
    string texName=System.IO.Path.GetFileName(mat.AlbedoTexture.ResourcePath);
    bool backdrop=texName.StartsWith("org_brandshatch_tree_line",System.StringComparison.Ordinal)||texName.StartsWith("org_tree_",System.StringComparison.Ordinal)||texName.StartsWith("org_shrub_",System.StringComparison.Ordinal)||texName.StartsWith("org_bush_",System.StringComparison.Ordinal);
    if(!backdrop)continue;
    Color c=mat.AlbedoColor;
    mat.AlbedoColor=new Color(Mathf.Min(1f,c.R*tone),Mathf.Min(1f,c.G*tone),Mathf.Min(1f,c.B*tone),c.A);
    records.Add(new{renderer=mesh.GetPath().ToString(),surface=s,texture=mat.AlbedoTexture.ResourcePath,albedoFrom=new[]{c.R,c.G,c.B},tone});
   }
  }
  GD.Print("FOREST_TONE materials=",records.Count," tone=",tone," scope=vegetation-backdrop materials only; textures/geometry untouched");
  if(System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT") is string dir&&records.Count>0){System.IO.Directory.CreateDirectory(dir);string safeName=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"forest-tone-"+safeName+".json"),System.Text.Json.JsonSerializer.Serialize(new{layer=track.Name.ToString(),tone,materials=records,scope="One tone-lift candidate on vegetation-backdrop materials; env-gated per-material; no texture/geometry change; not a promoted fix"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
 }
 static uint Fnv1a(string s){uint h=2166136261u;foreach(char c in s){h^=c;h*=16777619u;}return h;}
 // One controlled grass colour-balance candidate: env-gated (MYSIM_GRASS_BALANCE=1), reversible, addressed only to
 // ground ShaderMaterials whose texture is gnd_grass_* (the shader's own texture-based grass class). Rebalances the
 // warm macro tint (authored (1.10,1.04,0.78) pushes yellow-green), halves the blotch/streak amplitudes and slightly
 // darkens the tile tint. Never touches road, fence, foliage or any non-ground material; respects MYSIM_AUTHORED_GROUND
 // (which already zeroes procedural variation); default rendering unchanged when the variable is unset.
 static void GrassBalancePass(Node3D track){
  if(System.Environment.GetEnvironmentVariable("MYSIM_GRASS_BALANCE")!="1")return;
  if(System.Environment.GetEnvironmentVariable("MYSIM_AUTHORED_GROUND")=="1"){GD.Print("GRASS_BALANCE skipped: MYSIM_AUTHORED_GROUND owns ground variation");return;}
  var seen=new HashSet<Material>();var records=new List<object>();int surfaces=0;
  foreach(var node in track.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;if(mesh.Mesh==null)continue;
   for(int s=0;s<mesh.Mesh.GetSurfaceCount();s++){
    if(mesh.GetSurfaceOverrideMaterial(s) is not ShaderMaterial sm||sm.Shader==null)continue;
    if(!seen.Add(sm))continue;
    var tex=sm.GetShaderParameter("tex").AsGodotObject() as Texture2D;if(tex==null)continue;
    string name=System.IO.Path.GetFileName(tex.ResourcePath).ToLowerInvariant();
    if(!name.StartsWith("gnd_grass",System.StringComparison.Ordinal))continue;
    var tintVar=sm.GetShaderParameter("tint");
    if(tintVar.VariantType!=Variant.Type.Color)continue;
    Color tint=tintVar.AsColor();
    sm.SetShaderParameter("tint",new Color(tint.R*0.92f,tint.G*0.96f,tint.B*0.94f));
    sm.SetShaderParameter("macro_tint",new Vector3(0.99f,1.02f,0.96f));
    sm.SetShaderParameter("macro_amp",0.15f);
    sm.SetShaderParameter("detail_amp",0.14f);
    records.Add(new{renderer=mesh.GetPath().ToString(),surface=s,texture=tex.ResourcePath,tintFrom=new[]{tint.R,tint.G,tint.B},tintTo=new[]{tint.R*0.92f,tint.G*0.96f,tint.B*0.94f},macroTintFrom="1.10,1.04,0.78",macroTintTo="0.99,1.02,0.96",macroAmpTo=0.15f,detailAmpTo=0.14f});
    surfaces++;
   }
  }
  GD.Print("GRASS_BALANCE materials=",surfaces," scope=gnd_grass ground ShaderMaterials only; road/fence/foliage untouched");
  if(System.Environment.GetEnvironmentVariable("MYSIM_BINDING_AUDIT") is string dir&&records.Count>0){System.IO.Directory.CreateDirectory(dir);string safeName=System.Text.RegularExpressions.Regex.Replace(track.Name.ToString(),"[^A-Za-z0-9_-]","_");System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"grass-balance-"+safeName+".json"),System.Text.Json.JsonSerializer.Serialize(new{layer=track.Name.ToString(),materials=records,scope="One colour-balance candidate bundle on gnd_grass ground materials; env-gated; no road/fence/foliage material touched; not a promoted fix"},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));}
 }
}}








