using Godot;
namespace MySim {
// Original replacement paint candidate. Continuous assembly coordinates avoid per-panel atlas mirroring.
// Source geometry, UVs, original materials and non-paint roles are retained.
public static class RecoveredBodyPaint {
 static readonly Shader shader=new Shader {Code=@"shader_type spatial;
 render_mode cull_disabled;
 uniform mat4 car_from_mesh;
 uniform vec4 paint : source_color = vec4(0.4,0.02,0.03,1.0);
 uniform vec4 accent : source_color = vec4(0.9,0.85,0.72,1.0);
 varying vec3 panel_position;
 void vertex(){ panel_position=(car_from_mesh*vec4(VERTEX,1.0)).xyz; }
 void fragment(){
 float stripe=1.0-smoothstep(0.10,0.12,abs(abs(panel_position.x)-0.24));
 float lower=1.0-smoothstep(0.19,0.23,panel_position.y);
 vec3 colour=mix(paint.rgb,accent.rgb,stripe*0.9);
 ALBEDO=mix(colour,vec3(0.018,0.022,0.027),lower*0.85);
 METALLIC=0.25;ROUGHNESS=0.28;SPECULAR=0.5;CLEARCOAT=0.65;CLEARCOAT_ROUGHNESS=0.16;
 }"};
 public static void Apply(Node3D car,int index){
  Color[] palette={new Color(.65f,.025f,.045f),new Color(.025f,.18f,.38f),new Color(.025f,.32f,.20f),new Color(.57f,.28f,.025f)};
  var nodes=car.FindChildren("*","MeshInstance3D",true,false);int count=0;foreach(var node in nodes){var mesh=(MeshInstance3D)node;if(!mesh.IsVisibleInTree()||!mesh.Name.ToString().EndsWith("_mm_ext",System.StringComparison.OrdinalIgnoreCase))continue;
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){var paint=new ShaderMaterial {Shader=shader};paint.SetShaderParameter("car_from_mesh",car.GlobalTransform.AffineInverse()*mesh.GlobalTransform);paint.SetShaderParameter("paint",palette[index%palette.Length]);paint.SetShaderParameter("accent",new Color(.82f,.85f,.88f));mesh.SetSurfaceOverrideMaterial(surface,paint);count++;}
  }nodes.Clear();GD.Print("REBUILT_BODY_PAINT car=",index," surfaces=",count," candidate_only=1");
 }
}
}
