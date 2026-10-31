using Godot;
using System.Collections.Generic;
using MySimulator.Rebuild;
namespace MySim {
// Loads a recovered car FBX set, keeps LOD_A only, binds PNG textures by material suffix (mm_ext uses the livery).
public static class CarLoader {
 public static Node3D Load(string carId) {
  string dir = "res://assets/RealRacing/Source/" + carId + "/";
  var root = new Node3D { Name = carId };
  foreach (var part in Assets.Files(dir)) {
   if (!part.EndsWith(".fbx")) continue;
   var inst = GD.Load<PackedScene>(dir + part).Instantiate<Node3D>(); inst.Scale = Vector3.One * 100f; root.AddChild(inst);
  }
  var cache = new Dictionary<string, StandardMaterial3D>();
  foreach (var n in root.FindChildren("*", "MeshInstance3D", true, false)) {
   var m = (MeshInstance3D)n;
   string up = m.Name.ToString().ToUpperInvariant(); bool isInt = up.StartsWith("LOD_A_INT_") && !System.Text.RegularExpressions.Regex.IsMatch(up, "DAMAGE|BROKEN|CRACKED|SHATTERED"); if (!up.StartsWith("LOD_A") || System.Text.RegularExpressions.Regex.IsMatch(up, "DAMAGE|BROKEN|CRACKED|SHATTERED|BONNETCAM|_DEBRIS|SCRATCH|LOD_A_LIT_|WHEEL_BLUR") || (up.StartsWith("LOD_A_INT_") && !isInt)) { m.Visible = false; continue; } m.SetMeta("grp", isInt ? "int" : "ext"); if (isInt) m.Visible = false;
   for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++) {
    string mn = m.Mesh.SurfaceGetMaterial(s)?.ResourceName ?? ""; int i = mn.LastIndexOf("_mm_"); if (i < 0) continue; string suf = mn.Substring(i + 4);
    if (!cache.TryGetValue(suf, out var mat)) { mat = Build(dir, carId, suf); cache[suf] = mat; }
    m.SetSurfaceOverrideMaterial(s, mat);
   }
  }
  WindingFixer.Fix(root,out int windingSurfaces);
  return root;
 }
static Godot.Collections.Dictionary carMap;
 static readonly Shader CarShader = new Shader { Code = "shader_type spatial; render_mode cull_disabled; uniform sampler2D base : source_color, repeat_enable, filter_linear_mipmap_anisotropic; uniform sampler2D mask : hint_default_white; uniform vec2 uv_scale=vec2(-1.0); uniform vec2 uv_offset=vec2(1.0); uniform vec3 ambient_tint = vec3(1.0); uniform float use_mask = 0.0; uniform float alpha_mode = 0.0; uniform float base_roughness=0.5; uniform float base_metallic=0.0; uniform float clearcoat_weight=0.0; uniform float clearcoat_roughness=0.1; void fragment() { vec2 uv = UV * uv_scale + uv_offset; vec4 b = texture(base, uv); vec4 m = texture(mask, uv); float mk = mix(0.5, m.r, use_mask); ALBEDO = b.rgb * ambient_tint; ROUGHNESS = clamp(base_roughness * (0.8 + 0.4 * mk),0.03,0.98); SPECULAR = mix(0.3, 0.5, mk); CLEARCOAT = clearcoat_weight; CLEARCOAT_ROUGHNESS = clearcoat_roughness; METALLIC = base_metallic; if (alpha_mode > 0.5) { ALPHA = b.a; ALPHA_SCISSOR_THRESHOLD = 0.5; } }" };
 static void AuditUV(Material material,string role="",string renderer=""){
  string onlyNode=System.Environment.GetEnvironmentVariable("MYSIM_CAR_UV_NODE");if(!string.IsNullOrEmpty(onlyNode)&&onlyNode!=renderer)return;
  string scope=System.Environment.GetEnvironmentVariable("MYSIM_CAR_UV_SCOPE");if(scope=="livery"&&role!="ext")return;if(scope=="misc"&&role!="misc")return;if(scope=="nonlivery"&&(role=="ext"||material is not ShaderMaterial))return;
  string mode=System.Environment.GetEnvironmentVariable("MYSIM_CAR_UV");if(mode==null)return;Vector2 scale=mode=="none"?Vector2.One:mode=="u"?new Vector2(-1,1):mode=="v"?new Vector2(1,-1):new Vector2(-1,-1);var offset=new Vector2(scale.X<0?1:0,scale.Y<0?1:0);
  if(material is ShaderMaterial shader){shader.SetShaderParameter("uv_scale",scale);shader.SetShaderParameter("uv_offset",offset);}else if(material is StandardMaterial3D standard){standard.Uv1Scale=new Vector3(scale.X,scale.Y,1);standard.Uv1Offset=new Vector3(offset.X,offset.Y,0);}
 }
 static Godot.Collections.Dictionary surfaceUvRepairs;
 static Material SurfaceUV(Material material,int car,string renderer,string guid){
  if(System.Environment.GetEnvironmentVariable("MYSIM_DISABLE_SURFACE_UV_REPAIRS")=="1")return material;
  surfaceUvRepairs??=Json.ParseString(FileAccess.GetFileAsString("res://data/car_surface_uv_repairs.json")).AsGodotDictionary();string key=car+"/"+renderer;if(!surfaceUvRepairs.ContainsKey(key))return material;
  var spec=surfaceUvRepairs[key].AsGodotDictionary();var shader=material as ShaderMaterial;var texture=shader?.GetShaderParameter("base").As<Texture2D>();
  if(shader==null||guid!=spec["source_guid"].AsString()||texture?.ResourcePath!=spec["texture"].AsString())throw new System.InvalidOperationException("Reviewed surface UV repair source mismatch: "+key);
  var repaired=(ShaderMaterial)shader.Duplicate();var scale=spec["scale"].AsGodotArray();var offset=spec["offset"].AsGodotArray();repaired.SetShaderParameter("uv_scale",new Vector2(scale[0].AsSingle(),scale[1].AsSingle()));repaired.SetShaderParameter("uv_offset",new Vector2(offset[0].AsSingle(),offset[1].AsSingle()));GD.Print("SURFACE_UV_REPAIR ",key);return repaired;
 }
 static float MaterialFloat(Godot.Collections.Dictionary values,string key,float fallback) => values!=null && values.ContainsKey(key) ? values[key].AsSingle() : fallback;
 static string Res(string p) { if (p == null) return null; int i = p.IndexOf("Assets/RealRacing/"); return i < 0 ? null : "res://assets/" + p.Substring(i + "Assets/".Length); }
 // Exact Unity assignment from Car{n}.prefab: renderer name -> material guids -> textures (base + mask), colours and alpha mode.
 public static int ApplyExact(Node3D root, int carIdx) {
  RecoveredComponentMounts.Apply(root,carIdx);
  if (carMap == null) { if (!FileAccess.FileExists("res://assets/car_map.json")) return 0; carMap = Json.ParseString(FileAccess.GetFileAsString("res://assets/car_map.json")).AsGodotDictionary(); }
  var byName = carMap["cars"].AsGodotDictionary()[carIdx.ToString()].AsGodotDictionary(); var mats = carMap["materials"].AsGodotDictionary(); var cache = new Dictionary<string, Material>(); int bound = 0;
  foreach (var node in root.FindChildren("*", "MeshInstance3D", true, false)) {
   var mi = (MeshInstance3D)node; string nm = mi.Name.ToString(); if (!byName.ContainsKey(nm)) continue; var lists = byName[nm].AsGodotArray(); if (lists.Count == 0) continue; var guids = lists[0].AsGodotArray();
   for (int s = 0; s < mi.Mesh.GetSurfaceCount() && guids.Count > 0; s++) {
    string g = guids[s < guids.Count ? s : guids.Count - 1].AsString(); if (!cache.TryGetValue(g, out var mat)) {
     mat = null; if (mats.ContainsKey(g)) { var mi2 = mats[g].AsGodotDictionary(); var texs = mi2.ContainsKey("texs") ? mi2["texs"].AsGodotDictionary() : null; string bp = texs != null && texs.ContainsKey("_BaseMap") ? Res(texs["_BaseMap"].AsString()) : (texs != null && texs.ContainsKey("_MainTex") ? Res(texs["_MainTex"].AsString()) : null);
      if (bp == null) bp = FallbackTex(carIdx, nm); if (bp != null && ResourceLoader.Exists(bp)) { var sm = new ShaderMaterial { Shader = CarShader }; sm.SetShaderParameter("base", GD.Load<Texture2D>(bp)); string mp = texs != null && texs.ContainsKey("_MaskMap") ? Res(texs["_MaskMap"].AsString()) : null; if (mp != null && ResourceLoader.Exists(mp)) sm.SetShaderParameter("mask", GD.Load<Texture2D>(mp));
       var fl = mi2.ContainsKey("floats") ? mi2["floats"].AsGodotDictionary() : null; if (fl != null && fl.ContainsKey("_UseMask")) sm.SetShaderParameter("use_mask", (float)fl["_UseMask"].AsDouble()); sm.SetShaderParameter("base_roughness", Mathf.Clamp(1f-MaterialFloat(fl,"_Smoothness",.5f),.03f,.98f)); sm.SetShaderParameter("base_metallic", MaterialFloat(fl,"_Metallic",0)); sm.SetShaderParameter("clearcoat_weight", MaterialFloat(fl,"_ClearCoatMask",0)); sm.SetShaderParameter("clearcoat_roughness", Mathf.Clamp(1f-MaterialFloat(fl,"_ClearCoatSmoothness",.9f),.03f,1f));
       var cols = mi2.ContainsKey("colors") ? mi2["colors"].AsGodotDictionary() : null; if (cols != null && cols.ContainsKey("_AmbientColour")) { var a = cols["_AmbientColour"].AsGodotArray(); sm.SetShaderParameter("ambient_tint", new Vector3((float)a[0], (float)a[1], (float)a[2]) * 1.15f); }
       string ln = nm.ToLowerInvariant(); long q = mi2.ContainsKey("queue") ? (long)mi2["queue"].AsInt64() : -1; if (ln.Contains("badge") || ln.Contains("scratch") || ln.Contains("light") || ln.Contains("glow") || q > 2450) sm.SetShaderParameter("alpha_mode", 1f); mat = sm; } }
     if (mat == null && mats.ContainsKey(g)) {
     var descriptor=mats[g].AsGodotDictionary();var colors=descriptor.ContainsKey("colors")?descriptor["colors"].AsGodotDictionary():null;var floats=descriptor.ContainsKey("floats")?descriptor["floats"].AsGodotDictionary():null;
     if(colors!=null && colors.ContainsKey("_BaseColor")) {
      var c=colors["_BaseColor"].AsGodotArray();var color=new Color(c[0].AsSingle(),c[1].AsSingle(),c[2].AsSingle(),c[3].AsSingle());bool transparent=MaterialFloat(floats,"_Surface",0)>.5f;
      mat=new StandardMaterial3D { AlbedoColor=color, Roughness=Mathf.Clamp(1f-MaterialFloat(floats,"_Smoothness",.5f),.03f,.98f), Metallic=MaterialFloat(floats,"_Metallic",0), CullMode=BaseMaterial3D.CullModeEnum.Disabled, Transparency=transparent?BaseMaterial3D.TransparencyEnum.Alpha:BaseMaterial3D.TransparencyEnum.Disabled };
     }
    }
    cache[g] = mat; }
    if (mat != null) { var assigned=SurfaceUV(mat,carIdx,nm,g);string auditNode=System.Environment.GetEnvironmentVariable("MYSIM_CAR_UV_NODE");if(!string.IsNullOrEmpty(auditNode)&&auditNode==nm)assigned=(Material)mat.Duplicate();AuditUV(assigned,nm.Contains("_mm_")?nm.Substring(nm.LastIndexOf("_mm_")+4):"",nm);assigned.SetMeta("source_material_guid",g);mi.SetSurfaceOverrideMaterial(s,assigned);bound++; }
   }
  }
  GD.Print("CARMAP car=", carIdx, " bound=", bound); return bound;
 }
 public static Aabb MeasureBounds(Node3D car) { Aabb bb=new();bool first=true;var inv=car.GlobalTransform.AffineInverse();foreach(var node in car.FindChildren("*","MeshInstance3D",true,false)){var mesh=(MeshInstance3D)node;if(!mesh.Visible||!mesh.HasMeta("grp")||mesh.GetMeta("grp").AsString()!="ext")continue;var box=(inv*mesh.GlobalTransform)*mesh.GetAabb();bb=first?box:bb.Merge(box);first=false;}return bb; }
 public static Vector3 MeasureSize(Node3D car)=>MeasureBounds(car).Size;
 public static void ForceUnshaded(Node3D car) { foreach (var n in car.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; for (int s = 0; s < mi.Mesh.GetSurfaceCount(); s++) { var ov = mi.GetSurfaceOverrideMaterial(s) as ShaderMaterial; var tex = ov?.GetShaderParameter("base").As<Texture2D>(); if (tex != null) mi.SetSurfaceOverrideMaterial(s, new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoTexture = tex, CullMode = BaseMaterial3D.CullModeEnum.Disabled, Uv1Scale = new Vector3(1, -1, 1), Uv1Offset = new Vector3(0, 1, 0) }); } } }
 // Some Unity car materials have an empty _BaseMap (assigned at runtime in Unity): fall back to the car's own livery/slot texture by node suffix.
 static string FallbackTex(int carIdx, string nodeName) {
  string folder = MySimulator.Rebuild.CarCatalog.Folders[carIdx]; string dir = "res://assets/RealRacing/Source/" + folder + "/"; int k = nodeName.LastIndexOf("_mm_"); if (k < 0) return null; string suf = nodeName.Substring(k + 4);
  if(suf=="chassis"&&ResourceLoader.Exists("res://assets/RealRacing/VehicleShaderData/car_chassis.png"))return "res://assets/RealRacing/VehicleShaderData/car_chassis.png";
  if (suf == "ext") { foreach (var f in Assets.Files(dir + "livery/")) if (f.EndsWith(".png")) return dir + "livery/" + f; return null; }
  string p = dir + folder + "_" + suf + ".png"; return ResourceLoader.Exists(p) ? p : null;
 }
 public static Node3D[] AttachWheels(Node3D car, int carIdx) {
  var pts = Json.ParseString(FileAccess.GetFileAsString("res://assets/car_pivots.json")).AsGodotDictionary()[carIdx.ToString()].AsGodotArray();
  var wheelMeshes = new System.Collections.Generic.List<MeshInstance3D>();
  foreach (var n in car.FindChildren("*", "MeshInstance3D", true, false)) { var mi = (MeshInstance3D)n; string up = mi.Name.ToString().ToUpperInvariant(); if (System.Text.RegularExpressions.Regex.IsMatch(up, "^LOD_A_(WHEEL|TYRE|TIRE|CALIPER|BRAKE_DISC|DISC|HUB|ROTOR)") && !up.Contains("BLUR") && !up.Contains("GUARD")) wheelMeshes.Add(mi); }
  if (System.Environment.GetEnvironmentVariable("MYSIM_WHEELDBG") != null) foreach (var wm in wheelMeshes) for (int si = 0; si < wm.Mesh.GetSurfaceCount(); si++) { var am = wm.GetActiveMaterial(si); string ds = am is StandardMaterial3D sm0 ? "std tex=" + (sm0.AlbedoTexture != null ? sm0.AlbedoTexture.ResourcePath : "none") + " col=" + sm0.AlbedoColor + " met=" + sm0.Metallic + " rough=" + sm0.Roughness + " unsh=" + sm0.ShadingMode : (am == null ? "null" : am.GetType().Name + (am is ShaderMaterial shm ? " shader" : "")); GD.Print("WHEELPART ", wm.Name, " s", si, " ", ds); }
  foreach (var wm in wheelMeshes) { string wn = wm.Name.ToString().ToUpperInvariant(); bool tyre = wn.Contains("TYRE") || wn.Contains("TIRE"); bool rotor = wn.Contains("ROTOR") || wn.Contains("DISC"); if (!tyre && !rotor) continue; for (int si = 0; si < wm.Mesh.GetSurfaceCount(); si++) { var authored=wm.GetActiveMaterial(si);if(authored is ShaderMaterial shader&&shader.GetShaderParameter("base").VariantType==Variant.Type.Object&&shader.GetShaderParameter("base").As<Texture2D>()!=null)continue;if(authored is StandardMaterial3D standard&&standard.AlbedoTexture!=null)continue;if(authored!=null&&authored.HasMeta("source_material_guid"))continue;var tm = new StandardMaterial3D { AlbedoColor = tyre ? new Color(0.055f, 0.055f, 0.06f) : new Color(0.32f, 0.32f, 0.34f), Roughness = tyre ? 0.96f : 0.5f, Metallic = tyre ? 0f : 0.7f, MetallicSpecular = tyre ? 0.05f : 0.5f, CullMode = BaseMaterial3D.CullModeEnum.Disabled }; wm.SetSurfaceOverrideMaterial(si, tm); } }
  var pivots = new Node3D[4]; var inv = car.GlobalTransform.AffineInverse();
  for (int i = 0; i < 4; i++) {
   var p = pts[i].AsGodotArray(); var pivot = new Node3D { Name = "WheelPivot" + i, Position = new Vector3((float)p[0], (float)p[1], (float)p[2]) }; car.AddChild(pivot);
   var spin = new Node3D { Name = "Spin" }; pivot.AddChild(spin);var fixedBrake=new Node3D {Name="BrakeAssembly"};pivot.AddChild(fixedBrake); bool sourceMirror=System.Environment.GetEnvironmentVariable("MYSIM_WHEEL_MIRROR")!="legacy";if(sourceMirror?(float)p[0]>0:(float)p[0]<0)pivot.RotationDegrees=new Vector3(0,180,0);
   pivot.SetMeta("steer_base_yaw",pivot.Rotation.Y);
   bool rearAxle=(float)p[2]<0;
   string Family(MeshInstance3D mesh) { string name=mesh.Name.ToString().ToUpperInvariant();int material=name.IndexOf("_MM_",System.StringComparison.Ordinal);if(material>=0)name=name.Substring(0,material);return name.Replace("_REAR","").Replace("_FRONT",""); }
   foreach(var source in wheelMeshes){
    string name=source.Name.ToString().ToUpperInvariant(),family=Family(source);bool hasRear=false;
    foreach(var candidate in wheelMeshes)if(Family(candidate)==family&&candidate.Name.ToString().ToUpperInvariant().Contains("_REAR"))hasRear=true;
    if(hasRear&&name.Contains("_REAR")!=rearAxle)continue;
    var duplicate=(MeshInstance3D)source.Duplicate();if(name.Contains("CALIPER"))fixedBrake.AddChild(duplicate);else spin.AddChild(duplicate);duplicate.Transform=inv*source.GlobalTransform;duplicate.Visible=true;
   }
   pivots[i] = pivot;
  }
  for (int i = 0; i < 4; i++) {
   var spin = pivots[i].GetNode<Node3D>("Spin"); MeshInstance3D tyreDup = null; foreach (var ch in spin.GetChildren()) if (ch is MeshInstance3D cm) { string cn = cm.Name.ToString().ToUpperInvariant(); if (cn.Contains("TYRE") || cn.Contains("TIRE")) { if (tyreDup == null || cn.Contains("REAR") == (pts[i].AsGodotArray()[2].AsSingle() < 0)) tyreDup = cm; } }
   if (tyreDup == null) continue; var tb = tyreDup.Transform * tyreDup.GetAabb(); var ctr = tb.GetCenter(); float outerR = Mathf.Max(tb.Size.Y, tb.Size.Z) * 0.5f, tw = tb.Size.X; pivots[i].SetMeta("radius", outerR); if (outerR < 0.15f || outerR > 0.6f) continue;
   // Recovered tyre templates start at an inner sidewall. The prefab pivots are hub centres:
   // retain the template geometry and centre the complete assembly around that hub before mirroring/spinning.
   foreach (var child in spin.GetChildren()) if (child is MeshInstance3D part) part.Position -= ctr;
   var brakes=pivots[i].GetNode<Node3D>("BrakeAssembly");foreach(var child in brakes.GetChildren())if(child is MeshInstance3D part)part.Position-=ctr;
   if(System.Environment.GetEnvironmentVariable("MYSIM_REBUILT_TYRES")=="1"){foreach(var child in spin.GetChildren())if(child is MeshInstance3D part&&(part.Name.ToString().ToUpperInvariant().Contains("TYRE")||part.Name.ToString().ToUpperInvariant().Contains("TIRE"))){part.Visible=false;part.SetMeta("wheel_hidden",true);}spin.AddChild(TyreMeshBuilder.Create(outerR,tw,false));}
   bool authoredWheel=false;foreach(var child in spin.GetChildren())if(child is MeshInstance3D part&&part.Name.ToString().ToUpperInvariant().StartsWith("LOD_A_WHEEL"))authoredWheel=true;
   bool rebuild=System.Environment.GetEnvironmentVariable("MYSIM_REBUILT_RIMS")=="1";if(authoredWheel&&!rebuild&&System.Environment.GetEnvironmentVariable("MYSIM_PROCEDURAL_RIMS")!="1")continue;
   foreach (var ch in spin.GetChildren()) if (ch is MeshInstance3D cm2) { string cn2 = cm2.Name.ToString().ToUpperInvariant(); if (cn2.StartsWith("LOD_A_WHEEL")) { cm2.Visible = false; cm2.SetMeta("wheel_hidden", true); } }
   bool open = outerR < 0.37f && tw > 0.3f; float rimR = outerR * (carIdx >= 15 && carIdx <= 18 ? 0.6f : 0.69f); var rim = rebuild?WheelRimBuilder.Build(rimR,tw,carIdx>=15&&carIdx<=18?2:carIdx%2):BuildRim(rimR, tw, carIdx); rim.Position = Vector3.Zero; spin.AddChild(rim);
  }
  RecoveredComponentMounts.AttachToHubs(car,pivots);
  foreach (var w in wheelMeshes) { w.Visible = false; w.SetMeta("wheel_hidden", true); }
  var measuredBounds=MeasureBounds(car);var measured=measuredBounds.Size;var center=measuredBounds.GetCenter();MySimulator.Rebuild.VehicleDimensions.CenterX[carIdx]=center.X;MySimulator.Rebuild.VehicleDimensions.CenterY[carIdx]=center.Y;MySimulator.Rebuild.VehicleDimensions.CenterZ[carIdx]=center.Z; MySimulator.Rebuild.VehicleDimensions.Width[carIdx]=measured.X; MySimulator.Rebuild.VehicleDimensions.Height[carIdx]=measured.Y; MySimulator.Rebuild.VehicleDimensions.Length[carIdx]=measured.Z;
  TyreAppearance.Apply(pivots,Compound.Medium);
  GD.Print("WHEELS car=", carIdx, " parts=", wheelMeshes.Count);
  return pivots;
 }

 static Node3D BuildRim(float r, float tyreW, int carIdx) {
  var root = new Node3D { Name = "ProcRim" }; bool f1 = carIdx >= 15 && carIdx <= 18; float w = tyreW * (f1 ? 0.78f : 0.7f);
  Color[] tints = { new Color(0.78f, 0.79f, 0.82f), new Color(0.3f, 0.3f, 0.33f), new Color(0.74f, 0.62f, 0.34f), new Color(0.55f, 0.57f, 0.6f) }; var tint = tints[f1 ? 1 : (carIdx % 4 == 2 ? 2 : (carIdx % 4 == 1 ? 1 : (carIdx % 4 == 3 ? 3 : 0)))];
  var metal = new StandardMaterial3D { AlbedoColor = tint, Metallic = 0.55f, Roughness = 0.32f, MetallicSpecular = 0.7f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
  var dark = new StandardMaterial3D { AlbedoColor = new Color(0.04f, 0.04f, 0.045f), Roughness = 0.9f, CullMode = BaseMaterial3D.CullModeEnum.Disabled };
  var lip = new StandardMaterial3D { AlbedoColor = new Color(0.82f, 0.83f, 0.86f), Metallic = 0.6f, Roughness = 0.25f };
  Basis axis = new Basis(Vector3.Back, Mathf.Pi / 2f); // cylinder Y axis -> wheel X axis
  void Cyl(float rad, float len, float xOff, StandardMaterial3D m) { var mi = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = rad, BottomRadius = rad, Height = len, RadialSegments = 32, Rings = 1 }, MaterialOverride = m, Transform = new Transform3D(axis, new Vector3(xOff, 0, 0)) }; root.AddChild(mi); }
  Cyl(r * 0.97f, w * 0.9f, 0f, dark);                 // deep dark barrel seen between the spokes
  root.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = r * 0.86f, OuterRadius = r * 1.0f, Rings = 32, RingSegments = 8 }, MaterialOverride = lip, Transform = new Transform3D(axis, new Vector3(w * 0.46f, 0, 0)) }); // polished outer lip ring
  Cyl(r * 0.2f, w * 0.18f, w * 0.44f, metal);         // hub
  var capMat = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.92f), Metallic = 0.5f, Roughness = 0.3f }; Cyl(r * 0.07f, w * 0.12f, w * 0.52f, capMat);
  int n = f1 ? 10 : (carIdx % 2 == 0 ? 5 : 7);
  for (int k = 0; k < n; k++) {
   float ang = Mathf.Tau * k / n; var spoke = new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(w * 0.1f, r * 0.78f, r * (f1 ? 0.09f : 0.19f)) }, MaterialOverride = metal };
   var dir = new Vector3(0, Mathf.Cos(ang), Mathf.Sin(ang)); spoke.Position = new Vector3(w * 0.42f, dir.Y * r * 0.54f, dir.Z * r * 0.54f); spoke.Basis = new Basis(Vector3.Right, ang); root.AddChild(spoke);
  }
  return root;
 }
 static StandardMaterial3D Build(string dir, string car, string suf) {
  var mat = new StandardMaterial3D { Roughness = 0.45f, Metallic = 0.2f };
  string tex = dir + car + "_" + suf + ".png"; if(suf=="chassis")tex="res://assets/RealRacing/VehicleShaderData/car_chassis.png"; if (suf == "ext") tex = dir + "livery/" + car + "_ext_59.png";
  if (ResourceLoader.Exists(tex)) mat.AlbedoTexture = GD.Load<Texture2D>(tex); else mat.AlbedoColor = new Color(0.12f, 0.12f, 0.13f);
  if (suf == "lights" || suf == "badges" || suf == "scratches") mat.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
  if (suf == "lights") { mat.EmissionEnabled = false; }
  AuditUV(mat,suf);return mat;
 }
}}











