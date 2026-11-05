using Godot;
using System;
using System.Collections.Generic;
namespace MySim {
// Source-geometry axle-placement candidate. Preserve wheelbase/track/radii; never rewrite source meshes or pivots file.
public static class SourceAxleFitCandidate {
 // Strut-derived shared axle translation without moving anything; null when the car's source
 // geometry does not support the candidate (same role/coverage/range/spread guards as Apply).
 public static float? ComputeShift(Node3D car,Node3D[] wheels){
  if(car==null||wheels==null||wheels.Length!=4)return null;
  var inverse=car.GlobalTransform.AffineInverse();float sum=0,min=float.PositiveInfinity,max=float.NegativeInfinity;
  for(int hub=0;hub<4;hub++){
   string axle=hub<2?"FRONT":"REAR",side=hub%2==0?"LEFT":"RIGHT",role="LOD_A_STRUT_LOWER_"+axle+"_"+side;
   var points=new System.Collections.Generic.List<Vector3>();float outer=0;
   foreach(var node in car.FindChildren("*","MeshInstance3D",true,false)){
    var mesh=(MeshInstance3D)node;if(!mesh.IsVisibleInTree()||mesh.Mesh==null||!mesh.Name.ToString().StartsWith(role))continue;var transform=inverse*mesh.GlobalTransform;
    for(int si=0;si<mesh.Mesh.GetSurfaceCount();si++){using var arrays=mesh.Mesh.SurfaceGetArrays(si);foreach(var vertex in arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array()){var p=transform*vertex;points.Add(p);outer=Mathf.Max(outer,Mathf.Abs(p.X));}}
   }
   if(points.Count==0||outer<.2f)return null;
   Vector3 end=Vector3.Zero;int count=0;foreach(var p in points)if(Mathf.Abs(p.X)>=outer-.015f){end+=p;count++;}end/=count;
   float delta=end.Z-wheels[hub].Position.Z;if(!float.IsFinite(delta)||Mathf.Abs(delta)>1.2f)return null;
   sum+=delta;min=Mathf.Min(min,delta);max=Mathf.Max(max,delta);
  }
  if(max-min>.65f)return null;
  return sum/4f;
 }
 public static void Apply(Node3D car,Node3D[] wheels,int index){
  if(System.Environment.GetEnvironmentVariable("MYSIM_SOURCE_AXLE_FIT")!="1")return;
  if(index<15||index>18)throw new InvalidOperationException("Source axle candidate currently requires reviewed F1 source roles");
  float shift=ComputeShift(car,wheels)??throw new InvalidOperationException("Source axle candidate could not compute a consistent shift from the source geometry");
  var inverse=car.GlobalTransform.AffineInverse();var rows=new List<object>();var deltas=new float[4];
  for(int hub=0;hub<4;hub++){
   string axle=hub<2?"FRONT":"REAR",side=hub%2==0?"LEFT":"RIGHT";string role="LOD_A_STRUT_LOWER_"+axle+"_"+side;var points=new List<Vector3>();float outer=0;
   foreach(var node in car.FindChildren("*","MeshInstance3D",true,false)){
    var mesh=(MeshInstance3D)node;if(!mesh.IsVisibleInTree()||mesh.Mesh==null||!mesh.Name.ToString().StartsWith(role))continue;var transform=inverse*mesh.GlobalTransform;
    for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){using var arrays=mesh.Mesh.SurfaceGetArrays(surface);foreach(var vertex in arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array()){var p=transform*vertex;points.Add(p);outer=Mathf.Max(outer,Mathf.Abs(p.X));}}
   }
   if(points.Count==0||outer<.2f)throw new InvalidOperationException("Source lower suspension attachment role missing: "+role);
   Vector3 end=Vector3.Zero;int count=0;foreach(var p in points)if(Mathf.Abs(p.X)>=outer-.015f){end+=p;count++;}end/=count;
   float delta=end.Z-wheels[hub].Position.Z;if(!float.IsFinite(delta)||Mathf.Abs(delta)>1.2f)throw new InvalidOperationException("Source axle correction outside diagnostic bound");
   deltas[hub]=delta;
   rows.Add(new {hub,sourceRole=role,outerAttachment=new[]{end.X,end.Y,end.Z},samples=count,proposedLongitudinalDelta=delta});
  }
  float spread=0;foreach(var d in deltas)spread=Mathf.Max(spread,Mathf.Abs(d-shift));
  foreach(var wheel in wheels)wheel.Position+=Vector3.Back*shift;
  // Mount reconstruction precedes axle fitting; these intact templates have
  // not yet been reparented to BrakeAssembly. Keep their hub-relative pose.
  foreach(var node in car.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;if(mesh.HasMeta("mounted_hub_index")&&mesh.GetMeta("mounted_hub_index").AsInt32()>=0)mesh.GlobalPosition+=car.GlobalBasis*(Vector3.Back*shift);
  }
  GD.Print("SOURCE_AXLE_FIT car=",index," longitudinalShift=",shift," source-derived candidate; original wheelbase/track/radii preserved");
  if(System.Environment.GetEnvironmentVariable("MYSIM_SOURCE_AXLE_REPORT") is string path)System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new {car=index,longitudinalShift=shift,proposalSpread=spread,rows,scope="Proposed shared axle translation from actual lower-strut outer vertex clusters; not manufacturer calibration or clearance acceptance"}));
 }
}
}
