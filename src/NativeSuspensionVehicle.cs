using Godot;
using MySimulator.Rebuild;
using System;
namespace MySim {
// Experimental force-driven native rigid body. No pose snapping, height clamping or heading assignment.
// Not production-integrated until suspension, tyre/contact and real-track gates pass.
public partial class NativeSuspensionVehicle : RigidBody3D {
 public int CarIndex;public DriverInput Controls;public float Water;public Compound Compound=Compound.Medium;
 public readonly bool[] Grounded=new bool[4];public readonly float[] Loads=new float[4];public readonly float[] SpringLength=new float[4];
 public float WheelRadius=.33f,RestLength=.18f,StaticSag=.07f,DampingRatio=.7f;public int Contacts;public float WheelAngle;
 Vector3[] mounts=new Vector3[4];Tyre[] tyres={new Tyre(),new Tyre(),new Tyre(),new Tyre()};float power,drag,brakeDecel;
 public override void _Ready(){
  CarIndex=Math.Clamp(CarIndex,0,23);Mass=RecoveredVehicleProfiles.Mass[CarIndex];CanSleep=false;LinearDamp=0;AngularDamp=.15f;CollisionLayer=4;CollisionMask=7;
  CenterOfMassMode=CenterOfMassModeEnum.Custom;CenterOfMass=new Vector3(0,-.3f,0);
  float wheelbase=RecoveredGeometry.Wheelbase[CarIndex],track=VehicleDimensions.Width[CarIndex]*.8f;
  for(int i=0;i<4;i++){mounts[i]=new Vector3(i%2==0?-track*.5f:track*.5f,-.32f+RestLength-StaticSag,i<2?wheelbase*.5f:-wheelbase*.5f);tyres[i].Replace(Compound);}
  AddChild(new CollisionShape3D {Shape=new BoxShape3D {Size=new Vector3(track,.5f,wheelbase+.5f)}});
  float vmax=RecoveredVehicleProfiles.Speed[CarIndex]*.44704f;power=Mass*26.82f*26.82f/(2*RecoveredVehicleProfiles.Acceleration[CarIndex])*1.35f;drag=power/(vmax*vmax*vmax);brakeDecel=26.82f*26.82f/(2*RecoveredVehicleProfiles.Brakes[CarIndex]*.3048f);
 }
 public override void _IntegrateForces(PhysicsDirectBodyState3D state){
  var input=Controls.Safe();var pose=state.Transform;var up=pose.Basis.Y.Normalized();var forward=pose.Basis.Z.Normalized();float longitudinal=state.LinearVelocity.Dot(forward),speed=state.LinearVelocity.Length();
  WheelAngle=Mathf.MoveToward(WheelAngle,Dynamics.SteeringTarget(input.steer,longitudinal),3.2f*state.Step);
  float gravity=state.TotalGravity.Length(),spring=Mass*gravity/(4*StaticSag),damper=2*DampingRatio*Mathf.Sqrt(spring*Mass*.25f);
  float engine=input.reverse?-Mass*3.5f*(input.throttle+input.brake)*.5f:Mathf.Min(power/Mathf.Max(Mathf.Abs(longitudinal),6),Mass*gravity*2.2f)*input.throttle;
  Contacts=0;
  for(int i=0;i<4;i++){
   Grounded[i]=false;Loads[i]=0;SpringLength[i]=RestLength+.15f;var mount=pose*mounts[i];
   using var query=PhysicsRayQueryParameters3D.Create(mount,mount-up*(WheelRadius+RestLength+.15f),1);
   var hit=state.GetSpaceState().IntersectRay(query);if(hit.Count==0)continue;
   var point=(Vector3)hit["position"];var normal=((Vector3)hit["normal"]).Normalized();hit.Clear();
   float length=Mathf.Max(0,mount.DistanceTo(point)-WheelRadius);SpringLength[i]=length;
   var arm=point-pose.Origin;var velocity=state.GetVelocityAtLocalPosition(arm);
   float load=Mathf.Clamp(spring*(RestLength-length)-damper*velocity.Dot(normal),0,Mass*gravity);if(load<=0)continue;
   Grounded[i]=true;Loads[i]=load;Contacts++;state.ApplyForce(normal*load,arm);
   var rolling=(i<2?forward.Rotated(up,WheelAngle):forward);rolling=(rolling-normal*rolling.Dot(normal)).Normalized();var right=rolling.Cross(normal).Normalized();
   float u=velocity.Dot(rolling),side=velocity.Dot(right),mu=tyres[i].Grip(Water)*1.18f*Dynamics.GripMul(CarIndex),capacity=mu*load;
   float service=input.reverse?0:input.brake*brakeDecel*Mass*(i<2?.29f:.21f);if(input.handbrake&&i>=2)service+=Mass*1.5f;
   // A brake impulse cannot reverse a wheel's supported share of body momentum.
   service=Mathf.Min(service,Mathf.Abs(u)*Mass/(4*Mathf.Max(state.Step,.00001f)));
   float fx=(i>=2?engine*.5f:0)-Mathf.Sign(u)*service;
   fx=Mathf.Clamp(fx,-capacity,capacity);float remaining=Mathf.Sqrt(Mathf.Max(0,capacity*capacity-fx*fx));
   float alpha=Mathf.Atan2(side,Mathf.Max(Mathf.Abs(u),1));float fy=-remaining*(float)Math.Tanh(alpha/.12f);
   float roll=Mathf.Min(load*.014f,Mathf.Abs(u)*Mass/(4*Mathf.Max(state.Step,.00001f)));
   state.ApplyForce(rolling*(fx-Mathf.Sign(u)*roll)+right*fy,arm);
   tyres[i].Step(state.Step,Mathf.Abs(u),Mathf.Abs(alpha),load/Mathf.Max(1,Mass*gravity*.25f),Water);
  }
  state.ApplyCentralForce(-state.LinearVelocity*speed*drag-up*(Dynamics.DownK(CarIndex)*speed*speed));
 }
}
}
