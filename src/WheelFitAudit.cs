using Godot;
using System;
using System.Collections.Generic;
namespace MySim {
// Read-only fit evidence: actual body triangle samples in each steered wheel frame.
// A detected penetration is actionable; no detected sample is NOT a clearance certificate.
public static class WheelFitAudit {
 static List<Vector2> DrySection(Node3D spin){
  var mesh=spin.GetNodeOrNull<MeshInstance3D>("RebuiltRubberTyre");if(mesh==null||mesh.GetMeta("wet").AsBool())throw new InvalidOperationException("Exact wheel fit requires generated dry tyre geometry");
  var ordered=new SortedDictionary<float,Vector2>();using var arrays=mesh.Mesh.SurfaceGetArrays(0);var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();var uv=arrays[(int)Mesh.ArrayType.TexUV].AsVector2Array();
  for(int i=0;i<vertices.Length;i++)if(Mathf.Abs(uv[i].X)<.000001f){var point=mesh.Transform*vertices[i];ordered[uv[i].Y]=new Vector2(point.X,Mathf.Sqrt(point.Y*point.Y+point.Z*point.Z));}
  if(ordered.Count<8)throw new InvalidOperationException("Dry tyre cross-section extraction failed");return new List<Vector2>(ordered.Values);
 }
 static float MaterialDepth(Vector3 point,List<Vector2> section){
  var p=new Vector2(point.X,Mathf.Sqrt(point.Y*point.Y+point.Z*point.Z));bool inside=false;float distance=float.PositiveInfinity;
  for(int i=0,j=section.Count-1;i<section.Count;j=i++){
   var a=section[i];var b=section[j];if((a.Y>p.Y)!=(b.Y>p.Y)&&p.X<(b.X-a.X)*(p.Y-a.Y)/(b.Y-a.Y)+a.X)inside=!inside;
   var edge=b-a;float t=Mathf.Clamp((p-a).Dot(edge)/Mathf.Max(edge.LengthSquared(),.00000001f),0,1);distance=Mathf.Min(distance,p.DistanceTo(a+edge*t));
  }return inside?distance:-distance;
 }
 public static void Write(Node3D car,Node3D[] wheels,int index,string path){
  var rows=new List<object>();
  foreach(var wheel in wheels){
   float radius=wheel.GetMeta("radius").AsSingle();var spin=wheel.GetNode<Node3D>("Spin");Aabb? tyre=null;
   foreach(var child in spin.GetChildren())if(child is MeshInstance3D part&&part.Visible&&part.Mesh!=null){string n=part.Name.ToString().ToUpperInvariant();if(!n.Contains("TYRE")&&!n.Contains("TIRE"))continue;var box=part.Transform*part.GetAabb();tyre=tyre.HasValue?tyre.Value.Merge(box):box;}
   if(!tyre.HasValue)throw new InvalidOperationException("Wheel fit audit has no visible tyre");
   var bounds=tyre.Value;var centre=bounds.GetCenter();float halfWidth=bounds.Size.X*.5f;var parts=new List<object>();var section=DrySection(spin);
   foreach(var child in car.FindChildren("*","MeshInstance3D",true,false)){
    var mesh=(MeshInstance3D)child;string name=mesh.Name.ToString().ToUpperInvariant();bool rebuiltWishbone=mesh.GetParent()?.Name.ToString()=="RebuiltFrontDoubleWishbones";if(!mesh.IsVisibleInTree()||mesh.Mesh==null||(!name.StartsWith("LOD_A_")&&!rebuiltWishbone))continue;// Rubber/rim meshes rotate with Spin and are the reference envelope.
    // Fixed hub-mounted guards/calipers must still be tested: skipping the
    // entire hub silently omitted exactly the parts that can cut into tyres.
    bool wheelPart=false;foreach(var hub in wheels)if(hub.GetNode<Node3D>("Spin").IsAncestorOf(mesh)){wheelPart=true;break;}if(wheelPart)continue;
    var transform=spin.GlobalTransform.AffineInverse()*mesh.GlobalTransform;int samples=0,inside=0;float deepest=0;
    void Probe(Vector3 p){samples++;float depth=MaterialDepth(p,section);if(depth>.001f){inside++;deepest=Mathf.Max(deepest,depth);}}
    for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){
     using var arrays=mesh.Mesh.SurfaceGetArrays(surface);var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();var indices=arrays[(int)Mesh.ArrayType.Index].VariantType==Variant.Type.Nil?Array.Empty<int>():arrays[(int)Mesh.ArrayType.Index].AsInt32Array();int count=indices.Length>0?indices.Length:vertices.Length;
     for(int t=0;t+2<count;t+=3){var a=transform*vertices[indices.Length>0?indices[t]:t];var b=transform*vertices[indices.Length>0?indices[t+1]:t+1];var c=transform*vertices[indices.Length>0?indices[t+2]:t+2];for(int u=0;u<=4;u++)for(int v=0;v<=4-u;v++)Probe(a+(b-a)*(u/4f)+(c-a)*(v/4f));}
    }
    if(inside>0)parts.Add(new {part=mesh.Name.ToString(),samples,inside,deepest});
   }
   float minimumWorldY=float.PositiveInfinity,maximumWorldY=float.NegativeInfinity;int rubberVertices=0;
   foreach(var child in spin.GetChildren())if(child is MeshInstance3D rubber&&rubber.IsVisibleInTree()&&rubber.Name=="RebuiltRubberTyre"){
    for(int surface=0;surface<rubber.Mesh.GetSurfaceCount();surface++){using var arrays=rubber.Mesh.SurfaceGetArrays(surface);foreach(var vertex in arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array()){var world=rubber.GlobalTransform*vertex;minimumWorldY=Mathf.Min(minimumWorldY,world.Y);maximumWorldY=Mathf.Max(maximumWorldY,world.Y);rubberVertices++;}}
   }
   if(rubberVertices==0||!float.IsFinite(minimumWorldY))throw new InvalidOperationException("Actual rubber support extrema missing");
   rows.Add(new {rubberSupport=new{minimumWorldY,maximumWorldY,rubberVertices,scope="Actual transformed rubber vertices relative to preview floorY0; not loaded gameplay contact"},wheel=wheel.Name.ToString(),hub=new[]{wheel.Position.X,wheel.Position.Y,wheel.Position.Z},visualHubInCarFrame=new[]{(car.GlobalTransform.AffineInverse()*spin.GlobalPosition).X,(car.GlobalTransform.AffineInverse()*spin.GlobalPosition).Y,(car.GlobalTransform.AffineInverse()*spin.GlobalPosition).Z},kingpinRecipe=wheel.HasMeta("kingpin_recipe")?wheel.GetMeta("kingpin_recipe").AsString():"Source extrema-cluster candidate; joint identity unproven",sourceKingpin=wheel.HasMeta("source_kingpin_axis")?new{lower=wheel.GetMeta("source_kingpin_lower").AsVector3().ToString(),upper=wheel.GetMeta("source_kingpin_upper").AsVector3().ToString(),axis=wheel.GetMeta("source_kingpin_axis").AsVector3().ToString(),scope="Visual-only candidate steering axis; native tyre contact hub/radius unchanged"}:null,yaw=wheel.Rotation.Y,radius,width=2*halfWidth,tyreCentreOffset=new[]{centre.X,centre.Y,centre.Z},dryRubberPenetrationSamples=parts});
  }
  object sweep=System.Environment.GetEnvironmentVariable("MYSIM_WHEEL_CLEARANCE_SWEEP")=="1"?Sweep(car,wheels,index):null;
  System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new {car=index,scope="Body and fixed hub-mounted source-component triangle samples against actual generated dry tyre cross-section, excluding rotating wheel meshes, hub void and rounded-sidewall gaps; positive samples only, not exhaustive triangle or swept-motion certification",rows,sweep},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));GD.Print("PASS wheel fit evidence");
 }
 static float[] SweepValues(string name,string fallback,float min,float max){
  string text=System.Environment.GetEnvironmentVariable(name)??fallback;var parts=text.Split(',');if(parts.Length==0||parts.Length>12)throw new InvalidOperationException("Clearance sweep grid size invalid: "+name);
  var values=new float[parts.Length];for(int i=0;i<parts.Length;i++){if(!float.TryParse(parts[i],System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out values[i])||!float.IsFinite(values[i])||values[i]<min||values[i]>max)throw new InvalidOperationException("Clearance sweep value out of range: "+name+"="+parts[i]);}return values;
 }
 static string[] SweepModes(){
  string text=System.Environment.GetEnvironmentVariable("MYSIM_WHEEL_CLEARANCE_MODES");if(string.IsNullOrEmpty(text))return new[]{"front_both","rear_both","front_roll","rear_roll","diag","FL","FR","RL","RR"};
  var parts=text.Split(',');if(parts.Length>12)throw new InvalidOperationException("Clearance sweep mode grid too large");
  foreach(var mode in parts)if(mode!="front_both"&&mode!="rear_both"&&mode!="front_roll"&&mode!="rear_roll"&&mode!="diag"&&mode!="FL"&&mode!="FR"&&mode!="RL"&&mode!="RR")throw new InvalidOperationException("Unknown clearance sweep mode: "+mode);
  return parts;
 }
 static string PartCategory(string name){
  if(name.Contains("WING")||name.Contains("SPOILER"))return"wing";
  if(name.Contains("FLOOR")||name.Contains("DIFFUSER")||name.Contains("SKID"))return"floor";
  if(name.Contains("CALIPER")||name.Contains("BRAKE")||name.Contains("DISC")||name.Contains("ROTOR"))return"brakes";
  if(name.Contains("GUARD"))return"wheelguard";
  if(name.Contains("STRUT")||name.Contains("WISHBONE")||name.Contains("ARM")||name.Contains("SUSPENSION"))return"suspension";
  if(name.Contains("BODY")||name.Contains("CHASSIS")||name.Contains("BUMPER")||name.Contains("DOOR")||name.Contains("FENDER")||name.Contains("SIDEP")||name.Contains("NOSE")||name.Contains("HOOD")||name.Contains("BOOT")||name.Contains("COWL"))return"bodywork";
  return"other";
 }
 static readonly string[] SweepCategories={"wing","floor","brakes","suspension","bodywork","wheelguard","other"};
 // Stationary body sample cloud in car space with one obstacle category per part, captured once and
 // reused for every pose (same mesh filter and barycentric sampling as Write; rotating wheel meshes
 // stay excluded).
 static System.Collections.Generic.List<(string Name,string Category,System.Collections.Generic.List<Vector3> Points)> BodySampleCloud(Node3D car,Node3D[] wheels){
  var inverse=car.GlobalTransform.AffineInverse();var cloud=new System.Collections.Generic.List<(string,string,System.Collections.Generic.List<Vector3>)>();
  foreach(var node in car.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;string name=mesh.Name.ToString().ToUpperInvariant();bool rebuiltWishbone=mesh.GetParent()?.Name.ToString()=="RebuiltFrontDoubleWishbones";
   if(!mesh.IsVisibleInTree()||mesh.Mesh==null||(!name.StartsWith("LOD_A_")&&!rebuiltWishbone))continue;
   bool wheelPart=false;foreach(var hub in wheels)if(hub.GetNode<Node3D>("Spin").IsAncestorOf(mesh)){wheelPart=true;break;}if(wheelPart)continue;
   var toCar=inverse*mesh.GlobalTransform;var samples=new System.Collections.Generic.List<Vector3>();
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){
    using var arrays=mesh.Mesh.SurfaceGetArrays(surface);var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();var indices=arrays[(int)Mesh.ArrayType.Index].VariantType==Variant.Type.Nil?Array.Empty<int>():arrays[(int)Mesh.ArrayType.Index].AsInt32Array();int count=indices.Length>0?indices.Length:vertices.Length;
    for(int t=0;t+2<count;t+=3){var a=toCar*vertices[indices.Length>0?indices[t]:t];var b=toCar*vertices[indices.Length>0?indices[t+1]:t+1];var c=toCar*vertices[indices.Length>0?indices[t+2]:t+2];for(int u=0;u<=4;u++)for(int v=0;v<=4-u;v++)samples.Add(a+(b-a)*(u/4f)+(c-a)*(v/4f));}
   }
   if(samples.Count>0)cloud.Add((mesh.Name.ToString(),PartCategory(name),samples));
  }
  return cloud;
 }
 // Lowest point of the wheel's VISIBLE tyre geometry in spin-local coordinates: the actual contact
 // point of the visual wheel. Transformed per pose so every record ties the visual wheel transform to
 // the physical contact for that pose.
 static Vector3 TyreLowestLocal(Node3D spin){
  Vector3 best=Vector3.Zero;bool found=false;
  foreach(var child in spin.GetChildren())if(child is MeshInstance3D mesh&&mesh.Visible&&mesh.Mesh!=null){
   string name=mesh.Name.ToString().ToUpperInvariant();if(!name.Contains("TYRE")&&!name.Contains("TIRE"))continue;
   var toSpin=mesh.Transform;
   for(int surface=0;surface<mesh.Mesh.GetSurfaceCount();surface++){
    using var arrays=mesh.Mesh.SurfaceGetArrays(surface);var vertices=arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
    foreach(var vertex in vertices){var local=toSpin*vertex;if(!found||local.Y<best.Y){best=local;found=true;}}
   }
  }
  if(!found)throw new InvalidOperationException("Clearance sweep wheel has no visible tyre geometry for the contact link");
  return best;
 }
 // Opt-in swept-clearance fixture v2 (MYSIM_WHEEL_CLEARANCE_SWEEP=1). Poses are parameterized by the
 // ACTUAL suspension state of NativeSuspensionVehicle: spring length in [0 (full bump), RestLength+0.15
 // (full droop)] applied as the wheel-centre offset (RestLength-StaticSag)-spring — the same transform
 // the physics applies to its wheel shapes — so every pose IS a reachable suspension state and the
 // recorded visual contact point is linked to that state. Steering spans the full standstill lock
 // (Dynamics.SteeringLock(0)=0.55rad=31.5deg). Wheel-vs-{wing,bodywork,floor,brakes,suspension,
 // wheelguard,other} worst clearances are labelled per wheel per state and aggregated per wheel.
 // Evidence only, not a clearance certificate; the pre-sweep pose is restored and verified.
 static object Sweep(Node3D car,Node3D[] wheels,int index){
  const float staticDrop=0.11f,maxDroop=0.33f; // NativeSuspensionVehicle: RestLength-StaticSag; RestLength+0.15
  float[] steers=SweepValues("MYSIM_WHEEL_CLEARANCE_STEERS","-31.5,-15.75,0,15.75,31.5",-45f,45f);
  float[] springs=SweepValues("MYSIM_WHEEL_CLEARANCE_SPRINGS","0,0.055,0.11,0.165,0.22,0.275,0.33",0f,maxDroop);
  string[] modes=SweepModes();
  var sections=new List<System.Collections.Generic.List<Vector2>>();var lowest=new Vector3[wheels.Length];var rest=new Vector3[wheels.Length];
  var snapshots=new List<System.Collections.Generic.KeyValuePair<Node3D,Transform3D>>();
  for(int i=0;i<wheels.Length;i++){
   rest[i]=wheels[i].Position;sections.Add(DrySection(wheels[i].GetNode<Node3D>("Spin")));lowest[i]=TyreLowestLocal(wheels[i].GetNode<Node3D>("Spin"));
   snapshots.Add(new System.Collections.Generic.KeyValuePair<Node3D,Transform3D>(wheels[i],wheels[i].Transform));
   foreach(var child in wheels[i].GetChildren())if(child is Node3D node)snapshots.Add(new System.Collections.Generic.KeyValuePair<Node3D,Transform3D>(node,node.Transform));
  }
  var parts=BodySampleCloud(car,wheels);if(parts.Count==0)throw new InvalidOperationException("Clearance sweep found no stationary body geometry");
  var carTransform=car.GlobalTransform;var worldToCar=carTransform.AffineInverse();
  var staticContact=new Vector3[wheels.Length];
  for(int w=0;w<wheels.Length;w++)staticContact[w]=worldToCar*(wheels[w].GetNode<Node3D>("Spin").GlobalTransform*lowest[w]);
  var worstClearance=new float[wheels.Length,SweepCategories.Length];var worstPart=new string[wheels.Length,SweepCategories.Length];var worstSteer=new float[wheels.Length,SweepCategories.Length];var worstOffset=new float[wheels.Length,SweepCategories.Length];
  for(int w=0;w<wheels.Length;w++)for(int c=0;c<SweepCategories.Length;c++)worstClearance[w,c]=float.PositiveInfinity;
  var cache=new System.Collections.Generic.Dictionary<(int,float,float),object>();
  object EvaluateWheel(int w,float steerDegrees,float appliedOffset){
   var key=(w,steerDegrees,appliedOffset);if(cache.TryGetValue(key,out var cached))return cached;
   var spin=wheels[w].GetNode<Node3D>("Spin");var toSpin=spin.GlobalTransform.AffineInverse();
   var wheelCentre=worldToCar*spin.GlobalPosition;
   var contact=worldToCar*(spin.GlobalTransform*lowest[w]);
   float worst=float.NegativeInfinity;var offenders=new List<object>();int samplesInspected=0;
   var categoryWorst=new float[SweepCategories.Length];var categoryPart=new string[SweepCategories.Length];
   for(int c=0;c<SweepCategories.Length;c++)categoryWorst[c]=float.NegativeInfinity;
   foreach(var part in parts){
    float deepest=float.NegativeInfinity;
    foreach(var point in part.Item3){float depth=MaterialDepth(toSpin*(carTransform*point),sections[w]);if(depth>deepest)deepest=depth;}
    samplesInspected+=part.Item3.Count;
    int category=System.Array.IndexOf(SweepCategories,part.Item2);
    if(deepest>categoryWorst[category]){categoryWorst[category]=deepest;categoryPart[category]=part.Item1;}
    if(deepest>worst)worst=deepest;
    if(deepest>0f)offenders.Add(new{part=part.Item1,category=part.Item2,deepest});
   }
   var categories=new Dictionary<string,object>();
   for(int c=0;c<SweepCategories.Length;c++){
    bool has=categoryWorst[c]>float.NegativeInfinity;
    categories[SweepCategories[c]]=new{clearance=has?(float?)-categoryWorst[c]:null,part=categoryPart[c]};
    if(has&&-categoryWorst[c]<worstClearance[w,c]){worstClearance[w,c]=-categoryWorst[c];worstPart[w,c]=categoryPart[c];worstSteer[w,c]=steerDegrees;worstOffset[w,c]=appliedOffset;}
   }
   var row=new{wheel=wheels[w].Name.ToString(),hub=new[]{wheels[w].Position.X,wheels[w].Position.Y,wheels[w].Position.Z},wheelCentre=new[]{wheelCentre.X,wheelCentre.Y,wheelCentre.Z},contactPoint=new[]{contact.X,contact.Y,contact.Z},contactShift=new[]{contact.X-staticContact[w].X,contact.Z-staticContact[w].Z},groundGap=contact.Y,springLength=staticDrop-appliedOffset,wheelOffset=appliedOffset,clearance=-worst,penetratingParts=offenders.Count,offenders,categories,samplesInspected};
   cache[key]=row;return row;
  }
  var rows=new List<object>();int poses=0;int clampedCount=0;
  try{
   foreach(float steer in steers){
    for(int i=0;i<wheels.Length;i++)wheels[i].Position=rest[i];
    WheelAnimation.Update(wheels,0,Mathf.DegToRad(steer));
    var staticRows=new List<object>();for(int w=0;w<wheels.Length;w++)staticRows.Add(EvaluateWheel(w,steer,0f));
    rows.Add(new{steerDegrees=steer,mode="rest",springLength=staticDrop,wheels=staticRows});poses++;
    foreach(float spring in springs){
     if(Mathf.Abs(spring-staticDrop)<1e-6f)continue;
     float offset=staticDrop-spring;
     foreach(var mode in modes){
      var applied=new float[wheels.Length];
      if(mode=="front_both"){applied[0]=offset;applied[1]=offset;}
      else if(mode=="rear_both"){applied[2]=offset;applied[3]=offset;}
      else if(mode=="front_roll"){applied[0]=offset;applied[1]=-offset;}
      else if(mode=="rear_roll"){applied[2]=offset;applied[3]=-offset;}
      else if(mode=="diag"){applied[0]=offset;applied[3]=offset;applied[1]=-offset;applied[2]=-offset;}
      else if(mode=="FL")applied[0]=offset;
      else if(mode=="FR")applied[1]=offset;
      else if(mode=="RL")applied[2]=offset;
      else if(mode=="RR")applied[3]=offset;
      // Physics envelope: the bump stop limits the rise to +staticDrop; the run-41837315358 tables
      // recorded +0.22 offsets from negated mode branches - states the suspension cannot reach.
      for(int i=0;i<wheels.Length;i++){var wanted=applied[i];applied[i]=Mathf.Clamp(wanted,-(maxDroop-staticDrop),staticDrop);if(wanted!=applied[i])clampedCount++;}
      for(int i=0;i<wheels.Length;i++)wheels[i].Position=rest[i]+Vector3.Up*applied[i];
      WheelAnimation.Update(wheels,0,Mathf.DegToRad(steer));
      var wheelRows=new List<object>();for(int w=0;w<wheels.Length;w++)wheelRows.Add(EvaluateWheel(w,steer,applied[w]));
      rows.Add(new{steerDegrees=steer,mode=mode,springLength=spring,wheels=wheelRows});poses++;
     }
    }
   }
  }
  finally{
   for(int i=0;i<wheels.Length;i++)wheels[i].Position=rest[i];
   WheelAnimation.Update(wheels,0,0f);
   foreach(var snapshot in snapshots)if(!snapshot.Key.Transform.IsEqualApprox(snapshot.Value))snapshot.Key.Transform=snapshot.Value;
   foreach(var snapshot in snapshots)if(!snapshot.Key.Transform.IsEqualApprox(snapshot.Value))throw new InvalidOperationException("Clearance sweep failed to restore pose: "+snapshot.Key.Name);
  }
  var perWheel=new List<object>();float globalWorst=float.PositiveInfinity;string globalPart=null,globalWheel=null,globalCategory=null;float globalSteer=float.NaN,globalOffset=float.NaN;
  for(int w=0;w<wheels.Length;w++){
   var categories=new Dictionary<string,object>();
   for(int c=0;c<SweepCategories.Length;c++){
    bool has=worstPart[w,c]!=null;
    categories[SweepCategories[c]]=new{worstClearance=has?(float?)worstClearance[w,c]:null,part=worstPart[w,c],atSteerDegrees=has?(float?)worstSteer[w,c]:null,atWheelOffset=has?(float?)worstOffset[w,c]:null,atSpringLength=has?(float?)(staticDrop-worstOffset[w,c]):null};
    if(has){
     GD.Print("WHEEL_CLEARANCE_WORST car=",index," wheel=",wheels[w].Name," vs=",SweepCategories[c]," clearance=",worstClearance[w,c]," part=",worstPart[w,c]," atSteer=",worstSteer[w,c]," atWheelOffset=",worstOffset[w,c]);
     if(worstClearance[w,c]<globalWorst){globalWorst=worstClearance[w,c];globalPart=worstPart[w,c];globalWheel=wheels[w].Name.ToString();globalCategory=SweepCategories[c];globalSteer=worstSteer[w,c];globalOffset=worstOffset[w,c];}
    }
   }
   perWheel.Add(new{wheel=wheels[w].Name.ToString(),categories});
  }
  GD.Print("WHEEL_CLEARANCE_SWEEP_SUMMARY car=",index," poses=",poses," clampedStates=",clampedCount," worstClearance=",(globalPart!=null?(float?)globalWorst:null)," worstWheel=",globalWheel??"none"," worstVs=",globalCategory??"none"," worstPart=",globalPart??"none"," atSteer=",globalSteer," atWheelOffset=",globalOffset," pose restored; evidence only, not a clearance certificate");
  return new{car=index,steers,springs,modes,poses,clampedStates=clampedCount,bodyParts=parts.Count,perWheel,rows,scope="Full-lock and full-travel clearance sweep: poses parameterized by the actual suspension state (spring length 0..RestLength+0.15 of NativeSuspensionVehicle) applied through the real steering path; negated mode branches clamp at the bump stop (physics envelope), clampedStates counts them; per wheel labelled worst clearance vs wing/bodywork/floor/brakes/suspension/wheelguard/other; positive penetrations are findings, not acceptance"};
 }
}
}
