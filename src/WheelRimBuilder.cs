using Godot;
using System;
namespace MySim {
// Replacement asset candidate: hollow bead barrel, swept forged spokes and machined hub.
// Axis X; outer face -X, matching recovered tyre templates before per-side mirroring.
public static class WheelRimBuilder {
 static StandardMaterial3D Metal(Color colour,float roughness)=>new StandardMaterial3D {AlbedoColor=colour,Metallic=.9f,Roughness=roughness,MetallicSpecular=.5f,CullMode=BaseMaterial3D.CullModeEnum.Disabled};
 static void Vertex(SurfaceTool tool,Vector3 p,Vector3 n,Vector2 uv){tool.SetNormal(n);tool.SetUV(uv);tool.AddVertex(p);}
 static ArrayMesh Lathe(Vector2[] profile,int segments){
  var tool=new SurfaceTool();tool.Begin(Mesh.PrimitiveType.Triangles);
  void V(int circle,int point){int j=(point+profile.Length)%profile.Length;float angle=Mathf.Tau*circle/segments;var p=profile[j];var tangent=profile[(j+1)%profile.Length]-profile[(j-1+profile.Length)%profile.Length];var n=new Vector3(-tangent.Y,tangent.X*Mathf.Cos(angle),tangent.X*Mathf.Sin(angle)).Normalized();Vertex(tool,new Vector3(p.X,p.Y*Mathf.Cos(angle),p.Y*Mathf.Sin(angle)),n,new Vector2(circle/(float)segments,j/(float)profile.Length));}
  for(int i=0;i<segments;i++)for(int j=0;j<profile.Length;j++){V(i,j);V(i+1,j);V(i,j+1);V(i+1,j);V(i+1,j+1);V(i,j+1);}
  return tool.Commit();
 }
 static ArrayMesh Spoke(float radius,float width,float angle){
  const int steps=20,sides=10;var tool=new SurfaceTool();tool.Begin(Mesh.PrimitiveType.Triangles);
  void V(int along,int around){float t=along/(float)steps,theta=angle+.16f*t*t,phi=Mathf.Tau*around/sides;
   float distance=Mathf.Lerp(radius*.19f,radius*.96f,t),halfWidth=Mathf.Lerp(radius*.062f,radius*.034f,t),halfDepth=radius*.027f;
   var tangent=new Vector3(0,-Mathf.Sin(theta),Mathf.Cos(theta));var centre=new Vector3(-width*.42f+(1-t)*radius*.10f,distance*Mathf.Cos(theta),distance*Mathf.Sin(theta));
   var position=centre+Vector3.Right*(halfDepth*Mathf.Cos(phi))+tangent*(halfWidth*Mathf.Sin(phi));var normal=(Vector3.Right*(Mathf.Cos(phi)/halfDepth)+tangent*(Mathf.Sin(phi)/halfWidth)).Normalized();Vertex(tool,position,normal,new Vector2(t,around/(float)sides));
  }
  for(int i=0;i<steps;i++)for(int j=0;j<sides;j++){V(i,j);V(i+1,j);V(i,j+1);V(i+1,j);V(i+1,j+1);V(i,j+1);}
  return tool.Commit();
 }
 public static Node3D Build(float radius,float tyreWidth,int design){
  if(!float.IsFinite(radius)||radius<.1f||radius>.45f||tyreWidth<.1f||tyreWidth>.7f)throw new ArgumentOutOfRangeException("Recovered wheel dimensions invalid");
  var root=new Node3D {Name="RebuiltForgedRim"};float width=tyreWidth*.8f;var metal=Metal(new Color(.24f,.27f,.30f),.24f);var edge=Metal(new Color(.48f,.51f,.54f),.2f);var hubMetal=Metal(new Color(.12f,.14f,.16f),.35f);
  void Add(string name,Mesh mesh,Material material){root.AddChild(new MeshInstance3D {Name=name,Mesh=mesh,MaterialOverride=material});}
  var profile=new[]{new Vector2(-width*.48f,radius*.93f),new Vector2(-width*.48f,radius*.99f),new Vector2(-width*.46f,radius*1.008f),new Vector2(-width*.43f,radius),new Vector2(-width*.40f,radius*.96f),new Vector2(width*.40f,radius*.96f),new Vector2(width*.43f,radius),new Vector2(width*.46f,radius*1.008f),new Vector2(width*.48f,radius*.99f),new Vector2(width*.48f,radius*.93f)};
  Add("HollowBeadBarrel",Lathe(profile,192),edge);
  int pairs=design==0?5:design==1?7:10;
  for(int i=0;i<pairs;i++)foreach(float offset in design==2?new[]{0f}:new[]{-.045f,.045f})Add("ForgedSpoke"+i+"_"+(offset<0?"A":"B"),Spoke(radius,width,Mathf.Tau*i/pairs+offset),metal);
  var hubProfile=new[]{new Vector2(-width*.46f,radius*.075f),new Vector2(-width*.48f,radius*.1f),new Vector2(-width*.48f,radius*.18f),new Vector2(-width*.44f,radius*.23f),new Vector2(-width*.35f,radius*.23f),new Vector2(-width*.35f,radius*.075f)};
  Add("MachinedHub",Lathe(hubProfile,128),hubMetal);
  int nuts=design==2?1:5;for(int i=0;i<nuts;i++){float angle=Mathf.Tau*i/5;root.AddChild(new MeshInstance3D {Name="LugNut"+i,Mesh=new CylinderMesh {TopRadius=radius*(design==2?.085f:.026f),BottomRadius=radius*(design==2?.085f:.026f),Height=radius*.055f,RadialSegments=6},MaterialOverride=edge,Position=new Vector3(-width*.49f,(design==2?0:radius*.145f*Mathf.Cos(angle)),(design==2?0:radius*.145f*Mathf.Sin(angle))),Rotation=new Vector3(0,0,Mathf.Pi*.5f)});}
  root.SetMeta("outer_face_axis",-1);root.SetMeta("rim_radius",radius);root.SetMeta("tyre_width",tyreWidth);return root;
 }
}
}
