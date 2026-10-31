using Godot;
using MySimulator.Rebuild;
using System;
using System.Collections.Generic;
namespace MySim {
// Isolated real-scene force-driven trial. Production DriveController is not created in this mode.
public partial class NativeTrackTrial : Node3D {
 public int TrackIndex,CarIndex;public Node3D Car;public Node3D[] Wheels;public string Destination;
 NativeSuspensionVehicle body;Camera3D camera;float time,distance,maxSpeed,spin;int frames,groundedSamples,totalSamples;bool initialized,ending;Vector3 start;float initialHeading,sampledYaw;bool sampled;string turn=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_TRACK_STEER");readonly List<object> trace=new();
 public override void _Ready(){Engine.PhysicsTicksPerSecond=120;camera=new Camera3D {Current=true,Fov=48,Far=4000};AddChild(camera);}
 public override void _PhysicsProcess(double delta){if(ending)return;try{
  if(!initialized){if(++frames<3)return;var circuit=new Circuit(TrackIndex);var position=circuit.Point(.988f);using var query=PhysicsRayQueryParameters3D.Create(new Vector3(position.x,200,position.z),new Vector3(position.x,-200,position.z),1);query.HitBackFaces=true;var hit=GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count==0)throw new Exception("Native track spawn has no source drive surface");var ground=(Vector3)hit["position"];hit.Clear();
   body=new NativeSuspensionVehicle {CarIndex=CarIndex,Position=ground+Vector3.Up*.8f,Rotation=new Vector3(0,circuit.Heading(.988f),0)};AddChild(body);Car.Reparent(body,false);Car.Position=new Vector3(0,-.65f,0);Car.Rotation=Vector3.Zero;start=body.GlobalPosition;initialHeading=body.Rotation.Y;initialized=true;return;
  }
  time+=(float)delta;body.Controls=time<3?new DriverInput(0,0,0):time<7?new DriverInput(.3f,0,turn!=null&&time>=6?SteeringInput.ToSimulation(turn=="right"?.15f:-.15f):0):new DriverInput(0,1,0);
  if(!sampled&&time>=7){sampled=true;sampledYaw=Mathf.Wrap(body.Rotation.Y-initialHeading,-Mathf.Pi,Mathf.Pi);}
  var p=body.GlobalPosition;float speed=body.LinearVelocity.Length();if(!float.IsFinite(p.Y)||p.Y<start.Y-10)throw new Exception("Native track chassis fell through the recovered surface");maxSpeed=Mathf.Max(maxSpeed,speed);distance=Mathf.Max(distance,new Vector2(p.X-start.X,p.Z-start.Z).Length());
  if(time>2.5f){totalSamples++;if(body.Contacts>=3)groundedSamples++;}
  spin+=body.LinearVelocity.Dot(body.GlobalBasis.Z)*(float)delta;WheelAnimation.Update(Wheels,spin,body.WheelAngle);var forward=body.GlobalBasis.Z.Normalized();camera.GlobalPosition=p-forward*7+Vector3.Up*2.5f;camera.LookAt(p+forward*3);
  if(frames++%12==0)trace.Add(new {time,x=p.X,y=p.Y,z=p.Z,speed,contacts=body.Contacts,verticalSpeed=body.LinearVelocity.Y});
  if(time<11)return;bool ok=maxSpeed>8&&distance>10&&speed<1&&groundedSamples>totalSamples*.9f&&(turn==null||(sampled&&(turn=="right"?sampledYaw<-.02f:sampledYaw>.02f)));ending=true;var result=new {ok,track=TrackIndex,car=CarIndex,turn,sampledYaw,maxSpeed,distance,finalSpeed=speed,groundedSamples,totalSamples,trace,scope="Isolated acceleration/braking on recovered scene; no full lap, traffic, calibration or production acceptance"};System.IO.File.WriteAllText(Destination,System.Text.Json.JsonSerializer.Serialize(result,new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));GD.Print(ok?"PASS force-driven native track trial":"FAIL force-driven native track trial");Finish(ok);
 }catch(Exception e){GD.PushError(e.ToString());ending=true;CleanExit.Request(this,1);}}
 async void Finish(bool ok){string shot=System.Environment.GetEnvironmentVariable("MYSIM_SHOT");if(shot!=null){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);var error=GetViewport().GetTexture().GetImage().SavePng(shot);if(error!=Error.Ok){GD.PushError("Native physical scene capture failed: "+error);ok=false;}}CleanExit.Request(this,ok?0:1);}
}
}
