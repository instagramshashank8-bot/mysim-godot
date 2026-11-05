using Godot;
using MySimulator.Rebuild;
using System.Collections.Generic;
namespace MySim {
// Opt-in art candidate. Source geometry/materials are untouched; no lights are invented in daylight.
// Mounts require actual drive-surface support and clearance from every route segment.
public partial class TrackNightLighting : Node3D {
 public int TrackIndex;
 readonly List<(Vector3 mount,Vector3 aim,float offset,float chainage,float side,string zone)> lamps=new();
 readonly List<SpotLight3D> pool=new();
 readonly List<int> assignments=new();
 readonly List<object> selectionSamples=new();
 readonly List<object> placementAttempts=new();
 readonly Dictionary<string,int> rejectionReasons=new();
 readonly List<object> routeCoverage=new();
 float routeLength,longestUnlitGap,nearestStartStation=-1;
 bool built;float updateAge,auditAge;int rejectedMounts;string auditPath;string placedScheme="uniform";
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
  // Placement scheme: unset/"uniform" = the historical 38 m grid (bit-identical); "anchored" = the source-anchored
  // pitlane/braking/apex family layout proposed by the reviewer directive. Any other value is rejected loudly.
  string lightScheme=System.Environment.GetEnvironmentVariable("MYSIM_LIGHT_SCHEME")??"uniform";
  if(lightScheme!="uniform"&&lightScheme!="anchored")throw new System.InvalidOperationException("Unknown light scheme: "+lightScheme);
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
  float Pole(Vector3 bottom,float height){var mast=new StaticBody3D{Name="FloodlightMast_"+lamps.Count,CollisionLayer=2,CollisionMask=0,Position=bottom};AddChild(mast);
   mast.AddChild(new CollisionShape3D{Position=Vector3.Up*(height*.5f),Shape=new CylinderShape3D{Height=height,Radius=.13f}});
   mast.AddChild(new MeshInstance3D{Position=Vector3.Up*(height*.5f),Mesh=new CylinderMesh{Height=height,TopRadius=.09f,BottomRadius=.13f,RadialSegments=8},MaterialOverride=steel});
   mast.AddChild(new MeshInstance3D{Position=Vector3.Up*height,Mesh=new BoxMesh{Size=new Vector3(1.1f,.18f,.65f)},MaterialOverride=steel});
   mast.AddChild(new MeshInstance3D{Position=Vector3.Up*(height-.10f),Mesh=new BoxMesh{Size=new Vector3(.95f,.025f,.5f)},MaterialOverride=emitter});
   return height;}
  if(lightScheme=="anchored"){walked=BuildAnchoredLamps(route,space,Pole,Reject);}
  else for(int segment=0;segment<route.points.Length;segment++){
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
     lamps.Add((top-Vector3.Up*.15f,new Vector3(centre.x,centreGround.rawY,centre.z),offset,chainage,side,"uniform"));
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
  GD.Print("TRACK_FLOODLIGHTS candidate track=",TrackIndex," scheme=",lightScheme," supportedMounts=",lamps.Count," rejected=",rejected," activeBudget=",pool.Count," poolGain=",poolGain," longestStationGap=",longestUnlitGap," nearestStartStation=",nearestStartStation," rejectReasons=",System.Text.Json.JsonSerializer.Serialize(rejectionReasons));
  rejectedMounts=rejected;auditPath=System.Environment.GetEnvironmentVariable("MYSIM_TRACK_LIGHT_AUDIT");placedScheme=lightScheme;WriteAudit();
  NightVehicleEmissive();
 }
 // VEHICLE-LIGHT EMISSION candidate (MYSIM_VEHICLE_LIGHTS=emissive): the car prefabs' "lights" materials are
 // forced EmissionEnabled=false at build (CarLoader.Build), so at night every car's lamps read as dead glass -
 // the reviewer's night-visibility gap for OTHER cars. This night-only pass re-enables emission PER MESH with a
 // role colour decided from the mesh's position within its car (front = white headlight, rear = red taillight);
 // meshes are identified by the car texture convention (albedo ends "_lights.png"). Material-level only: no new
 // light nodes, so the floodlight pool budget and the anchored scheme are untouched (the cooperation rule:
 // emission = zero light-budget cost). Brake modulation and real beam lights = later tiers, not here.
 void NightVehicleEmissive(){
  var modeText=System.Environment.GetEnvironmentVariable("MYSIM_VEHICLE_LIGHTS");
  if(modeText!="emissive")return;
  Node root=GetParent();int vehicles=0;var vehicleMeshes=new List<MeshInstance3D>();
  foreach(var node in root.FindChildren("*","MeshInstance3D",true,false)){
   var mesh=(MeshInstance3D)node;
   bool lightsTex=false;
   for(int s=0;s<mesh.Mesh.GetSurfaceCount();s++){
    var texPath=mesh.GetActiveMaterial(s)?.AlbedoTexture?.ResourcePath??mesh.Mesh.SurfaceGetMaterial(s)?.AlbedoTexture?.ResourcePath??"";
    if(texPath.EndsWith("_lights.png",System.StringComparison.OrdinalIgnoreCase)){lightsTex=true;break;}
   }
   if(!lightsTex)continue;
   // car root = the depth-1 ancestor (a direct child of the world) whose total mesh count is small (a car, not a track layer)
   Node car=null;var walk=node.GetParent();
   while(walk!=null){var parent=walk.GetParent();if(parent!=null&&parent==root)break;walk=walk.GetParent();}
   if(walk==null)continue;
   int meshCount=0;float sizeOk=0f;
   foreach(var m2 in walk.FindChildren("*","MeshInstance3D",true,false)){var ab=m2.GetAabb();sizeOk=Mathf.Max(sizeOk,Mathf.Max(ab.Size.X,Mathf.Max(ab.Size.Y,ab.Size.Z)));meshCount++;if(meshCount>400)break;}
   if(meshCount>400||sizeOk>30f)continue;
   vehicleMeshes.Add(mesh);vehicles++;
  }
  int front=0,rear=0;
  foreach(var mesh in vehicleMeshes){
   var worldAb=mesh.GetAabb();var center=mesh.GlobalTransform*worldAb.GetCenter();
   // car forward = +Z basis (the body's forward convention); front = mesh centre forward of the car's mesh centroid
   float carZ=0f;int n=0;
   foreach(var other in vehicleMeshes){var oc=other.GlobalTransform*other.GetAabb().GetCenter();carZ+=oc.z;n++;}
   var carMeanZ=carZ/Mathf.Max(1,n);
   bool isFront=center.z>carMeanZ;
   for(int s=0;s<mesh.Mesh.GetSurfaceCount();s++){
    var mat=mesh.GetActiveMaterial(s) as StandardMaterial3D ?? mesh.Mesh.SurfaceGetMaterial(s) as StandardMaterial3D;
    var texPath=mat?.AlbedoTexture?.ResourcePath??"";
    if(!texPath.EndsWith("_lights.png",System.StringComparison.OrdinalIgnoreCase))continue;
    var clone=(StandardMaterial3D)mat.Duplicate();
    clone.EmissionEnabled=true;
    clone.Emission=isFront?new Color(.85f,.88f,.92f):new Color(.85f,.08f,.06f);
    clone.EmissionEnergyMultiplier=2.5f;
    mesh.SetSurfaceOverrideMaterial(s,clone);
    if(isFront)front++;else rear++;
   }
  }
  GD.Print("NIGHT_VEHICLE_LIGHTS emissive vehicles=",vehicles," frontSurfaces=",front," rearSurfaces=",rear," poolBudgetUntouched=true");
 }
 // ANCHORED placement scheme (MYSIM_LIGHT_SCHEME=anchored): pit/brake/apex pole families derived from the route
 // geometry (the real pit route + per-segment curvature). All positions provisional art (geometric derivation,
 // not survey data). Every candidate passes the placement validator (outside the corridor, ground support, level
 // ground, unobstructed, >=14 m separation, sane aim declination); rejections recorded per zone. SCALE-ONLY
 // (no rotation: these FBX nodes carry vertex-baked geometry at the node origin far from any pivot).
 float BuildAnchoredLamps(Circuit route,PhysicsDirectSpaceState3D space,Func<Vector3,float,float> pole,Action<string,float,float,float> reject){
  var count=route.points.Length;
  var chain=new float[count+1];var turnRate=new float[count];
  for(int i=0;i<count;i++){
   var j=(i+1)%count;
   var step=(route.points[j]-route.points[i]).Magnitude;
   chain[i+1]=chain[i]+step;
   var k=(j+1)%count;
   var h0=Mathf.Atan2(route.points[j].z-route.points[i].z,route.points[j].x-route.points[i].x);
   var h1=Mathf.Atan2(route.points[k].z-route.points[j].z,route.points[k].x-route.points[j].x);
   turnRate[i]=Mathf.Wrap(h1-h0,-Mathf.Pi,Mathf.Pi)/Mathf.Max(.5f,step);
  }
  var apexes=new List<(int seg,float tv)>();
  var inCorner=false;var cornerBest=0;
  for(int i=0;i<count;i++){
   var sharp=Mathf.Abs(turnRate[i])>=0.007f;
   if(sharp&&!inCorner){inCorner=true;cornerBest=i;}
   else if(sharp&&inCorner&&Mathf.Abs(turnRate[i])>Mathf.Abs(turnRate[cornerBest])){cornerBest=i;}
   if(inCorner&&(!sharp||i==count-1)){apexes.Add((cornerBest,turnRate[cornerBest]));inCorner=false;}
  }
  int zonesPit=0,zonesBrake=0,zonesApex=0;
  int TryPlace(Vector3 mountXZ,Vector3 aimXZ,float height,string zone,float chainage,float side){
   route.Find(new Vec(mountXZ.x,mountXZ.z),out float mountLat);
   if(Mathf.Abs(mountLat)<route.width*.5f+2.5f){reject("mount-inside-corridor",chainage,side,0);return -1;}
   var ground=Contact.Ground(space,new Vector3(mountXZ.x,0,mountXZ.z),0);
   if(!ground.found){reject("mount-ground-missing",chainage,side,0);return -1;}
   if(ground.normal.Y<.85f){reject("steep-ground",chainage,side,0);return -1;}
   var mount=new Vector3(mountXZ.x,ground.rawY,mountXZ.z);
   var aimGround=Contact.Ground(space,new Vector3(aimXZ.x,ground.rawY,aimXZ.z),mount.y);
   if(!aimGround.found){reject("aim-ground-missing",chainage,side,0);return -1;}
   foreach(var other in lamps)if(other.mount.DistanceTo(mount)<14f){reject("station-too-close",chainage,side,0);return -1;}
   using var clearanceShape=new CylinderShape3D{Height=height+.4f,Radius=.7f};
   using var check=new PhysicsShapeQueryParameters3D{Shape=clearanceShape,Transform=new Transform3D(Basis.Identity,mount+Vector3.Up*(height*.5f+.2f)),CollisionMask=2,Margin=.05f};
   if(space.IntersectShape(check,1).Count>0){reject("source-structure-obstruction",chainage,side,0);return -1;}
   var top=mount+Vector3.Up*height;
   var flat=new Vector3(aimXZ.x,0,aimXZ.z)-new Vector3(top.x,0,top.z);
   var decl=Mathf.RadToDeg(Mathf.Atan2(height,Mathf.Max(.5f,flat.Length())));
   if(decl<12f||decl>70f){reject("aim-declination-out",chainage,side,0);return -1;}
   pole(mount,height);
   lamps.Add((top-Vector3.Up*.15f,new Vector3(aimXZ.x,aimGround.rawY+.4f,aimXZ.z),0,chainage,side,zone));
   placementAttempts.Add(new{chainage,side,zone,accepted=true,reason="anchored/"+zone});
   routeCoverage.Add(new{chainage,mounts=1});
   return 0;
  }
  var pitStep=Mathf.Max(1,route.PitRoute.Length/10);
  for(int i=0;i<route.PitRoute.Length;i+=pitStep){
   var pp=route.PitRoute[i];
   var qprog=route.Find(pp,out float pitLat);
   var q=route.Point(qprog);
   var outX=pp.x-q.x;var outZ=pp.z-q.z;var ol=Mathf.Sqrt(outX*outX+outZ*outZ);
   if(ol<.001f)continue;outX/=ol;outZ/=ol;
   if(TryPlace(new Vector3(pp.x+outX*3.5f,0,pp.z+outZ*3.5f),new Vector3(pp.x,0,pp.z),8.5f,"pit",qprog*route.length,pitLat>=0?1f:-1f)==0)zonesPit++;
  }
  var segMean=route.length/count;
  foreach(var apex in apexes){
   var segA=apex.seg;
   var jn=(segA+1)%count;
   var dx=route.points[jn].x-route.points[segA].x;var dz=route.points[jn].z-route.points[segA].z;
   var dl=Mathf.Sqrt(dx*dx+dz*dz);if(dl<.001f)continue;dx/=dl;dz/=dl;
   var rightX=dz;var rightZ=-dx;
   var inside=Mathf.Sign(apex.tv);
   var apexX=route.points[segA].x+rightX*inside*2f;var apexZ=route.points[segA].z+rightZ*inside*2f;
   var vApex=Mathf.Sqrt(8.5f/Mathf.Max(.004f,Mathf.Abs(apex.tv)));
   var brakeDist=Mathf.Clamp(vApex*vApex/16f,25f,110f);
   var brakePoint=route.Point(Mathf.Wrap01(segA-brakeDist/segMean));
   var out2X=-inside*rightX;var out2Z=-inside*rightZ;
   var exitPoint=route.Point(Mathf.Wrap01((segA+4f)/count));
   if(TryPlace(new Vector3(apexX+out2X*10.5f,0,apexZ+out2Z*10.5f),new Vector3(exitPoint.x,0,exitPoint.z),15f,"apex",chain[segA],-inside)==0)zonesApex++;
   var aimBrake=route.Point(Mathf.Wrap01((segA*1f-brakeDist*.35f/segMean)/count));
   if(TryPlace(new Vector3(brakePoint.x+out2X*10.5f,0,brakePoint.z+out2Z*10.5f),new Vector3(aimBrake.x,0,aimBrake.z),15f,"brake",chain[segA]-brakeDist,-inside)==0)zonesBrake++;
  }
  GD.Print("TRACK_FLOODLIGHTS anchored zones: pit=",zonesPit," brake=",zonesBrake," apex=",zonesApex," corners=",apexes.Count);
  return route.length;
 }
 void WriteAudit(){if(auditPath==null)return;var folder=System.IO.Path.GetDirectoryName(auditPath);if(!string.IsNullOrEmpty(folder))System.IO.Directory.CreateDirectory(folder);var stations=new List<object>();foreach(var lamp in lamps)stations.Add(new{mount=new[]{lamp.mount.X,lamp.mount.Y,lamp.mount.Z},aim=new[]{lamp.aim.X,lamp.aim.Y,lamp.aim.Z},lamp.offset,lamp.chainage,lamp.side,zone=lamp.zone});System.IO.File.WriteAllText(auditPath,System.Text.Json.JsonSerializer.Serialize(new{track=TrackIndex,candidate=true,scheme=placedScheme,poolGain=EnvFloat("MYSIM_NIGHT_POOL_GAIN",1f),supportedMounts=lamps.Count,rejected=rejectedMounts,rejectionReasons,routeLength,longestStationGap=longestUnlitGap,nearestStartStation,routeCoverage,placementAttempts,activeBudget=pool.Count,selectionPolicy="two-nearby-plus-forward-weighted-hysteresis",stations,selectionSamples,scope="Ray-supported placement candidate (provisional art, not real-circuit photometry). Station gaps measure placement, not lux/illumination; requires full track night/shadow/clearance review before promotion"}));}
}
}
