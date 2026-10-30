using Godot;
using System;
using System.Collections.Generic;
namespace MySim {
// Read-only assembly evidence; never hides, moves, rewrites UVs or replaces a source part.
public static class CarAssemblyAudit {
 public static void Write(Node3D car,int index,string path){
  var rows=new List<object>();var children=car.FindChildren("*","MeshInstance3D",true,false);
  foreach(var child in children){var mesh=(MeshInstance3D)child;if(mesh.Mesh==null)continue;var box=(car.GlobalTransform.AffineInverse()*mesh.GlobalTransform)*mesh.GetAabb();var materials=new List<object>();
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){var material=mesh.GetActiveMaterial(surface);var shader=material as ShaderMaterial;var texture=shader?.GetShaderParameter("base").As<Texture2D>();materials.Add(new {surface,source=material!=null&&material.HasMeta("source_material_guid")?material.GetMeta("source_material_guid").AsString():null,texture=texture?.ResourcePath,materialType=material?.GetType().Name});}
   var blendShapes=new List<object>();if(mesh.Mesh is ArrayMesh am)for(int i=0;i<am.GetBlendShapeCount();i++)blendShapes.Add(new {name=am.GetBlendShapeName(i).ToString(),value=mesh.GetBlendShapeValue(i)});
   rows.Add(new {name=mesh.Name.ToString(),path=car.GetPathTo(mesh).ToString(),visible=mesh.IsVisibleInTree(),localPosition=new[]{mesh.Position.X,mesh.Position.Y,mesh.Position.Z},centre=new[]{box.GetCenter().X,box.GetCenter().Y,box.GetCenter().Z},size=new[]{box.Size.X,box.Size.Y,box.Size.Z},materials,blendShapes});
  }children.Clear();System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new {car=index,scope="Read-only imported assembly and material evidence; not geometry acceptance",rows},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));GD.Print("PASS read-only car assembly evidence ",rows.Count);
 }
}
}
