using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
namespace MySim {
// Explicit reconstruction experiment: mount separate source templates, rather
// than delete low-lying geometry or change tyre/contact acceptance thresholds.
public static class RecoveredComponentMounts {
 public static void Apply(Node3D car,int index){
  if(System.Environment.GetEnvironmentVariable("MYSIM_COMPONENT_MOUNTS")!="1")return;
  using var profiles=JsonDocument.Parse(FileAccess.GetFileAsString("res://data/component_mount_profiles.json"));
  var pts=Json.ParseString(FileAccess.GetFileAsString("res://assets/car_pivots.json")).AsGodotDictionary()[index.ToString()].AsGodotArray();
  var inverse=car.GlobalTransform.AffineInverse();var rows=new List<object>();var nodes=car.FindChildren("*","MeshInstance3D",true,false);
  var groups=new Dictionary<string,List<MeshInstance3D>>();foreach(var node in nodes){var mesh=(MeshInstance3D)node;if(!mesh.IsVisibleInTree()||mesh.Mesh==null)continue;string key=mesh.Name.ToString().ToUpperInvariant();int material=key.IndexOf("_MM_",StringComparison.Ordinal);if(material>=0)key=key.Substring(0,material);if(!groups.ContainsKey(key))groups[key]=new();groups[key].Add(mesh);}
  foreach(var group in groups){string name=group.Key;var mesh=group.Value[0];
   bool caliper=name.StartsWith("LOD_A_BRAKE_CALIPER_"),guard=name.StartsWith("LOD_A_WHEELGUARD_"),steering=name.StartsWith("LOD_A_STEERING_WHEEL");if(!caliper&&!guard&&!steering)continue;
   var before=inverse*mesh.GlobalTransform;var bounds=before*mesh.GetAabb();foreach(var surface in group.Value)bounds=bounds.Merge((inverse*surface.GlobalTransform)*surface.GetAabb());Vector3 target;int hub=-1;
   if(steering){var position=profiles.RootElement.GetProperty("steeringWheel").GetProperty(index>=15&&index<=18?"openWheel":"closedCar");target=new Vector3(position[0].GetSingle(),position[1].GetSingle(),position[2].GetSingle());}
   else{bool rear=name.Contains("_REAR"),left=name.Contains("_LEFT");if(!left&&!name.Contains("_RIGHT"))throw new Exception("Ambiguous wheel component side: "+name);hub=(rear?2:0)+(left?0:1);var point=pts[hub].AsGodotArray();target=new Vector3(point[0].AsSingle(),point[1].AsSingle(),point[2].AsSingle());
    MeshInstance3D tyre=null;foreach(var candidate in nodes){var m=(MeshInstance3D)candidate;string n=m.Name.ToString().ToUpperInvariant();if(n.StartsWith("LOD_A_TYRE")&&!n.Contains("BLUR")&&n.Contains("REAR")==rear){tyre=m;break;}}if(tyre==null)foreach(var candidate in nodes){var m=(MeshInstance3D)candidate;if(m.Name.ToString().ToUpperInvariant().StartsWith("LOD_A_TYRE")&&!m.Name.ToString().Contains("BLUR")){tyre=m;break;}}
    if(tyre==null)throw new Exception("Cannot mount recovered wheel component without source tyre dimensions");var tyreBox=(inverse*tyre.GlobalTransform)*tyre.GetAabb();float radius=Mathf.Max(tyreBox.Size.Y,tyreBox.Size.Z)*.5f,width=tyreBox.Size.X;if(radius<.15f||radius>.6f||width<.1f||width>.6f)throw new Exception("Invalid source wheel component mounting dimensions");
    var profile=profiles.RootElement.GetProperty(caliper?"caliper":"wheelguard");target.X-=Mathf.Sign(target.X)*width*profile.GetProperty("inboardTyreWidth").GetSingle();if(caliper)target.Z-=radius*profile.GetProperty("rearwardRadius").GetSingle();else target.Y+=radius*profile.GetProperty("aboveHubRadius").GetSingle();
   }
   var offset=target-bounds.GetCenter();foreach(var surface in group.Value){surface.SetMeta("original_component_transform",surface.Transform);surface.SetMeta("reconstructed_component_mount",true);surface.SetMeta("mounted_hub_index",hub);surface.GlobalTransform=new Transform3D(surface.GlobalBasis,surface.GlobalPosition+car.GlobalBasis*offset);}rows.Add(new {part=name,hub,originalCentre=new[]{bounds.GetCenter().X,bounds.GetCenter().Y,bounds.GetCenter().Z},mountedCentre=new[]{target.X,target.Y,target.Z},scope="Authored experimental pose; geometry and UV preserved"});
  }nodes.Clear();if(rows.Count==0)throw new Exception("Component mounting experiment selected no source parts");GD.Print("RECONSTRUCTED_COMPONENT_MOUNTS car=",index," parts=",rows.Count);
  string report=System.Environment.GetEnvironmentVariable("MYSIM_COMPONENT_MOUNT_REPORT");if(report!=null)System.IO.File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new {car=index,rows},new JsonSerializerOptions {WriteIndented=true}));
 }
}
}
