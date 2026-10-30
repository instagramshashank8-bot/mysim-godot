using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
namespace MySim {
// Read-only world-space geometry evidence. Duplicate candidates are never hidden/deleted.
public static class TrackSceneAudit {
 public static void Write(List<Node3D> layers,int track,string destination){
  var groups=new Dictionary<string,List<object>>();var lamps=new List<object>();int meshes=0;
  foreach(var layer in layers){using var children=layer.FindChildren("*","MeshInstance3D",true,false);foreach(var node in children){
   var mesh=(MeshInstance3D)node;if(mesh.Mesh==null||!mesh.IsVisibleInTree())continue;meshes++;
   using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);int vertices=0,indices=0;var materials=new List<string>();
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){
    using var arrays=mesh.Mesh.SurfaceGetArrays(surface);var points=(Vector3[])arrays[(int)Mesh.ArrayType.Vertex];var triangles=(int[])arrays[(int)Mesh.ArrayType.Index];vertices+=points.Length;indices+=triangles?.Length??0;
    using var bytes=new MemoryStream();using(var writer=new BinaryWriter(bytes,System.Text.Encoding.UTF8,true)){
     writer.Write(points.Length);foreach(var point in points){var world=mesh.GlobalTransform*point;writer.Write((float)Math.Round(world.X,4));writer.Write((float)Math.Round(world.Y,4));writer.Write((float)Math.Round(world.Z,4));}
     writer.Write(triangles?.Length??0);if(triangles!=null)foreach(int index in triangles)writer.Write(index);
    }hash.AppendData(bytes.ToArray());materials.Add(mesh.GetActiveMaterial(surface)?.ResourceName??"");
   }
   string key=Convert.ToHexString(hash.GetHashAndReset());var bounds=mesh.GlobalTransform*mesh.GetAabb();
   var record=new {path=mesh.GetPath().ToString(),name=mesh.Name.ToString(),vertices,indices,materials,centre=new[]{bounds.GetCenter().X,bounds.GetCenter().Y,bounds.GetCenter().Z},size=new[]{bounds.Size.X,bounds.Size.Y,bounds.Size.Z}};
   if(!groups.ContainsKey(key))groups[key]=new List<object>();groups[key].Add(record);
   string label=mesh.Name.ToString().ToLowerInvariant();if(label.Contains("lamp")||label.Contains("flood")||label.Contains("light"))lamps.Add(record);
  }
  }
  var duplicates=new List<object>();foreach(var pair in groups)if(pair.Value.Count>1)duplicates.Add(new {worldGeometryHash=pair.Key,instances=pair.Value});
  Directory.CreateDirectory(Path.GetDirectoryName(destination));System.IO.File.WriteAllText(destination,System.Text.Json.JsonSerializer.Serialize(new {track,visibleMeshes=meshes,duplicateGroups=duplicates,lightingNameCandidates=lamps,scope="Read-only geometry candidates quantized0.1mm; intentional overlays/LOD require manual review. No automatic hiding, deduplication or collision change."},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
  GD.Print("PASS read-only track scene audit meshes=",meshes," duplicateGroups=",duplicates.Count," lightingCandidates=",lamps.Count);
 }
}
}
