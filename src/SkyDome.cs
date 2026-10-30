using Godot;
namespace MySim {
// Original track sky dome (<map>/processed/*_sky.fbx + hills png), unshaded, re-centred on the camera every frame.
public partial class SkyDome : Node3D {
 Node3D dome;
 public static void Add(Node3D parent, string map) {
  if(Conditions.IsNight||Conditions.Wet>0){GD.Print("SKY procedural conditions selected; daytime panorama suppressed");return;}
  string dir = "res://assets/RealRacing/Source/" + map + "/processed/"; string fbx = null; string png = null;
  { foreach (var f in Assets.Files(dir)) { if (f.EndsWith("_sky.fbx")) fbx = f; if (f.EndsWith(".png")) png = f; } }
  if (fbx == null) { GD.Print("SKY no fbx"); return; }
  var node = GD.Load<PackedScene>(dir + fbx).Instantiate<Node3D>(); node.Scale = new Vector3(100f, 100f, 100f);
  var tex = png != null ? GD.Load<Texture2D>(dir + png) : null; int n = 0;
  foreach (var mi in node.FindChildren("*", "MeshInstance3D", true, false)) { var m = (MeshInstance3D)mi; m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off; for (int s = 0; s < m.Mesh.GetSurfaceCount(); s++) { var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled, AlbedoTexture = tex, Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor }; m.SetSurfaceOverrideMaterial(s, mat); n++; } }
  var sd = new SkyDome { dome = node }; sd.AddChild(node); parent.AddChild(sd); GD.Print("SKY loaded ", fbx, " tex=", png, " surfaces=", n);
 }
 public override void _Process(double delta) { var cam = GetViewport().GetCamera3D(); if (cam != null) GlobalPosition = cam.GlobalPosition; }
}}

