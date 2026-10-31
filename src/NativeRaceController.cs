using Godot;
using MySimulator.Rebuild;
using System;
namespace MySim {
// Opt-in integration of force-driven chassis with actual race rules/HUD/input/camera/pit/audio.
// Rival native authority and full-race acceptance are still required before production promotion.
public partial class NativeRaceController : DriveController {
 NativeSuspensionVehicle body;readonly SteeringInput steering=new();readonly PedalController pedalsNative=new();readonly Vector3[] homes=new Vector3[4];SurfaceWeather weather;float elapsed,travel;int mode,shown=-1;bool pauseWas=true,cameraWas,pitWas,parked,ending;Vector3 spawn,pausedVelocity,pausedAngular;bool pausedBody,padSeen,padStartBlocked;
 public NativeSuspensionVehicle Chassis=>body;public int CameraMode=>mode;NativeGameplayChecks gameplayChecks;
 public override void CycleCamera(){mode=(mode+1)%4;}
 public override void _Ready(){
  int rivals=int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_RIVALS")??Settings.Get("rivals",5).ToString());if(rivals!=0)throw new InvalidOperationException("Native gameplay integration currently requires explicit zero rivals; multiplayer contact validation is pending");
  Engine.PhysicsTicksPerSecond=120;Race=new Race(TrackIndex,Laps,Conditions.WetnessValue);Player=Race.Add("PLAYER",true,(Compound)Mathf.Clamp(Settings.Get("starting_compound",1),0,3));Player.carModel=CarIndex;Player.externalPhysics=true;Player.traction=Settings.Get("tc",true);Player.abs=Settings.Get("abs",true);Player.Place(Race.track,.988f,0);Player.externalResetRequested=false;Race.countdown=0;Race.started=true;weather=Conditions.BeginWeather();
  if(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_GAMEPLAY_CHECKS")=="1"){gameplayChecks=new NativeGameplayChecks {Drive=this};AddChild(gameplayChecks);}
  if(Hud!=null){Hud.Race=Race;Hud.Player=Player;}if(Conditions.Raining)Conditions.AddRain(Cam);
 }
 bool PlaceBody(string reason="spawn"){var point=new Vector3(Player.position.x,200,Player.position.z);using var query=PhysicsRayQueryParameters3D.Create(point,point-Vector3.Up*400,1);query.HitBackFaces=true;var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count==0)return false;var ground=(Vector3)hit["position"];hit.Clear();
  if(body==null){body=new NativeSuspensionVehicle {CarIndex=CarIndex,Compound=Player.tyres[0].compound,RecoveredWheelCentres=new Vector3[4],RecoveredWheelRadii=new float[4],Position=ground+Vector3.Up*.8f,Rotation=new Vector3(0,Player.heading,0)};for(int i=0;i<4;i++){homes[i]=WheelPivots[i].Position;body.RecoveredWheelCentres[i]=homes[i]-Vector3.Up*.65f;body.RecoveredWheelRadii[i]=WheelPivots[i].GetMeta("radius").AsSingle();}AddChild(body);Player.tyres=body.Tyres;TyreAppearance.Apply(WheelPivots,Player.tyres[0].compound);spawn=ground;}
  else{body.GlobalPosition=ground+Vector3.Up*.65f;body.Rotation=new Vector3(0,Player.heading,0);body.LinearVelocity=body.AngularVelocity=Vector3.Zero;GD.Print("NATIVE_CONTROLLED_PLACEMENT reason=",reason," track=",TrackIndex);}
  Player.externalResetRequested=false;return true;
 }
 void ReadBody(){var p=body.GlobalPosition;var forward=body.GlobalBasis.Z;Player.position=new Vec(p.X,p.Z);Player.velocity=new Vec(body.LinearVelocity.X,body.LinearVelocity.Z);Player.speed=body.LinearVelocity.Length();Player.heading=Mathf.Atan2(forward.X,forward.Z);Player.yawRate=body.AngularVelocity.Y;Player.steering=body.WheelAngle;Player.progress=Race.track.Find(Player.position,out Player.lateral);Player.surfaceKnown=true;Player.onDrivingSurface=body.Contacts>0&&Race.track.OnRoad(Player.progress,Player.lateral);int gears=RecoveredVehicleProfiles.GearData[CarIndex].Length;float vmax=RecoveredVehicleProfiles.Speed[CarIndex]*.44704f,step=vmax/gears*.92f;float longitudinal=body.LinearVelocity.Dot(forward);Player.gear=longitudinal<-.1f?-1:Math.Clamp(1+(int)(Mathf.Abs(longitudinal)/step),1,gears);Player.rpm=Mathf.Clamp(Player.gear<0?2500:3600+(Mathf.Abs(longitudinal)-(Player.gear-1)*step)/step*4600,900,RecoveredVehicleProfiles.Redline[CarIndex]);}
 public override void _PhysicsProcess(double delta){if(ending)return;try{
  if(body==null){if(!PlaceBody())return;}ReadBody();
  var pausePads=Input.GetConnectedJoypads();bool startButton=false;if(pausePads.Count>0){bool raw=Input.IsJoyButtonPressed(pausePads[0],JoyButton.Start);if(!padSeen){padSeen=true;padStartBlocked=raw;}startButton=raw&&!padStartBlocked;if(!raw)padStartBlocked=false;}
  bool pause=startButton||Input.IsKeyPressed(Key.Escape)||Input.IsKeyPressed(Key.Tab);if(pause&&!pauseWas)Race.paused=!Race.paused;pauseWas=pause;Hud.Paused=Race.paused;
  if(Race.paused){if(!pausedBody){pausedVelocity=body.LinearVelocity;pausedAngular=body.AngularVelocity;body.Freeze=true;pausedBody=true;}UpdateAudio(-1);return;}if(pausedBody){body.Freeze=parked;body.LinearVelocity=pausedVelocity;body.AngularVelocity=pausedAngular;pausedBody=false;}
  elapsed+=(float)delta;Conditions.Advance(weather,(float)delta);Race.wetness=weather.Water;RenderingServer.GlobalShaderParameterSet("mysim_surface_water",Race.wetness);body.Water=Race.wetness;body.TractionControl=Player.traction;body.BrakeStabilityAssist=Player.abs;
  float throttle=ControlBindings.Held(0)?1:0,brake=ControlBindings.Held(1)?1:0,requested=(ControlBindings.Held(3)?1:0)-(ControlBindings.Held(2)?1:0);bool analog=false;var pads=Input.GetConnectedJoypads();if(pads.Count>0){int id=pads[0];float axis=Input.GetJoyAxis(id,JoyAxis.LeftX);if(Mathf.Abs(axis)>.08f){requested=Mathf.Sign(axis)*Mathf.Pow((Mathf.Abs(axis)-.08f)/.92f,1.35f);analog=true;}throttle=Mathf.Max(throttle,Mathf.Clamp(Input.GetJoyAxis(id,JoyAxis.TriggerRight),0,1));brake=Mathf.Max(brake,Mathf.Clamp(Input.GetJoyAxis(id,JoyAxis.TriggerLeft),0,1));}
  if(TouchControls.Active){if(Mathf.Abs(TouchControls.Steer)>.01f)requested=TouchControls.Steer;throttle=Mathf.Max(throttle,TouchControls.Throttle);brake=Mathf.Max(brake,TouchControls.Brake);}if(!DisplayServer.WindowIsFocused()&&DisplayServer.GetName()!="headless")throttle=brake=requested=0;
  if(AutoDrive){throttle=elapsed>=3&&elapsed<7?.3f:0;brake=elapsed>=7?1:0;requested=0;}
  Player.input=pedalsNative.Update(throttle,brake,SteeringInput.ToSimulation(steering.Step(requested,(float)delta,analog)),ControlBindings.Held(4),body.LinearVelocity.Dot(body.GlobalBasis.Z),(float)delta,!AutoDrive);
  bool pit=ControlBindings.Held(5)||(pads.Count>0&&Input.IsJoyButtonPressed(pads[0],JoyButton.A));if(pit&&!pitWas)Hud.TogglePitStrategy();pitWas=pit;
  Race.Advance((float)delta);if(Player.externalResetRequested&&!PlaceBody("race-rule-reset"))throw new Exception("Native race reset lacks drive surface");
  if(Player.servicing&&!parked){if(!PlaceBody("pit-box-alignment"))throw new Exception("Pit box has no physical surface");body.LinearVelocity=body.AngularVelocity=Vector3.Zero;body.Freeze=true;parked=true;}else if(!Player.servicing&&parked){body.Freeze=false;parked=false;TyreAppearance.Apply(WheelPivots,Player.tyres[0].compound);}
  body.Controls=Player.servicing?new DriverInput(0,1,0):Player.input;
  float service=Player.servicing?PitServiceTimeline.Duration-Player.serviceRemaining:-1;lastLift=PitSequence(service);Car.GlobalTransform=new Transform3D(body.GlobalBasis,body.GlobalPosition-body.GlobalBasis.Y*.65f+Vector3.Up*lastLift);UpdateCrew(service);UpdateAudio(service);
  travel+=body.LinearVelocity.Dot(body.GlobalBasis.Z)*(float)delta;WheelAnimation.Update(WheelPivots,travel,body.WheelAngle);for(int i=0;i<4;i++)WheelPivots[i].Position=homes[i]+Vector3.Up*(body.RestLength-body.StaticSag-body.SpringLength[i]);
  bool camera=ControlBindings.Held(6)||(pads.Count>0&&Input.IsJoyButtonPressed(pads[0],JoyButton.Y));if(camera&&!cameraWas)CycleCamera();cameraWas=camera;UpdateView();
  if(AutoDrive&&elapsed>=AutoSeconds){bool ok=Player.speed<1&&(body.GlobalPosition-spawn).Length()>10&&Hud.Player==Player&&Hud.Race==Race&&(gameplayChecks==null||gameplayChecks.Completed);GD.Print(ok?"PASS native integrated race HUD, chassis acceleration and braking":"FAIL native integrated gameplay");ending=true;CleanExit.Request(this,ok?0:1);}
 }catch(Exception e){GD.PushError(e.ToString());ending=true;CleanExit.Request(this,1);}}
 void UpdateView(){if(shown!=mode){shown=mode;CarViewVisibility.Apply(Car,WheelPivots,CarIndex,mode==2);}CarViewVisibility.SetWheels(WheelPivots,CarIndex,mode==2);
  var forward=body.GlobalBasis.Z;var target=Car.GlobalPosition+Vector3.Up*.8f;if(mode<2){var want=target-forward*(mode==0?7:11)+Vector3.Up*(mode==0?2:3.8f);using var query=PhysicsRayQueryParameters3D.Create(target,want,2);query.HitBackFaces=true;var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count>0){want=target+(want-target).Normalized()*Mathf.Max(0,target.DistanceTo((Vector3)hit["position"])-.35f);hit.Clear();}Cam.GlobalPosition=want;Cam.LookAt(target+forward*3);Cam.Fov=48;}
  else{Cam.GlobalPosition=Car.GlobalTransform*new Vector3(mode==2?-.35f:0,mode==2?.88f:.62f,mode==2?.05f:1.9f);Cam.LookAt(Cam.GlobalPosition+forward*10);Cam.Fov=mode==2?75:70;}
 }
}
}
