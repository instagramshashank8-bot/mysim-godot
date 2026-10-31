using Godot;
using System;
namespace MySim {
// Independent engine geometry invariant: Godot front faces are clockwise, opposite outward vertex normals.
public static class GeneratedWheelGeometryChecks {
 static int Check(Mesh mesh){int triangles=0;for(int surface=0;surface<mesh.GetSurfaceCount();surface++){using var arrays=mesh.SurfaceGetArrays(surface);var vertices=(Vector3[])arrays[(int)Mesh.ArrayType.Vertex];var normals=(Vector3[])arrays[(int)Mesh.ArrayType.Normal];var indices=(int[])arrays[(int)Mesh.ArrayType.Index];int count=indices?.Length??vertices.Length;for(int i=0;i+2<count;i+=3){int a=indices==null?i:indices[i],b=indices==null?i+1:indices[i+1],c=indices==null?i+2:indices[i+2];var cross=(vertices[b]-vertices[a]).Cross(vertices[c]-vertices[a]);if(cross.LengthSquared()<1e-14f)continue;float facing=cross.Normalized().Dot((normals[a]+normals[b]+normals[c]).Normalized());if(facing>0.001f)throw new Exception("Generated wheel triangle faces inward relative to engine winding: surface="+surface+" triangle="+(i/3));triangles++;}}if(triangles==0)throw new Exception("Generated wheel has no valid triangles");return triangles;}
 public static void Run(Node owner){int triangles=0;foreach(bool wet in new[]{false,true}){using var mesh=TyreMeshBuilder.Build(.33f,.3f,wet);triangles+=Check(mesh);}for(int design=0;design<3;design++){var rim=WheelRimBuilder.Build(.2f,.3f,design);owner.AddChild(rim);var nodes=rim.FindChildren("*","MeshInstance3D",true,false);foreach(var node in nodes)triangles+=Check(((MeshInstance3D)node).Mesh);nodes.Clear();rim.QueueFree();}GD.Print("PASS generated tyre/rim outward normals and clockwise winding triangles=",triangles);}
}
}
