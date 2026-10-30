using Godot;
using System;
using System.Collections.Generic;
using MySimulator.Rebuild;
namespace MySim {
public partial class NativeSuspensionScenario : Node3D {
 sealed class Trial {public NativeSuspensionVehicle body;public string kind;public Vector3 start;public float distance;public bool settled;}
 readonly List<Trial> trials=new();float elapsed;bool launched;
 public override void _Ready(){Engine.PhysicsTicksPerSecond=120;var ground=new StaticBody3D {CollisionLayer=1,CollisionMask=4,Position=new Vector3(0,-.5f,0)};ground.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(1200,1,600)}});AddChild(ground);
  int slot=0;foreach(int car in new[]{0,16})foreach(string kind in new[]{"right","left","dry-brake","wet-brake"}){
   var body=new NativeSuspensionVehicle {CarIndex=car,Water=kind=="wet-brake"?1:0,Position=new Vector3(-350+slot*90,.8f,-100)};AddChild(body);trials.Add(new Trial {body=body,kind=kind});slot++;
  }
 }
 public override void _PhysicsProcess(double delta){try{
  elapsed+=(float)delta;
  if(elapsed>=3&&!launched){launched=true;foreach(var trial in trials){var body=trial.body;trial.settled=body.Contacts==4&&Mathf.Abs(body.LinearVelocity.Y)<.15f&&body.Position.Y>.45f&&body.Position.Y<.9f;trial.start=body.Position;body.LinearVelocity=new Vector3(0,0,trial.kind.Contains("brake")?20:8);body.Controls=trial.kind.Contains("brake")?new DriverInput(0,1,0):new DriverInput(.1f,0,SteeringInput.ToSimulation(trial.kind=="right"?.25f:-.25f));}}
  if(!launched)return;
  foreach(var trial in trials){var p=trial.body.Position;if(!float.IsFinite(p.X)||!float.IsFinite(p.Y)||!float.IsFinite(p.Z))throw new Exception("Nonfinite native vehicle");trial.distance=Mathf.Max(trial.distance,p.DistanceTo(trial.start));}
  if(elapsed<13)return;
  var results=new List<object>();bool all=true;foreach(var trial in trials){var b=trial.body;float side=b.Position.X-trial.start.X;bool ok=trial.settled&&(trial.kind=="right"?side<-.4f:trial.kind=="left"?side>.4f:b.LinearVelocity.Length()<.5f);all&=ok;results.Add(new {car=b.CarIndex,trial=trial.kind,ok,settled=trial.settled,speed=b.LinearVelocity.Length(),lateralDisplacement=side,distance=trial.distance,height=b.Position.Y,contacts=b.Contacts});}
  for(int i=0;i<trials.Count;i+=4)all&=trials[i+3].distance>trials[i+2].distance;
  string path=System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_SUSPENSION_CHECKS");System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));System.IO.File.WriteAllText(path,System.Text.Json.JsonSerializer.Serialize(new {all,scope="Experimental flat-pad native rigidbody spring/tyre-force fixture; no production car/track, traffic or real-world calibration acceptance",results},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));
  GD.Print(all?"PASS native suspension, screen direction and wet braking":"FAIL native suspension fixture");SetPhysicsProcess(false);CleanExit.Request(this,all?0:1);
 }catch(Exception e){GD.PushError(e.ToString());SetPhysicsProcess(false);CleanExit.Request(this,1);}}
}
}
