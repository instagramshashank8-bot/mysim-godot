using Godot;
using MySimulator.Rebuild;
using System.Collections.Generic;
namespace MySim {
// Opt-in art candidate. Source geometry/materials are untouched; no lights are invented in daylight.
// Mounts require actual drive-surface support and clearance from every route segment.
public partial class TrackNightLighting : Node3D {
 public int TrackIndex;
 readonly List<(Vector3 mount,Vector3 aim,float offset,float chainage,float side)> lamps=new();
 readonly List<SpotLight3D> pool=new();
 readonly List<int> assignments=new();
 readonly List<object> selectionSamples=new();
 readonly List<object> placementAttempts=new();
 readonly Dictionary<string,int> rejectionReasons=new();
 readonly List<object> routeCoverage=new();
 float routeLength,longestUnlitGap,nearestStartStation=-1;
 bool built;float updateAge,auditAge;int rejectedMounts;string auditPath;
 static float Read(System.Text.Json.JsonElement p,string key)=>p.GetProperty(key).GetSingle();
 static float EnvFloat(string key,float fallback)=>float.TryParse(System.Environment.GetEnvironmentVariable(key),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out float parsed)?parsed:fallback;
 public override void _PhysicsProcess(double delta) {
  if(!built){built=true;Build();}
  updateAge+=(float)delta;auditAge+=(float)delta;if(updateAge<.1f)return;updateAge=0;
  var camera=GetViewport().GetCamera3D();if(camera==null)return;
  var origin=camera.GlobalPosition;var forward=-camera.GlobalBasis.Z;forward.Y=0;forward=forward.Normalized();
  var nearby=new List<int>();for(int i=0;i<lamps.Count;i++)if(origin.DistanceSquaredTo(lamps[i].mount)<180f*180f)nearby.Add(i);
  nearby.Sort((a,b)=>origin.DistanceSquaredTo(lamps[a].mount).CompareTo(origin.DistanceSquaredTo(lamps[b].mount)));
  // Retain two local light sources for the car/contact shadows. Spend the remaining bounded
  // budget down the visible road instead of selecting an equal number of lamps behind it.
  var desired=new List<int>();var shadowStations=new HashSet<int>();
  for(int i=0;i<System.Math.Min(2,nearby.Count);i++){desired.Add(nearby[i]);shadowStations.Add(nearby[i]);}
  float Alignment(int id){var d=lamps[id].aim-origin;d.Y=0;return d.LengthSquared()>.001f?forward.Dot(d.Normalized()):1f;}
  float Score(int id){float facing=Alignment(id);float score=origin.DistanceTo(lamps[id].mount)*(facing<-.1f?3f:1f+.6f*(1f-facing));return assignments.Contains(id)?score*.85f:score;}
  nearby.Sort((a,b)=>Score(a).CompareTo(Score(b)));
  foreach(int id in nearby){if(desired.Count>=pool.Count)break;if(!desired.Contains(id))desired.Add(id);}
  // Keep a selected station in its existing slot so light/shadow ownership does not reshuffle
  // whenever two distances cross. The score hysteresis also limits boundary flicker.
  for(int i=0;i<assignments.Count;i++)if(!desired.Contains(assignments[i]))assignments[i]=-1;
  foreach(int id in desired)if(!assignments.Contains(id)){int slot=assignments.IndexOf(-1);if(slot>=0)assignments[slot]=id;}
  var selected=new List<object>();
  for(int i=0;i<pool.Count;i++){int id=assignments[i];var light=pool[i];light.Visible=id>=0;if(!light.Visible)continue;var station=lamps[id];light.GlobalPosition=station.mount;light.LookAt(station.aim,Vector3.Up);light.ShadowEnabled=shadowStations.Contains(id);selected.Add(new{slot=i,station=id,distance=origin.DistanceTo(station.mount),frontAlignment=Alignment(id),frontFacing=Alignment(id)>=0,shadow=light.ShadowEnabled});}
  if(auditPath!=null&&selectionSamples.Count<16&&(selectionSamples.Count==0||auditAge>=1f)){auditAge=0;selectionSamples.Add(new{camera=new[]{origin.X,origin.Y,origin.Z},forward=new[]{forward.X,forward.Y,forward.Z},selected});WriteAudit();}
 }
 void Build(){
  // One controlled night candidate: a pool-energy multiplier. Source profile placement values are untouched;
  // the gain multiplies both the spot-light energy and the visible emitter strips so the heads stay consistent
  // with the light they represent. Default unset = 1 = exact current behaviour.
  float poolGain=EnvFloat("MYSIM_NIGHT_POOL_GAIN",1f);
  using var document=System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString("res://data/track_floodlights.json"));var p=document.RootElement;
  float spacing=Read(p,"spacing"),height=Read(p,"height"),range=Read(p,"range");var offsets=new List<float>();
  foreach(var candidate in p.GetProperty("routeOffsets").EnumerateArray()){float offset=candidate.GetSingle();if(!float.IsFinite(offset)||offset<9||offset>24||offsets.Contains(offset))throw new System.InvalidOperationException("Invalid floodlight roadside offset");offsets.Add(offset);}
  if(spacing<20||height<8||range<height||offsets.Count==0||offsets.Count>6)throw new System.InvalidOperationException("Invalid floodlight placement profile");
  var route=new Circuit(TrackIndex);var space=GetWorld3D().DirectSpaceState;float walked=0,next=0;int rejected=0;
  var supportedChainages=new List<float>();
  void Reject(string reason,float chainage,float side,float offset){rejectionReasons.TryGetValue(reason,out int count);rejectionReasons[reason]=count+1;placementAttempts.Add(new{chainage,side,offset,accepted=false,reason});}
  var steel=new StandardMaterial3D{AlbedoColor=new Color(.16f,.18f,.20f),Metallic=.7f,Roughness=.5f};
  var emitter=new StandardMaterial3D{AlbedoColor=new Color(.78f,.85f,.92f),EmissionEnabled=true,Emission=new Color(.78f,.85f,.92f),EmissionEnergyMultiplier=2f*poolGain,Roughness=.4f};
  for(int segment=0;segment<route.points.Length;segment++){
   var a=route.points[segment];var b=route.points[(segment+1)%route.points.Length];float length=(b-a).Magnitude;if(length<.001f)continue;
   while(next<walked+length){float chainage=next,t=(next-walked)/length;next+=spacing;var centre=a+(b-a)*t;var right=(b-a).Normalized.Right;int siteMounts=0;
    var centreGround=Contact.Ground(space,new Vector3(centre.x,0,centre.z),0);if(!centreGround.found){rejected+=2;Reject("route-ground-missing",chainage,-1,0);Reject("route-ground-missing",chainage,1,0);routeCoverage.Add(new{chainage,mounts=0});continue;}
    foreach(float side in new[]{-1f,1f}){
     bool mounted=false;
     foreach(float offset in offsets){
     var pos=centre+right*(offset*side);route.Find(pos,out float lateral);if(Mathf.Abs(lateral)<route.width*.5f+2f){Reject("route-clearance",chainage,side,offset);continue;}
     var ground=Contact.Ground(space,new Vector3(pos.x,centreGround.rawY,pos.z),centreGround.rawY);
     if(!ground.found){Reject("mount-ground-missing",chainage,side,offset);continue;}
     if(Mathf.Abs(ground.rawY-centreGround.rawY)>3f){Reject("elevation-mismatch",chainage,side,offset);continue;}
     if(ground.normal.Y<.85f){Reject("steep-ground",chainage,side,offset);continue;}
     var bottom=new Vector3(pos.x,ground.rawY,pos.z);var top=bottom+Vector3.Up*height;
     using var clearanceShape=new CylinderShape3D{Height=height+.4f,Radius=.7f};
     using var check=new PhysicsShapeQueryParameters3D{Shape=clearanceShape,Transform=new Transform3D(Basis.Identity,bottom+Vector3.Up*(height*.5f+.2f)),CollisionMask=2,Margin=.05f};
     if(space.IntersectShape(check,1).Count>0){Reject("source-structure-obstruction",chainage,side,offset);continue;}
     var mast=new StaticBody3D{Name="FloodlightMast_"+lamps.Count,CollisionLayer=2,CollisionMask=0,Position=bottom};AddChild(mast);
     mast.AddChild(new CollisionShape3D{Position=Vector3.Up*(height*.5f),Shape=new CylinderShape3D{Height=height,Radius=.13f}});
     mast.AddChild(new MeshInstance3D{Position=Vector3.Up*(height*.5f),Mesh=new CylinderMesh{Height=height,TopRadius=.09f,BottomRadius=.13f,RadialSegments=8},MaterialOverride=steel});
     var head=new MeshInstance3D{Position=Vector3.Up*height,Mesh=new BoxMesh{Size=new Vector3(1.1f,.18f,.65f)},MaterialOverride=steel};mast.AddChild(head);
     mast.AddChild(new MeshInstance3D{Position=Vector3.Up*(height-.10f),Mesh=new BoxMesh{Size=new Vector3(.95f,.025f,.5f)},MaterialOverride=emitter});
     lamps.Add((top-Vector3.Up*.15f,new Vector3(centre.x,centreGround.rawY,centre.z),offset,chainage,side));
     placementAttempts.Add(new{chainage,side,offset,accepted=true,reason="supported-clear-mount"});mounted=true;siteMounts++;break;
     }
     if(!mounted)rejected++;
    }
    routeCoverage.Add(new{chainage,mounts=siteMounts});if(siteMounts>0)supportedChainages.Add(chainage);
   }walked+=length;
  }
  routeLength=walked;longestUnlitGap=routeLength;
  if(supportedChainages.Count>0){longestUnlitGap=0;nearestStartStation=routeLength;for(int i=0;i<supportedChainages.Count;i++){float here=supportedChainages[i],nextStation=i+1<supportedChainages.Count?supportedChainages[i+1]:supportedChainages[0]+routeLength;longestUnlitGap=Mathf.Max(longestUnlitGap,nextStation-here);nearestStartStation=Mathf.Min(nearestStartStation,Mathf.Min(here,routeLength-here));}}
  int budget=p.GetProperty("activeLights").GetInt32();if(budget<2||budget>12)throw new System.InvalidOperationException("Floodlight pool must remain bounded");
  for(int i=0;i<budget;i++){var light=new SpotLight3D{Visible=false,SpotRange=range,SpotAngle=Read(p,"coneAngle"),SpotAttenuation=1f,LightEnergy=Read(p,"energy")*poolGain,LightColor=new Color(.82f,.89f,1f),ShadowEnabled=i<2,ShadowBias=.03f,ShadowNormalBias=1f,DistanceFadeEnabled=true,DistanceFadeBegin=110f,DistanceFadeLength=45f};AddChild(light);pool.Add(light);assignments.Add(-1);}
  GD.Print("TRACK_FLOODLIGHTS candidate track=",TrackIndex," supportedMounts=",lamps.Count," rejected=",rejected," activeBudget=",pool.Count," poolGain=",poolGain," longestStationGap=",longestUnlitGap," nearestStartStation=",nearestStartStation," rejectReasons=",System.Text.Json.JsonSerializer.Serialize(rejectionReasons));
  rejectedMounts=rejected;auditPath=System.Environment.GetEnvironmentVariable("MYSIM_TRACK_LIGHT_AUDIT");WriteAudit();
 }
 void WriteAudit(){if(auditPath==null)return;var folder=System.IO.Path.GetDirectoryName(auditPath);if(!string.IsNullOrEmpty(folder))System.IO.Directory.CreateDirectory(folder);var stations=new List<object>();foreach(var lamp in lamps)stations.Add(new{mount=new[]{lamp.mount.X,lamp.mount.Y,lamp.mount.Z},aim=new[]{lamp.aim.X,lamp.aim.Y,lamp.aim.Z},lamp.offset,lamp.chainage,lamp.side});System.IO.File.WriteAllText(auditPath,System.Text.Json.JsonSerializer.Serialize(new{track=TrackIndex,candidate=true,poolGain=EnvFloat("MYSIM_NIGHT_POOL_GAIN",1f),supportedMounts=lamps.Count,rejected=rejectedMounts,rejectionReasons,routeLength,longestStationGap=longestUnlitGap,nearestStartStation,routeCoverage,placementAttempts,activeBudget=pool.Count,selectionPolicy="two-nearby-plus-forward-weighted-hysteresis",stations,selectionSamples,scope="Ray-supported procedural candidate. Station gaps measure placement, not lux/illumination; requires full track night/shadow/clearance review before promotion"}));}
}
}
