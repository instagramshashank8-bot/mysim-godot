using Godot;
using System;
namespace MySim {
// Replacement candidate built in hub-centred coordinates. Source mesh/UVs remain untouched.
public static class TyreMeshBuilder {
 public static ArrayMesh Build(float radius,float width,bool wet){
  if(!float.IsFinite(radius)||!float.IsFinite(width)||radius<.15f||radius>.6f||width<.1f||width>.8f)throw new ArgumentOutOfRangeException("Invalid recovered tyre dimensions");
  // Closed section: bead, rounded sidewall, shoulder, contact crown, opposite sidewall and inner liner.
  var section=new[]{new Vector2(-.43f,.64f),new Vector2(-.49f,.70f),new Vector2(-.52f,.82f),new Vector2(-.51f,.91f),new Vector2(-.46f,.975f),new Vector2(-.39f,.995f),new Vector2(-.30f,1),new Vector2(-.20f,1.002f),new Vector2(-.10f,1.003f),new Vector2(0,1.004f),new Vector2(.10f,1.003f),new Vector2(.20f,1.002f),new Vector2(.30f,1),new Vector2(.39f,.995f),new Vector2(.46f,.975f),new Vector2(.51f,.91f),new Vector2(.52f,.82f),new Vector2(.49f,.70f),new Vector2(.43f,.64f),new Vector2(.38f,.70f),new Vector2(-.38f,.70f)};
  const int segments=384;var tool=new SurfaceTool();tool.Begin(Mesh.PrimitiveType.Triangles);
  void Vertex(int angle,int cross){int j=(cross+section.Length)%section.Length;float theta=Mathf.Tau*angle/segments;var p=section[j];var tangent=section[(j+1)%section.Length]-section[(j-1+section.Length)%section.Length];float r=p.Y*radius;
   // Physical channels in the wet contact crown, with angled cross channels; dry compounds retain a slick crown.
   if(wet&&Mathf.Abs(p.X)<.40f){float phase=Mathf.PosMod(angle+Mathf.Abs(p.X)*48f,16f);if(phase<2)r-=.005f;}
   var normal=new Vector3(-tangent.Y/width,tangent.X*Mathf.Cos(theta)/radius,tangent.X*Mathf.Sin(theta)/radius).Normalized();tool.SetNormal(normal);tool.SetUV(new Vector2(angle/(float)segments,j/(float)section.Length));tool.AddVertex(new Vector3(p.X*width,r*Mathf.Cos(theta),r*Mathf.Sin(theta)));
  }
  for(int i=0;i<segments;i++)for(int j=0;j<section.Length;j++){Vertex(i,j);Vertex(i,j+1);Vertex(i+1,j);Vertex(i+1,j);Vertex(i,j+1);Vertex(i+1,j+1);}return tool.Commit();
 }
 public static MeshInstance3D Create(float radius,float width,bool wet){var part=new MeshInstance3D {Name="RebuiltRubberTyre",Mesh=Build(radius,width,wet),MaterialOverride=new StandardMaterial3D {AlbedoColor=new Color(.025f,.027f,.029f),Roughness=.88f,Metallic=0,MetallicSpecular=.22f,CullMode=BaseMaterial3D.CullModeEnum.Disabled}};part.SetMeta("radius",radius);part.SetMeta("width",width);part.SetMeta("wet",wet);return part;}
 public static void SetWet(MeshInstance3D part,bool wet){if(part.GetMeta("wet").AsBool()==wet)return;part.Mesh=Build(part.GetMeta("radius").AsSingle(),part.GetMeta("width").AsSingle(),wet);part.SetMeta("wet",wet);}
}
}
