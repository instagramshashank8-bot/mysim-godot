using Godot;
using MySimulator.Rebuild;
using System;
namespace MySim {
// Experimental force-driven native rigid body. No pose snapping, height clamping or heading assignment.
// Not production-integrated until suspension, tyre/contact and real-track gates pass.
public partial class NativeSuspensionVehicle : RigidBody3D {
 public readonly NativeRoadSpeedPlanner RoadPlanning=new();
 readonly NativeContactReplayRecorder contactReplay=new();
 Vehicle raceVehicle;float emptyStoppedTime;public bool DamageEnabled;public readonly NativeImpactDamage Impacts=new();
 public void BindRaceControls(Vehicle vehicle,float dt,Circuit track=null){RoadPlanning.Step(vehicle,this,track,dt);raceVehicle=vehicle;Controls=GridHold||vehicle.servicing||vehicle.retired||vehicle.finished?new DriverInput(0,1,0):vehicle.input;TractionControl=vehicle.traction;BrakeStabilityAssist=vehicle.abs;if(GridHold||vehicle.servicing||vehicle.retired||vehicle.finished){HybridDriveForce=0;vehicle.ersActive=false;}else HybridDriveForce=HybridDeployment.Step(vehicle,vehicle.input,dt)*vehicle.input.throttle;}
 public bool GridHold;public bool RotationalTyres;public readonly float[] WheelOmega=new float[4],WheelRotation=new float[4],LongitudinalSlip=new float[4],LateralSlipAngle=new float[4];
 public void SeedRollingVelocity(Vector3 velocity){Impacts.ResetHistory();LinearVelocity=velocity;float speed=velocity.Dot(GlobalBasis.Z);for(int i=0;i<4;i++){WheelOmega[i]=speed/radii[i];LongitudinalSlip[i]=0;}}
 void FreeWheel(int i,DriverInput input,float engine,float dt){if(!RotationalTyres)return;float brake=(input.reverse?0:input.brake)*brakeDecel*Mass*(i<2?.29f:.21f)+(input.handbrake&&i>=2?Mass*1.5f:0);float inertia=1.6f*radii[i]*radii[i]/(.33f*.33f);RotationalTyreContact.Airborne(ref WheelOmega[i],radii[i],inertia,i>=2?engine*.5f:0,brake,dt);WheelRotation[i]=Mathf.Wrap(WheelRotation[i]+WheelOmega[i]*dt,-Mathf.Pi,Mathf.Pi);}
 readonly System.Collections.Generic.List<(CollisionShape3D Shape,Vector3 Point)> underfloorPoints=new();
 public object UnderfloorEvidence(Transform3D? physicsPose=null){if(underfloorPoints.Count==0)return null;int hits=0;float minimum=float.PositiveInfinity,maximum=float.NegativeInfinity;foreach(var sample in underfloorPoints){var world=(physicsPose??GlobalTransform)*sample.Shape.Transform*sample.Point;var ground=Contact.Ground(GetWorld3D().DirectSpaceState,world,world.Y);if(!ground.found)continue;float clearance=world.Y-ground.rawY;minimum=Mathf.Min(minimum,clearance);maximum=Mathf.Max(maximum,clearance);hits++;}return new {samples=underfloorPoints.Count,groundHits=hits,minimumClearance=hits>0?(float?)minimum:null,maximumClearance=hits>0?(float?)maximum:null,pitch=Rotation.X,roll=Rotation.Z,scope="Actual lowest recovered chassis, bumper and body convex vertices against real drive-surface rays; not a contact impulse or whole-body collision certificate"};}
 public object ImpactShapeEvidence(int shapeIndex,Transform3D pose){
  var owner=ShapeOwnerGetOwner(ShapeFindOwner(shapeIndex)) as CollisionShape3D;
  if(owner?.Shape is not ConvexPolygonShape3D convex)return null;
  int hits=0;float minimum=float.PositiveInfinity;Vector3 lowest=Vector3.Zero;var points=convex.Points;
  foreach(var vertex in points){var world=pose*owner.Transform*vertex;var ground=Contact.Ground(GetWorld3D().DirectSpaceState,world,world.Y);if(!ground.found)continue;hits++;float clearance=world.Y-ground.rawY;if(clearance<minimum){minimum=clearance;lowest=world;}}
  return new {part=owner.Name.ToString(),vertices=points.Length,groundHits=hits,margin=convex.Margin,minimumVertexClearance=hits>0?(float?)minimum:null,minimumVertexWorld=new[]{lowest.X,lowest.Y,lowest.Z},scope="All vertices of the actual struck convex shape at physics pose; vertex rays do not cover edge/face intersections or certify collision freedom"};
 }
 public object ImpactFaceEvidence(int shapeIndex,Transform3D previous,Transform3D current,Vector3 rawContact){
  var owner=ShapeOwnerGetOwner(ShapeFindOwner(shapeIndex)) as CollisionShape3D;if(owner?.Shape is not ConvexPolygonShape3D convex)return null;
  using var debug=convex.GetDebugMesh();var faces=debug.GetFaces();var phases=new System.Collections.Generic.List<object>();var space=GetWorld3D().DirectSpaceState;
  foreach(float fraction in new[]{0f,.5f,1f}){var pose=previous.InterpolateWith(current,fraction)*owner.Transform;int hits=0,samples=0;float minimum=float.PositiveInfinity;Vector3 minimumWorld=Vector3.Zero;
   void Probe(Vector3 local){samples++;var world=pose*local;var ground=Contact.Ground(space,world,world.Y);if(!ground.found)return;hits++;float clearance=world.Y-ground.rawY;if(clearance<minimum){minimum=clearance;minimumWorld=world;}}
   for(int i=0;i+2<faces.Length;i+=3){var a=faces[i];var b=faces[i+1];var c=faces[i+2];Probe((a+b+c)/3);Probe((a+b)/2);Probe((b+c)/2);Probe((c+a)/2);}
   float contactBottom=float.PositiveInfinity;int contactIntersections=0;
   for(int i=0;i+2<faces.Length;i+=3){var a=pose*faces[i];var b=pose*faces[i+1];var c=pose*faces[i+2];float d=(b.Z-c.Z)*(a.X-c.X)+(c.X-b.X)*(a.Z-c.Z);if(Mathf.Abs(d)<.0000001f)continue;
    float u=((b.Z-c.Z)*(rawContact.X-c.X)+(c.X-b.X)*(rawContact.Z-c.Z))/d;float v=((c.Z-a.Z)*(rawContact.X-c.X)+(a.X-c.X)*(rawContact.Z-c.Z))/d;float w=1-u-v;if(u<-.0001f||v<-.0001f||w<-.0001f)continue;contactIntersections++;contactBottom=Mathf.Min(contactBottom,u*a.Y+v*b.Y+w*c.Y);
   }
   var contactGround=Contact.Ground(space,rawContact,rawContact.Y);
   phases.Add(new {fraction,contactIntersections,contactColumnBottom=contactIntersections>0?(float?)contactBottom:null,contactGroundY=contactGround.found?(float?)contactGround.rawY:null,contactColumnClearance=contactIntersections>0&&contactGround.found?(float?)(contactBottom-contactGround.rawY):null,samples,groundHits=hits,minimumSampleClearance=hits>0?(float?)minimum:null,minimumWorld=new[]{minimumWorld.X,minimumWorld.Y,minimumWorld.Z}});
  }
  return new {part=owner.Name.ToString(),triangles=faces.Length/3,phases,scope="Actual convex face centroids and edge midpoints at previous/mid/current integration poses; includes exact vertical hull intersection at raw contact XZ interpreted as world; frame interpretation and continuous collision remain separate"};
 }
 public object RoadContactEvidence(Vector3 world){
  var rows=new System.Collections.Generic.List<object>();var space=GetWorld3D().DirectSpaceState;
  foreach(bool backfaces in new[]{false,true}){using var query=PhysicsRayQueryParameters3D.Create(world+Vector3.Up*.2f,world-Vector3.Up*2f,1);query.HitBackFaces=backfaces;var hit=space.IntersectRay(query);
   if(hit.Count==0){rows.Add(new {backfaces,found=false});continue;}var position=hit["position"].AsVector3();var normal=hit["normal"].AsVector3();int face=hit.ContainsKey("face_index")?hit["face_index"].AsInt32():-1;float? sourceCrossY=null;float[] sourceNormal=null;int triangles=0;string colliderName=null,colliderPath=null,shapeType=null;
   if(hit["collider"].AsGodotObject() is CollisionObject3D collider&&hit.ContainsKey("shape")){colliderName=collider.Name.ToString();colliderPath=collider.GetPath().ToString();var shapeNode=collider.ShapeOwnerGetOwner(collider.ShapeFindOwner(hit["shape"].AsInt32())) as CollisionShape3D;shapeType=shapeNode?.Shape?.GetType().Name??"none";if(shapeNode?.Shape is ConcavePolygonShape3D concave){var data=concave.Data;triangles=data.Length/3;if(face>=0&&face*3+2<data.Length){var basis=shapeNode.GlobalBasis;var a=basis*data[face*3];var b=basis*data[face*3+1];var c=basis*data[face*3+2];var cross=(b-a).Cross(c-a).Normalized();sourceCrossY=cross.Y;sourceNormal=new[]{cross.X,cross.Y,cross.Z};}}}
   rows.Add(new {backfaces,found=true,face,triangles,sourceCrossY,sourceNormal,normal=new[]{normal.X,normal.Y,normal.Z},position=new[]{position.X,position.Y,position.Z},normalY=normal.Y,groundY=position.Y,contactHeightAboveGround=world.Y-position.Y,colliderName,colliderPath,shapeType});hit.Clear();
  }
  return new {rows,scope="Actual source ground rays at raw contact interpreted in world coordinates; front/backface comparison only, source untouched"};
 }
 public int CarIndex;public float HybridDriveForce;public DriverInput Controls;public bool TractionControl=true,BrakeStabilityAssist=true;public float Water;public Compound Compound=Compound.Medium;
 public readonly System.Collections.Generic.List<(CollisionShape3D Shape,int Hub)> MountedShapes=new();
 public override void _PhysicsProcess(double delta){foreach(var entry in MountedShapes){int hub=entry.Hub;var centre=RecoveredWheelCentres[hub];var rotation=new Basis(Vector3.Up,hub<2?WheelAngle:0);var offset=Vector3.Up*(RestLength-StaticSag-SpringLength[hub]);entry.Shape.Transform=new Transform3D(rotation,centre+offset-rotation*centre);}}
 public string[] ContactParts=Array.Empty<string>(),ContactSurfaces=Array.Empty<string>();public int ChassisContacts;public readonly Vector3[] ContactForces=new Vector3[4],ContactNormals=new Vector3[4],ContactPoints=new Vector3[4];
 public readonly bool[] Grounded=new bool[4];public readonly float[] Loads=new float[4];public readonly float[] SpringLength=new float[4];
 public Vector3[] RecoveredWheelCentres;public float[] RecoveredWheelRadii;
 public float SuspensionPreload;float bumpStopTravel=float.PositiveInfinity,bumpStopRate;public float WheelRadius=.33f,RestLength=.18f,StaticSag=.07f,DampingRatio=.7f;public int Contacts;public float WheelAngle;
 public float TyreLoadRatio {get{float sum=0;foreach(float load in Loads)sum+=load;return sum/(Mass*9.81f);}}
 public Tyre[] Tyres=>tyres;
 Vector3[] mounts=new Vector3[4];float[] radii=new float[4];Tyre[] tyres={new Tyre(),new Tyre(),new Tyre(),new Tyre()};float power,drag,brakeDecel;
 public bool Initialized {get;private set;}
 public override void _Ready(){
  try{Configure();Initialized=true;}catch(Exception error){
   // Godot logs _Ready exceptions and otherwise keeps ticking a half-configured body.
   // Keep the invalid source/profile failure explicit; never drive with default zero power.
   Freeze=true;SetPhysicsProcess(false);GD.PushError("NATIVE_INITIALIZATION_FAILED car="+CarIndex+": "+error);CleanExit.Request(this,1);
  }
 }
 void Configure(){
  DamageEnabled|=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_DAMAGE")=="1";RotationalTyres=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_ROTATIONAL_TYRES")=="1";CarIndex=Math.Clamp(CarIndex,0,23);NativeCarColliders.ConfigureWheels(this,CarIndex);Mass=RecoveredVehicleProfiles.Mass[CarIndex];CanSleep=false;ContinuousCd=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_CCD")!="0";MaxContactsReported=16;LinearDamp=0;AngularDamp=.15f;CollisionLayer=4;CollisionMask=7;
  CenterOfMassMode=CenterOfMassModeEnum.Custom;CenterOfMass=new Vector3(0,-.3f,0);
  float wheelbase=RecoveredGeometry.Wheelbase[CarIndex],track=VehicleDimensions.Width[CarIndex]*.8f;
  if((RecoveredWheelCentres==null)!=(RecoveredWheelRadii==null)||RecoveredWheelCentres!=null&&(RecoveredWheelCentres.Length!=4||RecoveredWheelRadii.Length!=4))throw new InvalidOperationException("Recovered wheel contact configuration incomplete");
  for(int i=0;i<4;i++){radii[i]=RecoveredWheelRadii==null?WheelRadius:RecoveredWheelRadii[i];if(!float.IsFinite(radii[i])||radii[i]<.15f||radii[i]>.6f)throw new InvalidOperationException("Recovered wheel radius invalid");mounts[i]=RecoveredWheelCentres==null?new Vector3(i%2==0?-track*.5f:track*.5f,-.32f+RestLength-StaticSag,i<2?wheelbase*.5f:-wheelbase*.5f):RecoveredWheelCentres[i]+Vector3.Up*(RestLength-StaticSag);tyres[i].Replace(Compound);}
  if(!NativeCarColliders.Install(this,CarIndex)){AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(track,.5f,wheelbase+.5f)}});GD.Print("NATIVE_COLLISION_FIXTURE_PROXY car=",CarIndex);}
  if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_RIDE_HEIGHT")=="1"){
   using var setup=System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString("res://data/native_suspension_profile.json"));var settings=setup.RootElement;float clearance=settings.GetProperty("minimumStaticClearance").GetSingle(),maximum=settings.GetProperty("maximumPreload").GetSingle(),reserve=settings.GetProperty("bumpStopReserve").GetSingle();bumpStopRate=settings.GetProperty("bumpStopRateMultiplier").GetSingle();if(!float.IsFinite(clearance)||clearance<=0||clearance>.2f||!float.IsFinite(reserve)||reserve<=0||reserve>=clearance||!float.IsFinite(bumpStopRate)||bumpStopRate<1||bumpStopRate>100||!float.IsFinite(maximum)||maximum<=0||maximum>.2f)throw new Exception("Invalid experimental suspension profile");bumpStopTravel=clearance-reserve;
   string path="res://assets/generated_native_car_hulls/car-"+CarIndex.ToString("00")+".json";using var manifest=System.Text.Json.JsonDocument.Parse(FileAccess.GetFileAsString(path));float bottom=float.PositiveInfinity;foreach(var part in manifest.RootElement.GetProperty("partBounds").EnumerateArray()){string name=part.GetProperty("part").GetString();if(manifest.RootElement.GetProperty("mountedHubs").TryGetProperty(name,out _))continue;bottom=Mathf.Min(bottom,part.GetProperty("minimumY").GetSingle());}
   float tyreBottom=float.PositiveInfinity;for(int i=0;i<4;i++)tyreBottom=Mathf.Min(tyreBottom,RecoveredWheelCentres[i].Y-radii[i]);float sourceClearance=bottom-tyreBottom;SuspensionPreload=Mathf.Max(0,clearance-sourceClearance);if(!float.IsFinite(SuspensionPreload)||SuspensionPreload>maximum)throw new Exception("Source clearance requires unsupported suspension extension: car="+CarIndex+" clearance="+sourceClearance+" requiredPreload="+SuspensionPreload+" maximum="+maximum);GD.Print("NATIVE_STATIC_CLEARANCE preload=",SuspensionPreload," original=",sourceClearance," target=",clearance);
  }
  // Underfloor evidence collects on every run (read-only vertex map for contact/clearance telemetry);
  // the ride-height mode separately consumes it and adjusts preload - evidence must not depend on the mode.
  {foreach(var node in GetChildren()){if(node is not CollisionShape3D shape||shape.Shape is not ConvexPolygonShape3D convex||!(shape.Name.ToString().Contains("CHASSIS",StringComparison.OrdinalIgnoreCase)||shape.Name.ToString().Contains("BUMPER",StringComparison.OrdinalIgnoreCase)||shape.Name.ToString().EndsWith("_LOD_A_BODY",StringComparison.OrdinalIgnoreCase)))continue;var points=convex.Points;float lowest=float.PositiveInfinity;foreach(var vertex in points)lowest=Mathf.Min(lowest,vertex.Y);var unique=new System.Collections.Generic.HashSet<Vector3>();foreach(var vertex in points)if(vertex.Y<=lowest+.005f&&unique.Add(vertex))underfloorPoints.Add((shape,vertex));}}
  float vmax=RecoveredVehicleProfiles.Speed[CarIndex]*.44704f;power=Mass*26.82f*26.82f/(2*RecoveredVehicleProfiles.Acceleration[CarIndex])*1.35f;drag=power/(vmax*vmax*vmax);brakeDecel=26.82f*26.82f/(2*RecoveredVehicleProfiles.Brakes[CarIndex]*.3048f);
 }
 public override void _IntegrateForces(PhysicsDirectBodyState3D state){
  if(!Initialized)return;
  if(DamageEnabled)Impacts.Observe(state,raceVehicle,CarIndex,this);contactReplay.Observe(this,state);
 var input=GridHold?new DriverInput(0,1,0):Controls.Safe();var pose=state.Transform;var up=pose.Basis.Y.Normalized();var forward=pose.Basis.Z.Normalized();float longitudinal=state.LinearVelocity.Dot(forward),speed=state.LinearVelocity.Length();
  WheelAngle=Mathf.MoveToward(WheelAngle,Dynamics.SteeringTarget(input.steer,longitudinal),3.2f*state.Step);
  float gravity=state.TotalGravity.Length(),spring=Mass*gravity/(4*StaticSag),damper=2*DampingRatio*Mathf.Sqrt(spring*Mass*.25f);
  float motorSpeed=RotationalTyres?Mathf.Max(Mathf.Abs(longitudinal),Mathf.Abs((WheelOmega[2]*radii[2]+WheelOmega[3]*radii[3])*.5f)):Mathf.Abs(longitudinal);
  float engine=input.reverse?-Mass*3.5f*(input.throttle+input.brake)*.5f:Mathf.Min(power/Mathf.Max(motorSpeed,6),Mass*gravity*2.2f)*input.throttle;
  if(raceVehicle!=null&&(raceVehicle.fuel<=0||raceVehicle.retired||raceVehicle.finished)){engine=0;if(raceVehicle.retired||raceVehicle.finished)HybridDriveForce=0;}if(DamageEnabled&&raceVehicle!=null)engine*=NativeImpactDamage.PowerFactor(raceVehicle);float combustionForce=engine;
  if(!input.reverse&&!GridHold)engine+=Mathf.Max(0,HybridDriveForce);float combustionShare=Mathf.Abs(engine)>.001f?Mathf.Abs(combustionForce/engine):0,combustionWork=0;
  Contacts=0;ChassisContacts=state.GetContactCount();ContactParts=new string[ChassisContacts];ContactSurfaces=new string[ChassisContacts];for(int c=0;c<ChassisContacts;c++){int shape=state.GetContactLocalShape(c);uint owner=ShapeFindOwner(shape);ContactParts[c]=(ShapeOwnerGetOwner(owner) as Node)?.Name.ToString()??"UnknownBodyShape";ContactSurfaces[c]=(state.GetContactColliderObject(c) as Node)?.GetPath().ToString()??"UnknownContactBody";}
  for(int i=0;i<4;i++){
   Grounded[i]=false;Loads[i]=0;ContactForces[i]=Vector3.Zero;ContactNormals[i]=Vector3.Zero;SpringLength[i]=RestLength+.15f;var mount=pose*mounts[i];
   using var query=PhysicsRayQueryParameters3D.Create(mount,mount-up*(radii[i]+RestLength+SuspensionPreload+.15f),1);
   var hit=state.GetSpaceState().IntersectRay(query);if(hit.Count==0){FreeWheel(i,input,engine,state.Step);continue;}
   var point=(Vector3)hit["position"];var normal=((Vector3)hit["normal"]).Normalized();ContactNormals[i]=normal;ContactPoints[i]=point;hit.Clear();
   float length=Mathf.Max(0,mount.DistanceTo(point)-radii[i]);SpringLength[i]=length;
   var arm=point-pose.Origin;var velocity=state.GetVelocityAtLocalPosition(arm);
   float compression=RestLength+SuspensionPreload-StaticSag-length;float bumpForce=spring*bumpStopRate*Mathf.Max(0,compression-bumpStopTravel);float load=Mathf.Clamp(spring*(RestLength+SuspensionPreload-length)+bumpForce-damper*velocity.Dot(normal),0,Mass*gravity);if(load<=0){FreeWheel(i,input,engine,state.Step);continue;}
   Grounded[i]=true;Loads[i]=load;Contacts++;state.ApplyForce(normal*load,arm);
   var rolling=(i<2?forward.Rotated(up,WheelAngle):forward);rolling=(rolling-normal*rolling.Dot(normal)).Normalized();var right=rolling.Cross(normal).Normalized();
   float u=velocity.Dot(rolling),side=velocity.Dot(right),mu=tyres[i].Grip(Water)*1.18f*Dynamics.GripMul(CarIndex),capacity=mu*load;
   float service=input.reverse?0:input.brake*brakeDecel*Mass*(i<2?.29f:.21f);float handbrake=input.handbrake&&i>=2?Mass*1.5f:0;if(!RotationalTyres)service+=handbrake;
   // A brake impulse cannot reverse a wheel's supported share of body momentum.
   if(!RotationalTyres)service=Mathf.Min(service,Mathf.Abs(u)*Mass/(4*Mathf.Max(state.Step,.00001f)));
   float alpha=Mathf.Atan2(side,Mathf.Max(Mathf.Abs(u),1));float lateralDemand=-capacity*(float)Math.Tanh(alpha/.12f);
   bool reserveLateral=(TractionControl&&i>=2&&engine>0&&input.brake<.01f&&!input.handbrake)||(BrakeStabilityAssist&&input.brake>.01f&&!input.reverse&&!(input.handbrake&&i>=2));
   float drive=i>=2?engine*.5f:0;
   // Torque management preserves the grip requested by measured lateral slip instead of
   // spending the whole rear friction circle on drive and allowing power oversteer.
   float longitudinalBudget=reserveLateral?Mathf.Sqrt(Mathf.Max(0,capacity*capacity-lateralDemand*lateralDemand)):capacity;
   float fx=Mathf.Clamp(drive-Mathf.Sign(u)*service,-longitudinalBudget,longitudinalBudget);float remaining=Mathf.Sqrt(Mathf.Max(0,capacity*capacity-fx*fx));
   float fy=reserveLateral?lateralDemand:-remaining*(float)Math.Tanh(alpha/.12f);
   if(RotationalTyres&&reserveLateral&&System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_COMBINED_ASSIST")=="1"){
    // Manage requested wheel torque before angular integration, never overwrite contact forces.
    // The rotational branch previously bypassed the lateral reserve used by the direct-force branch.
    Dynamics.ContactTyreForces(load,mu,0,Mathf.Atan2(side,Mathf.Max(Mathf.Abs(u),2)),out _,out float pureLateral);
    float torqueBudget=Mathf.Sqrt(Mathf.Max(0,capacity*capacity-pureLateral*pureLateral));
    drive=Mathf.Clamp(drive,-torqueBudget,torqueBudget);service=Mathf.Min(service,torqueBudget);
   }
   if(RotationalTyres){float inertia=1.6f*radii[i]*radii[i]/(.33f*.33f);RotationalTyreContact.Step(ref WheelOmega[i],radii[i],inertia,load,mu,u,side,drive,service,handbrake,TractionControl&&i>=2,BrakeStabilityAssist,state.Step,out fx,out fy,out LongitudinalSlip[i],out LateralSlipAngle[i]);alpha=LateralSlipAngle[i];WheelRotation[i]=Mathf.Wrap(WheelRotation[i]+WheelOmega[i]*state.Step,-Mathf.Pi,Mathf.Pi);}
   // Passive contact damping must not reverse a patch's lateral momentum in one solver step.
   // Include angular inertia at the contact lever, then share the budget over all four patches.
   var lateralLever=(arm-state.CenterOfMass).Cross(right);float inverseEffectiveMass=state.InverseMass+lateralLever.Dot(state.InverseInertiaTensor*lateralLever);
   float lateralStop=Mathf.Abs(side)/(4*Mathf.Max(state.Step,.00001f)*Mathf.Max(inverseEffectiveMass,.0000001f));fy=Mathf.Clamp(fy,-lateralStop,lateralStop);
   if(i>=2)combustionWork+=Mathf.Max(0,Mathf.Sign(engine)*fx)*Mathf.Abs(u)*state.Step*combustionShare;
   float roll=Mathf.Min(load*.014f,Mathf.Abs(u)*Mass/(4*Mathf.Max(state.Step,.00001f)));
   var tangentialForce=rolling*(fx-Mathf.Sign(u)*roll)+right*fy;ContactForces[i]=normal*load+tangentialForce;state.ApplyForce(tangentialForce,arm);
   tyres[i].Step(state.Step,Mathf.Abs(u),Mathf.Sqrt(alpha*alpha+(RotationalTyres?.25f*LongitudinalSlip[i]*LongitudinalSlip[i]:0)),load/Mathf.Max(1,Mass*gravity*.25f),Water);
  }
  if(raceVehicle!=null){NativeFuelUse.Consume(raceVehicle,combustionWork,state.Step);if(!raceVehicle.finished&&!raceVehicle.retired&&!raceVehicle.servicing&&raceVehicle.fuel<=0&&state.LinearVelocity.Length()<.5f){emptyStoppedTime+=state.Step;if(emptyStoppedTime>=2){raceVehicle.retired=true;raceVehicle.retirementReason="OUT OF FUEL";}}else emptyStoppedTime=0;}
  float damageDrag=DamageEnabled&&raceVehicle!=null?NativeImpactDamage.DragFactor(raceVehicle):1,damageAero=DamageEnabled&&raceVehicle!=null?NativeImpactDamage.AeroFactor(raceVehicle):1;state.ApplyCentralForce(-state.LinearVelocity*speed*drag*damageDrag-up*(Dynamics.DownK(CarIndex)*speed*speed*damageAero));
 }
}
}
