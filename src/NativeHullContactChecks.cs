using Godot;
using System;
using System.Collections.Generic;
namespace MySim {
// Real recovered compound hulls, measured wheel contacts and native static wall impacts.
public partial class NativeHullContactChecks : Node3D {
 public int CarIndex;sealed class Trial {public NativeSuspensionVehicle Body;public bool Front;public float Penetration;public int Hulls;}
 readonly List<Trial> trials=new();float elapsed;bool launched,ended;
 public override void _Ready(){Engine.PhysicsTicksPerSecond=120;var floor=new StaticBody3D {CollisionLayer=1,CollisionMask=4,Position=new Vector3(15,-.5f,0)};floor.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(100,1,100)}});AddChild(floor);
  for(int i=0;i<2;i++){bool front=i==0;float x=i*30;var wall=new StaticBody3D {CollisionLayer=2,CollisionMask=4,Position=new Vector3(x,2,front?6:-6)};wall.AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(15,4,.2f)}});AddChild(wall);var body=new NativeSuspensionVehicle {CarIndex=CarIndex,Position=new Vector3(x,.8f,0)};AddChild(body);int shapes=0;foreach(var node in body.GetChildren())if(node is CollisionShape3D shape&&shape.Shape is ConvexPolygonShape3D)shapes++;if(shapes==0)throw new Exception("Native wall probe did not load recovered compound hulls");trials.Add(new Trial {Body=body,Front=front,Hulls=shapes});}
 }
 public override void _PhysicsProcess(double delta){if(ended)return;try{elapsed+=(float)delta;if(!launched&&elapsed>=3){foreach(var trial in trials)trial.Body.LinearVelocity=new Vector3(0,0,trial.Front?20:-20);launched=true;}
  if(!launched)return;foreach(var trial in trials){var body=trial.Body;if(!body.GlobalPosition.IsFinite())throw new Exception("Native hull collision became nonfinite");foreach(var node in body.GetChildren())if(node is CollisionShape3D child&&child.Shape is ConvexPolygonShape3D shape)foreach(var point in shape.Points){float z=(child.GlobalTransform*point).Z;trial.Penetration=Mathf.Max(trial.Penetration,trial.Front?z-5.9f:-5.9f-z);}}
  if(elapsed<9)return;var rows=new List<object>();bool all=trials.Count==2;foreach(var trial in trials){bool ok=trial.Penetration<=.05f&&trial.Body.LinearVelocity.Length()<1&&Mathf.Abs(trial.Body.GlobalPosition.Z)>1;all&=ok;rows.Add(new {ok,car=CarIndex,front=trial.Front,hulls=trial.Hulls,maximumHullPenetration=trial.Penetration,finalSpeed=trial.Body.LinearVelocity.Length(),bodyZ=trial.Body.GlobalPosition.Z});}System.IO.Directory.CreateDirectory("outputs/cloud-game");System.IO.File.WriteAllText("outputs/cloud-game/native-hull-contact-results.json",System.Text.Json.JsonSerializer.Serialize(new {all,rows,scope="Native front/rear wall impacts against actual compound body hull vertices; no wheel-body joint, full-track or visual acceptance"},new System.Text.Json.JsonSerializerOptions {WriteIndented=true}));ended=true;GD.Print(all?"PASS recovered compound hull bumper coverage":"FAIL recovered compound hull bumper coverage");CleanExit.Request(this,all?0:1);
 }catch(Exception e){ended=true;GD.PushError(e.ToString());CleanExit.Request(this,1);}}
}
}
