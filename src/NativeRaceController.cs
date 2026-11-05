using Godot;
using MySimulator.Rebuild;
using System;
namespace MySim {
// Opt-in integration of force-driven chassis with actual race rules/HUD/input/camera/pit/audio.
// Rival native authority and full-race acceptance are still required before production promotion.
public partial class NativeRaceController : DriveController {
 NativeSuspensionVehicle body;readonly SteeringInput steering=new();readonly PedalController pedalsNative=new();readonly Vector3[] homes=new Vector3[4];SurfaceWeather weather;float elapsed,travel,spawnHeading,maximumYaw;int ruleResets,telemetryTick;readonly System.Collections.Generic.List<object> telemetry=new();int mode,shown=-1;bool pauseWas=true,cameraWas,pitWas,parked,ending;Vector3 spawn,pausedVelocity,pausedAngular;bool pausedBody,padSeen,padStartBlocked;
 Vector3 previousPose;bool havePreviousPose;float actualPoseDistance,resultWait;
 NativeTraffic traffic;public NativeTraffic Traffic=>traffic;public NativeSuspensionVehicle Chassis=>body;public int CameraMode=>mode;NativeGameplayChecks gameplayChecks;NativePitChecks pitChecks;
 MySimulator.Rebuild.DrivingValidation.Runner validation;string validationReportPath=System.Environment.GetEnvironmentVariable("MYSIM_DRIVING_VALIDATION_REPORT");int validationResets;int dvTraceTick;bool gridHoldTraceSeen;
 public override void CycleCamera(){mode=(mode+1)%4;}
 public override void _Ready(){
  int rivals=int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_RIVALS")??Settings.Get("rivals",5).ToString());if(rivals!=0&&System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_TRAFFIC")!="1")throw new InvalidOperationException("Native opponents require explicit experimental traffic flag until full contact/race validation");
  NativePhysicsClock.Configure();if(System.Environment.GetEnvironmentVariable("MYSIM_DEFER_NATIVE_RENDER")=="1")GetViewport().Disable3D=true;Race=new Race(TrackIndex,Laps,Conditions.WetnessValue);Player=Race.Add("PLAYER",true,(Compound)Mathf.Clamp(int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_STARTING_COMPOUND")??Settings.Get("starting_compound",1).ToString(),System.Globalization.CultureInfo.InvariantCulture),0,3));Player.carModel=CarIndex;if(System.Environment.GetEnvironmentVariable("MYSIM_START_FUEL") is string initialFuel)Player.fuel=float.Parse(initialFuel,System.Globalization.CultureInfo.InvariantCulture);if(!float.IsFinite(Player.fuel)||Player.fuel<0)throw new ArgumentOutOfRangeException("MYSIM_START_FUEL");Player.human=!AiTest;if(System.Environment.GetEnvironmentVariable("MYSIM_ERS_AUTO")=="0")Player.ersAuto=false;Player.externalPhysics=true;Player.nativeTyreSteering=true;Player.traction=Settings.Get("tc",true);Player.abs=Settings.Get("abs",true);if(System.Environment.GetEnvironmentVariable("MYSIM_ASSISTS")=="0"){Player.traction=false;Player.abs=false;GD.Print("ASSISTS_OFF traction and brake-stability assists disabled for physics acceptance");}validationReportPath=System.Environment.GetEnvironmentVariable("MYSIM_DRIVING_VALIDATION_REPORT");Player.Place(Race.track,.988f,0);Player.externalResetRequested=false;if(AiTest&&System.Environment.GetEnvironmentVariable("MYSIM_GRID_START")!="1"){Race.countdown=0;Race.started=true;}weather=Conditions.BeginWeather();traffic=new NativeTraffic(this,rivals);
  if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_GAMEPLAY_CHECKS")=="1"){gameplayChecks=new NativeGameplayChecks {Drive=this};AddChild(gameplayChecks);}
  if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_PIT_CHECKS")=="1"){pitChecks=new NativePitChecks();pitChecks.Request(Player);}
  if(Hud!=null){Hud.Race=Race;Hud.Player=Player;}if(Conditions.Raining)Conditions.AddRain(Cam);
 }
 bool PlaceBody(string reason="spawn"){var point=new Vector3(Player.position.x,200,Player.position.z);using var query=PhysicsRayQueryParameters3D.Create(point,point-Vector3.Up*400,1);query.HitBackFaces=true;var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count==0)return false;var ground=(Vector3)hit["position"];hit.Clear();
  if(body==null){body=new NativeSuspensionVehicle {CarIndex=CarIndex,Compound=Player.tyres[0].compound,RecoveredWheelCentres=new Vector3[4],RecoveredWheelRadii=new float[4],Position=ground+Vector3.Up*.8f,Rotation=new Vector3(0,Player.heading,0)};for(int i=0;i<4;i++){homes[i]=WheelPivots[i].Position;body.RecoveredWheelCentres[i]=homes[i]-Vector3.Up*.65f;body.RecoveredWheelRadii[i]=WheelPivots[i].GetMeta("radius").AsSingle();}AddChild(body);if(!body.Initialized){ending=true;SetPhysicsProcess(false);return false;}Player.tyres=body.Tyres;TyreAppearance.Apply(WheelPivots,Player.tyres[0].compound);spawn=ground;spawnHeading=Player.heading;}
  else{body.GlobalPosition=ground+Vector3.Up*.65f;body.Rotation=new Vector3(0,Player.heading,0);body.SeedRollingVelocity(Vector3.Zero);body.AngularVelocity=Vector3.Zero;if(reason=="race-rule-reset")ruleResets++;GD.Print("NATIVE_CONTROLLED_PLACEMENT reason=",reason," track=",TrackIndex);}
  Player.externalResetRequested=false;havePreviousPose=false;return true;
 }
 void ReadBody(){var p=body.GlobalPosition;if(havePreviousPose)actualPoseDistance+=p.DistanceTo(previousPose);previousPose=p;havePreviousPose=true;var forward=body.GlobalBasis.Z;var q=body.GlobalBasis.GetRotationQuaternion();Player.hasBodyPose=true;Player.bodyHeight=p.Y;Player.bodyQx=q.X;Player.bodyQy=q.Y;Player.bodyQz=q.Z;Player.bodyQw=q.W;Player.position=new Vec(p.X,p.Z);Player.velocity=new Vec(body.LinearVelocity.X,body.LinearVelocity.Z);Player.speed=body.LinearVelocity.Length();Player.heading=Mathf.Atan2(forward.X,forward.Z);Player.yawRate=body.AngularVelocity.Y;Player.steering=body.WheelAngle;Player.progress=Race.track.Find(Player.position,out Player.lateral);Player.surfaceKnown=true;Player.onDrivingSurface=NativeSurfaceLegality.IsLegalWithWheelSupport(GetWorld3D().DirectSpaceState,p,Race.track.OnRoad(Player.progress,Player.lateral),body.Grounded);if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_FACE_EVIDENCE")=="1"&&!Player.onDrivingSurface&&!Player.lapInvalid){var ground=Contact.Ground(GetWorld3D().DirectSpaceState,p,p.Y,float.MaxValue);GD.Print("NATIVE_LEGALITY_FIRST_REJECTION ",System.Text.Json.JsonSerializer.Serialize(new {x=p.X,y=p.Y,z=p.Z,progress=Player.progress,lateral=Player.lateral,routeWidth=Race.track.width,insideRoute=Race.track.OnRoad(Player.progress,Player.lateral),ground.found,ground.near,ground.rawY,ground.normal,contacts=body.Contacts}));}int gears=RecoveredVehicleProfiles.GearData[CarIndex].Length;float vmax=RecoveredVehicleProfiles.Speed[CarIndex]*.44704f,step=vmax/gears*.92f;float longitudinal=body.LinearVelocity.Dot(forward);Player.gear=longitudinal<-.1f?-1:Math.Clamp(1+(int)(Mathf.Abs(longitudinal)/step),1,gears);Player.rpm=Mathf.Clamp(Player.gear<0?2500:3600+(Mathf.Abs(longitudinal)-(Player.gear-1)*step)/step*4600,900,RecoveredVehicleProfiles.Redline[CarIndex]);}
 public override void _PhysicsProcess(double delta){if(ending)return;try{
  if(body==null){if(!PlaceBody())return;}ReadBody();if(!traffic.Read())return;
  var pausePads=Input.GetConnectedJoypads();bool startButton=false;if(pausePads.Count>0){bool raw=Input.IsJoyButtonPressed(pausePads[0],JoyButton.Start);if(!padSeen){padSeen=true;padStartBlocked=raw;}startButton=raw&&!padStartBlocked;if(!raw)padStartBlocked=false;}
  bool pause=startButton||Input.IsKeyPressed(Key.Escape)||Input.IsKeyPressed(Key.Tab);if(pause&&!pauseWas)Race.paused=!Race.paused;pauseWas=pause;Hud.Paused=Race.paused;
  traffic.Pause(Race.paused);if(Race.paused){if(!pausedBody){pausedVelocity=body.LinearVelocity;pausedAngular=body.AngularVelocity;body.Freeze=true;pausedBody=true;}UpdateAudio(-1);return;}if(pausedBody){body.Freeze=parked;body.LinearVelocity=pausedVelocity;body.AngularVelocity=pausedAngular;pausedBody=false;}
  elapsed+=(float)delta;Conditions.Advance(weather,(float)delta);Race.wetness=weather.Water;RenderingServer.GlobalShaderParameterSet("mysim_surface_water",Race.wetness);body.Water=Race.wetness;body.TractionControl=Player.traction;body.BrakeStabilityAssist=Player.abs;
  float throttle=ControlBindings.Held(0)?1:0,brake=ControlBindings.Held(1)?1:0,requested=(ControlBindings.Held(3)?1:0)-(ControlBindings.Held(2)?1:0);bool analog=false;var pads=Input.GetConnectedJoypads();if(pads.Count>0){int id=pads[0];float axis=Input.GetJoyAxis(id,JoyAxis.LeftX);if(Mathf.Abs(axis)>.08f){requested=Mathf.Sign(axis)*Mathf.Pow((Mathf.Abs(axis)-.08f)/.92f,1.35f);analog=true;}throttle=Mathf.Max(throttle,Mathf.Clamp(Input.GetJoyAxis(id,JoyAxis.TriggerRight),0,1));brake=Mathf.Max(brake,Mathf.Clamp(Input.GetJoyAxis(id,JoyAxis.TriggerLeft),0,1));}
  if(TouchControls.Active){if(Mathf.Abs(TouchControls.Steer)>.01f)requested=TouchControls.Steer;throttle=Mathf.Max(throttle,TouchControls.Throttle);brake=Mathf.Max(brake,TouchControls.Brake);}if(!DisplayServer.WindowIsFocused()&&DisplayServer.GetName()!="headless")throttle=brake=requested=0;
  if(AutoDrive&&!AiTest){throttle=elapsed>=3&&elapsed<7?.3f:0;brake=elapsed>=7?1:0;requested=0;}
  // Race AI runs at120Hz; retain its command during the intervening native240Hz tick.
  // Human controls only own the car outside pit assist and terminal race states.
  if(Player.human&&!Player.pitActive&&!Player.finished&&!Player.retired)Player.input=pedalsNative.Update(throttle,brake,SteeringInput.ToSimulation(steering.Step(requested,(float)delta,analog)),ControlBindings.Held(4),body.LinearVelocity.Dot(body.GlobalBasis.Z),(float)delta,!AutoDrive);
  bool pit=ControlBindings.Held(5)||(pads.Count>0&&Input.IsJoyButtonPressed(pads[0],JoyButton.A));if(pit&&!pitWas)Hud.TogglePitStrategy();pitWas=pit;
  Race.Advance((float)delta);Player.totalDistance=actualPoseDistance;traffic.Apply((float)delta);if(Player.externalResetRequested&&!PlaceBody("race-rule-reset"))throw new Exception("Native race reset lacks drive surface");
  if(Player.servicing&&!parked){if(!PlaceBody("pit-box-alignment"))throw new Exception("Pit box has no physical surface");body.SeedRollingVelocity(Vector3.Zero);body.AngularVelocity=Vector3.Zero;body.Freeze=true;parked=true;}else if(!Player.servicing&&parked){body.Freeze=false;parked=false;TyreAppearance.Apply(WheelPivots,Player.tyres[0].compound);}
  if(validation==null&&System.Environment.GetEnvironmentVariable("MYSIM_DRIVING_VALIDATION")=="1")validation=CreateValidationRunner();
  if(validation!=null){
   if(!validation.Completed&&(Player.finished||Player.retired||elapsed>=AutoSeconds))validation.Abort("session ended before all validation profiles completed");
   validation.Advance(BuildValidationSample(),(float)delta);
   if(validation.NeedsReset){if(!PlaceBody("validation-reset"))throw new Exception("Driving validation reset lacks drive surface");validationResets++;validation.ConfirmReset();}
   if(validation.Completed){FinishValidation();return;}
   if(!Player.servicing&&!Player.finished&&!Player.retired)Player.input=ValidationInput(validation.Current);
   if(dvTraceTick++%120==0){MySimulator.Rebuild.DrivingValidation.Run stage=null;foreach(var r in validation.Runs){if(!r.Completed){stage=r;break;}}
    GD.Print("DV_TRACE t=",elapsed.ToString("0.0")," started=",Race.started," gridHold=",body.GridHold," spd=",Player.speed.ToString("0.00")," thr=",Player.input.throttle.ToString("0.00")," brk=",Player.input.brake.ToString("0.00")," stage=",(stage!=null?stage.Profile.Id:"done")," clock=",validation.Clock.ToString("0.0"));gridHoldTraceSeen=gridHoldTraceSeen||body.GridHold;}
  }
  body.GridHold=!Race.started;body.BindRaceControls(Player,(float)delta,Race.track);
  if(WheelPivots[0].GetMeta("displayed_compound").AsInt32()!=(int)Player.tyres[0].compound)TyreAppearance.Apply(WheelPivots,Player.tyres[0].compound);
  float service=Player.servicing?Player.ServiceElapsed:-1;lastLift=PitSequence(service);Car.GlobalTransform=new Transform3D(body.GlobalBasis,body.GlobalPosition-body.GlobalBasis.Y*.65f+Vector3.Up*lastLift);UpdateCrew(service);UpdateAudio(service);
  travel+=body.LinearVelocity.Dot(body.GlobalBasis.Z)*(float)delta;if(body.RotationalTyres)WheelAnimation.UpdateRotational(WheelPivots,body.WheelRotation,body.WheelAngle);else WheelAnimation.Update(WheelPivots,travel,body.WheelAngle);for(int i=0;i<4;i++)WheelPivots[i].Position=homes[i]+Vector3.Up*(body.RestLength-body.StaticSag-body.SpringLength[i]);
  bool camera=ControlBindings.Held(6)||(pads.Count>0&&Input.IsJoyButtonPressed(pads[0],JoyButton.Y));if(camera&&!cameraWas)CycleCamera();cameraWas=camera;UpdateView();
  if(AutoDrive&&!AiTest&&elapsed>=3)maximumYaw=Mathf.Max(maximumYaw,Mathf.Abs(Mathf.Wrap(body.Rotation.Y-spawnHeading,-Mathf.Pi,Mathf.Pi)));if(++telemetryTick%12==0)telemetry.Add(new {elapsed,raceStarted=Race.started,raceClock=Race.clock,countdown=Race.countdown,gridHold=body.GridHold,appliedThrottle=body.Controls.throttle,appliedBrake=body.Controls.brake,hybridForce=body.HybridDriveForce,loadedWheels=LoadedWheelEvidence.Capture(WheelPivots,body),underfloor=body.UnderfloorEvidence(),roadSpeedPlan=body.RoadPlanning.Evidence,velocityX=body.LinearVelocity.X,velocityY=body.LinearVelocity.Y,velocityZ=body.LinearVelocity.Z,actualPoseDistance,opponents=traffic.Evidence,damage=Player.damage,impactEvents=body.Impacts.Events,impactEvidenceCount=body.Impacts.Evidence.Count,impactEvidenceReference="top-level impactEvidence; count is the sample-time prefix",verticalRoadPlanning=body.RoadPlanning.Evidence,x=body.GlobalPosition.X,z=body.GlobalPosition.Z,progress=Player.progress,lateral=Player.lateral,nextGate=Player.nextGate,lapInvalid=Player.lapInvalid,completedLaps=Player.completedLaps,steer=Player.input.steer,wheelAngle=body.WheelAngle,throttle=Player.input.throttle,brake=Player.input.brake,reverse=Player.input.reverse,desiredSpeed=Player.dbgA,pathCurvature=Player.dbgB,lookLocalX=Player.dbgC,aiRecoveries=Player.recoveries,pitActive=Player.pitActive,wear=Player.Wear,speed=Player.speed,yaw=body.Rotation.Y,maximumYaw,contacts=body.Contacts,chassisContacts=body.ChassisContacts,contactParts=body.ContactParts,contactSurfaces=body.ContactSurfaces,bodyY=body.GlobalPosition.Y,springLengths=(float[])body.SpringLength.Clone(),tyreLoadRatio=body.TyreLoadRatio,wheelOmega=(float[])body.WheelOmega.Clone(),slip=(float[])body.LongitudinalSlip.Clone(),rotationalTyres=body.RotationalTyres,ruleResets,paused=Race.paused,servicing=Player.servicing});
  pitChecks?.Observe(this);if(pitChecks?.Blocked==true){FinishAi(false);return;}
  if(AiTest&&elapsed>=30&&Player.totalDistance<10){GD.Print("NATIVE_AI_LAUNCH_FAILURE: force-driven car failed to travel10m in30seconds; bodyContacts=",body.ChassisContacts," tyreContacts=",body.Contacts);if(validation!=null&&!validation.Completed){validation.Abort("AI launch failure at t=30: totalDistance="+Player.totalDistance+" bodyContacts="+body.ChassisContacts+" tyreContacts="+body.Contacts);FinishValidation();return;}FinishAi(false);return;}
  if(AiTest&&(Player.finished||Player.retired||elapsed>=AutoSeconds)){if((Player.finished||Player.retired)&&GetTree().CurrentScene is Main resultGame&&!resultGame.ResultsVisible&&resultWait<14){resultWait+=(float)delta;return;}FinishAi(Player.finished&&Player.completedLaps>=Laps&&Player.bestLap>0&&ruleResets==0&&Player.totalDistance>=Race.track.length*.95f);return;}
  if(AutoDrive&&!AiTest&&elapsed>=AutoSeconds){bool ok=Player.speed<1&&body.Contacts==4&&body.TyreLoadRatio>.9f&&body.TyreLoadRatio<1.1f&&(body.GlobalPosition-spawn).Length()>10&&maximumYaw<.15f&&ruleResets==0&&Hud.Player==Player&&Hud.Race==Race&&(gameplayChecks==null||gameplayChecks.Completed);string report=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_GAMEPLAY_REPORT");if(report!=null)System.IO.File.WriteAllText(report,System.Text.Json.JsonSerializer.Serialize(new {ok,rotationalTyres=body.RotationalTyres,car=CarIndex,track=TrackIndex,maximumYaw,ruleResets,speed=Player.speed,displacement=(body.GlobalPosition-spawn).Length(),checksCompleted=gameplayChecks?.Completed,telemetry,scope="Opt-in native single-car gameplay integration, no full-race/rival/art/real-world calibration acceptance"},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));GD.Print(ok?"PASS native integrated race HUD, chassis acceleration and braking":"FAIL native integrated gameplay");ending=true;FinishProbeCapture(ok);}
 }catch(Exception e){GD.PushError(e.ToString());ending=true;CleanExit.Request(this,1);}}
 MySimulator.Rebuild.DrivingValidation.Runner CreateValidationRunner(){
  // The DV pipeline selects the manifest via DRIVING_VALIDATION_MANIFEST (tools/cloud_game_audit.py);
  // GEOMETRY_CASE_MANIFEST stays as the generic named-case fallback, then the core default.
  string manifestPath=System.Environment.GetEnvironmentVariable("DRIVING_VALIDATION_MANIFEST");
  if(string.IsNullOrEmpty(manifestPath))manifestPath=System.Environment.GetEnvironmentVariable("GEOMETRY_CASE_MANIFEST");
  if(string.IsNullOrEmpty(manifestPath))manifestPath="data/native_driving_validation_core.json";
  var ids=MySimulator.Rebuild.DrivingValidation.ParseCaseProfiles(ReadProjectText(manifestPath),CarIndex,TrackIndex);
  var profiles=MySimulator.Rebuild.DrivingValidation.ParseProfiles(ReadProjectText(MySimulator.Rebuild.DrivingValidation.ProfilesResourceDefault));
  var map=new System.Collections.Generic.Dictionary<string,MySimulator.Rebuild.DrivingValidation.Profile>();
  foreach(var p in profiles)map[p.Id]=p;
  var references=ValidationReferences();
  var runs=new System.Collections.Generic.List<MySimulator.Rebuild.DrivingValidation.Run>();
  foreach(var id in ids){
   if(!map.TryGetValue(id,out var profile))throw new InvalidOperationException("Driving validation profile "+id+" is missing from native_driving_validation_profiles.json");
   if(profile.RequireRotationalTyres&&!body.RotationalTyres)throw new InvalidOperationException("Driving validation profile "+id+" requires rotational tyres; dispatch with rotational_tyres=true");
   if(profile.RequireDamage&&!body.DamageEnabled)throw new InvalidOperationException("Driving validation profile "+id+" requires the native damage system; dispatch with native_damage=true");
   runs.Add(new MySimulator.Rebuild.DrivingValidation.Run(profile,references));
  }
  if(runs.Count==0)throw new InvalidOperationException("Driving validation case selected no profiles");
  return new MySimulator.Rebuild.DrivingValidation.Runner(runs,references);
 }
 System.Collections.Generic.Dictionary<string,double> ValidationReferences()=>new System.Collections.Generic.Dictionary<string,double>{
  ["brakeStop60Metres"]=RecoveredVehicleProfiles.Brakes[CarIndex]*.3048,
  ["accel0To60Seconds"]=RecoveredVehicleProfiles.Acceleration[CarIndex],
  ["steerActuatorRate"]=3.2};
 string ReadProjectText(string path){
  if(System.IO.File.Exists(path))return System.IO.File.ReadAllText(path);
  var normalised=path.Replace('\\','/');if(!normalised.StartsWith("res://",StringComparison.Ordinal))normalised="res://"+normalised;
  return FileAccess.GetFileAsString(normalised);
 }
 MySimulator.Rebuild.DrivingValidation.Sample BuildValidationSample(){
  var forward=body.GlobalBasis.Z;var right=body.GlobalBasis.X;
  float longitudinal=body.LinearVelocity.Dot(forward);
  int mask=0;for(int i=0;i<4;i++)if(body.Grounded[i])mask|=1<<i;
  bool nonSurfaceContact=false;foreach(var surface in body.ContactSurfaces)if(surface!=null&&surface.IndexOf("DriveSurface",StringComparison.OrdinalIgnoreCase)<0){nonSurfaceContact=true;break;}
  var command=validation.Current;
  return new MySimulator.Rebuild.DrivingValidation.Sample{
   speed=body.LinearVelocity.Length(),longitudinalSpeed=longitudinal,lateralSpeed=body.LinearVelocity.Dot(right),
   heading=body.Rotation.Y,yawRate=body.AngularVelocity.Y,upY=body.GlobalBasis.Y.Normalized().Y,
   wheelAngle=body.WheelAngle,wheelAngleTarget=MySimulator.Rebuild.Dynamics.SteeringTarget((float)command.Steer,longitudinal),
   steerCommand=command.Steer,throttle=command.Throttle,brake=command.Brake,
   x=body.GlobalPosition.X,z=body.GlobalPosition.Z,trackLateral=Player.lateral,progress=Player.progress,water=body.Water,
   onDrivingSurface=Player.onDrivingSurface,nonSurfaceContact=nonSurfaceContact,groundedMask=mask,chassisContacts=body.ChassisContacts,
   impactEvents=body.Impacts.Events,maxDeltaV=body.Impacts.MaximumDeltaV,
   loads=ToDoubles(body.Loads),springLengths=ToDoubles(body.SpringLength),
   longitudinalSlip=ToDoubles(body.LongitudinalSlip),lateralSlipAngle=ToDoubles(body.LateralSlipAngle)};
 }
 static double[] ToDoubles(float[] source){var result=new double[source.Length];for(int i=0;i<source.Length;i++)result[i]=source[i];return result;}
 DriverInput ValidationInput(MySimulator.Rebuild.DrivingValidation.Command c)=>new DriverInput((float)c.Throttle,(float)c.Brake,(float)c.Steer){handbrake=c.Handbrake,reverse=c.Reverse};
 void FinishValidation(){
  ending=true;
  bool ok=validation.Completed&&validation.AllPassed;
  var runList=new System.Collections.Generic.List<object>();
  foreach(var run in validation.Runs){
   var metrics=new System.Collections.Generic.Dictionary<string,object>();
   foreach(var phase in run.Profile.Phases)
    foreach(var name in MySimulator.Rebuild.DrivingValidation.KnownMetrics){
     if(name=="meanYawRateAbsMin")continue;
     var value=MySimulator.Rebuild.DrivingValidation.ComputeMetric(name,phase.Id,run.Timeline,run.Profile,validation.References);
     if(value.HasValue)metrics[phase.Id+"."+name]=value.Value;
    }
   var assertions=new System.Collections.Generic.List<object>();
   foreach(var r in run.Results)assertions.Add(new{id=r.Id,metric=r.Metric,op=r.Op,passed=r.Passed,actual=r.Actual,expected=r.Expected});
   var samples=new System.Collections.Generic.List<object>();
   foreach(var s in run.Timeline)samples.Add(new{s.t,s.speed,s.longitudinalSpeed,s.lateralSpeed,s.heading,s.yawRate,s.upY,s.wheelAngle,s.wheelAngleTarget,s.steerCommand,s.throttle,s.brake,s.x,s.z,s.trackLateral,s.progress,s.water,onDrivingSurface=s.onDrivingSurface?1:0,s.groundedMask,s.chassisContacts,s.impactEvents,s.maxDeltaV,s.loads,s.springLengths,s.longitudinalSlip,s.lateralSlipAngle});
   runList.Add(new{profile=run.Profile.Id,title=run.Profile.Title,ok=run.Passed&&run.AbortReason==null,completed=run.Completed,abortReason=run.AbortReason,firstFailure=run.FirstFailure,metrics,assertions,sampleCount=run.Timeline.Count,samplesTruncated=run.Timeline.Count>=run.Profile.MaxSamples,samples});
  }
  var report=new System.Collections.Generic.Dictionary<string,object>{
   ["ok"]=validation.Completed&&validation.AllPassed,
   ["completed"]=validation.Completed,["allPassed"]=validation.AllPassed,
   ["car"]=CarIndex,["track"]=TrackIndex,["physicsHz"]=Engine.PhysicsTicksPerSecond,
   ["recordFps"]=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_RECORD_FPS")??"unrecorded",
   ["rotationalTyres"]=body.RotationalTyres,
   ["assistsOff"]=System.Environment.GetEnvironmentVariable("MYSIM_ASSISTS")=="0",
   ["damageEnabled"]=body.DamageEnabled,
   ["backend"]=System.Environment.GetEnvironmentVariable("MYSIM_PHYSICS_BACKEND")??"authoritative record is physics-backend-experiment.json of the same run",
   ["weatherProfile"]=Conditions.Wet,["water"]=body.Water,["compound"]=(int)Player.tyres[0].compound,
   ["simulatedSeconds"]=validation.Clock,["validationResets"]=validationResets,["ruleResets"]=ruleResets,
   ["firstFailure"]=validation.FirstFailure,["abortReason"]=validation.AbortReason,
   ["gridHoldTraceSeen"]=gridHoldTraceSeen,
   ["runs"]=runList,
   ["scope"]="Scripted controlled driving validation with failure-closed assertions on the native force-driven model; scenario rig evidence only - no lap, AI pace, graphics or real-world calibration claim. Compare only against runs with identical solver/backend/rate settings."};
  string path=validationReportPath??"user://driving-validation-report.json";
  string writePath=path.StartsWith("user://",StringComparison.Ordinal)?ProjectSettings.GlobalizePath(path):path;
  System.IO.File.WriteAllText(writePath,System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
  GD.Print(ok?"PASS driving validation scenarios":"FAIL driving validation: "+(validation.FirstFailure??"incomplete"));
  CleanExit.Request(this,ok?0:1);
 }
 async void FinishProbeCapture(bool ok){try{string shot=System.Environment.GetEnvironmentVariable("MYSIM_SHOT");if(shot!=null){body.Freeze=true;GetViewport().Disable3D=false;await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);using var capture=GetViewport().GetTexture().GetImage();var error=capture.SavePng(shot);if(error!=Error.Ok)throw new Exception("Integrated native capture failed: "+error);GD.Print("PASS actual integrated native scene capture");}CleanExit.Request(this,ok?0:1);}catch(Exception e){GD.PushError(e.ToString());CleanExit.Request(this,1);}}
 async void FinishAi(bool ok){try{ending=true;body.Controls=new DriverInput(0,1,0);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_RESULT_CAPTURE") is string imagePath){await ToSignal(RenderingServer.Singleton,RenderingServer.SignalName.FramePostDraw);using var image=GetViewport().GetTexture().GetImage();if(image.SavePng(imagePath)!=Error.Ok)throw new Exception("Native results image save failed");}bool resultVisible=GetTree().CurrentScene is Main game&&game.ResultsVisible;ok&=(pitChecks==null||pitChecks.Completed)&&resultVisible&&actualPoseDistance>=Race.track.length*.95f&&(!body.DamageEnabled||body.Impacts.Events==0&&Player.damage==0)&&traffic.AdvancedWithoutRescue&&(gameplayChecks==null||gameplayChecks.Completed);string report=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_GAMEPLAY_REPORT");if(report!=null){using var reportStream=System.IO.File.Create(report);System.Text.Json.JsonSerializer.Serialize(reportStream,new {ok,continuousCcd=body.ContinuousCd,car=CarIndex,track=TrackIndex,damage=Player.damage,damageEnabled=body.DamageEnabled,weatherProfile=Conditions.Wet,water=body.Water,tyreCompound=(int)Player.tyres[0].compound,impactEvents=body.Impacts.Events,impactEvidence=body.Impacts.Evidence,verticalRoadPlanning=body.RoadPlanning.Evidence,actualPoseDistance,rotationalTyres=body.RotationalTyres,finished=Player.finished,laps=Player.completedLaps,bestLap=Player.bestLap,time=elapsed,ruleResets,distance=Player.totalDistance,requiredDistance=Race.track.length*.95f,resultVisible,pitRoute=Array.ConvertAll(Race.track.PitRoute,p=>new[]{p.x,p.z}),pitServiceIndex=Race.track.PitServiceIndex,routeWidth=Race.track.width,route=Array.ConvertAll(Race.track.points,p=>new[]{p.x,p.z}),bodyY=body.GlobalPosition.Y,tyreContacts=body.Contacts,contactParts=body.ContactParts,contactSurfaces=body.ContactSurfaces,springLengths=body.SpringLength,telemetry,pit=pitChecks?.Evidence,opponents=traffic.Evidence,opponentResets=traffic.RuleResets,checksCompleted=gameplayChecks?.Completed,scope="Native force-driven legal player lap and results UI; opponents must advance physically without rescue when enabled; all-layout/contact/art acceptance separate"},new System.Text.Json.JsonSerializerOptions {WriteIndented=true});}GD.Print(ok?"PASS native force-driven valid lap and results UI":"FAIL native force-driven lap or results UI");CleanExit.Request(this,ok?0:1);}catch(Exception error){GD.PushError("Native race finalization failed: "+error);CleanExit.Request(this,1);}}
 void UpdateView(){if(shown!=mode){shown=mode;CarViewVisibility.Apply(Car,WheelPivots,CarIndex,mode==2);}CarViewVisibility.SetWheels(WheelPivots,CarIndex,mode==2);
  var forward=body.GlobalBasis.Z;var target=Car.GlobalPosition+Vector3.Up*.8f;if(mode<2){var want=target-forward*(mode==0?7:11)+Vector3.Up*(mode==0?2:3.8f);using var query=PhysicsRayQueryParameters3D.Create(target,want,2);query.HitBackFaces=true;var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count>0){want=target+(want-target).Normalized()*Mathf.Max(0,target.DistanceTo((Vector3)hit["position"])-.35f);hit.Clear();}Cam.GlobalPosition=want;Cam.LookAt(target+forward*3);Cam.Fov=48;}
  else{Cam.GlobalPosition=Car.GlobalTransform*new Vector3(mode==2&&CarCatalog.RaceClass(CarIndex)!=1?-.35f:0,mode==2?.88f:.62f,mode==2?.05f:1.9f);Cam.LookAt(Cam.GlobalPosition+forward*10);Cam.Fov=mode==2?75:70;}
 }
}
}
