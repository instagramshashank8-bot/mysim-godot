using Godot;
using MySimulator.Rebuild;
using System;
using System.Collections.Generic;
namespace MySim {
// Experimental native authority for opponents: the race model requests controls,
// while each physical chassis owns position, rotation and contact response.
public sealed class NativeTraffic {
 sealed class Entry {public Vehicle RaceCar;public NativeSuspensionVehicle Body;public Node3D Model;public Node3D[] Wheels;public Vector3[] Homes;public float Travel;public bool Paused,Parked;public Vector3 Velocity,Angular;}
 readonly List<Entry> entries=new();readonly NativeRaceController owner;
 public int Count=>entries.Count;public int RuleResets {get;private set;}
 public NativeTraffic(NativeRaceController owner,int count){this.owner=owner;if(count<0||count>19)throw new ArgumentOutOfRangeException(nameof(count));
  for(int i=0;i<count;i++){int car=owner.CarIndex;do{car=(car+1)%CarCatalog.Count;}while(CarCatalog.RaceClass(car)!=CarCatalog.RaceClass(owner.CarIndex));
   // Cycle within the selected class; repeated models are separate physical cars.
   for(int j=0;j<i;j++){do{car=(car+1)%CarCatalog.Count;}while(CarCatalog.RaceClass(car)!=CarCatalog.RaceClass(owner.CarIndex));}
   var raceCar=owner.Race.Add("AI "+(i+1),false,Compound.Medium);raceCar.carModel=car;raceCar.externalPhysics=true;raceCar.nativeTyreSteering=true;raceCar.externalResetRequested=false;
   entries.Add(new Entry {RaceCar=raceCar});
  }
 }
 bool Place(Entry entry,bool reset){var v=entry.RaceCar;var start=new Vector3(v.position.x,200,v.position.z);using var query=PhysicsRayQueryParameters3D.Create(start,start-Vector3.Up*400,1);query.HitBackFaces=true;var hit=owner.GetWorld3D().DirectSpaceState.IntersectRay(query);if(hit.Count==0)return false;var ground=(Vector3)hit["position"];hit.Clear();
  if(entry.Body==null){int car=v.carModel;entry.Model=CarLoader.Load(CarCatalog.Folders[car]);owner.Car.GetParent().AddChild(entry.Model);CarLoader.ApplyExact(entry.Model,car);entry.Wheels=CarLoader.AttachWheels(entry.Model,car);entry.Homes=new Vector3[4];var centres=new Vector3[4];var radii=new float[4];for(int i=0;i<4;i++){entry.Homes[i]=entry.Wheels[i].Position;centres[i]=entry.Homes[i]-Vector3.Up*.65f;radii[i]=entry.Wheels[i].GetMeta("radius").AsSingle();}
   entry.Body=new NativeSuspensionVehicle {CarIndex=car,RecoveredWheelCentres=centres,RecoveredWheelRadii=radii,Position=ground+Vector3.Up*.8f,Rotation=new Vector3(0,v.heading,0)};owner.AddChild(entry.Body);v.tyres=entry.Body.Tyres;
  }else{entry.Body.GlobalPosition=ground+Vector3.Up*.65f;entry.Body.Rotation=new Vector3(0,v.heading,0);entry.Body.LinearVelocity=entry.Body.AngularVelocity=Vector3.Zero;if(reset)RuleResets++;}
  v.externalResetRequested=false;return true;
 }
 public bool Read(){foreach(var e in entries){if(e.Body==null&&!Place(e,false))return false;var b=e.Body;var v=e.RaceCar;if(!b.GlobalPosition.IsFinite()||!b.LinearVelocity.IsFinite())throw new Exception("Nonfinite native opponent chassis");v.position=new Vec(b.GlobalPosition.X,b.GlobalPosition.Z);v.velocity=new Vec(b.LinearVelocity.X,b.LinearVelocity.Z);v.speed=b.LinearVelocity.Length();v.heading=Mathf.Atan2(b.GlobalBasis.Z.X,b.GlobalBasis.Z.Z);v.yawRate=b.AngularVelocity.Y;v.steering=b.WheelAngle;v.progress=owner.Race.track.Find(v.position,out v.lateral);v.surfaceKnown=true;v.onDrivingSurface=b.Contacts>0&&owner.Race.track.OnRoad(v.progress,v.lateral);}
  return true;
 }
 public void Pause(bool pause){foreach(var e in entries){if(e.Body==null)continue;if(pause&&!e.Paused){e.Velocity=e.Body.LinearVelocity;e.Angular=e.Body.AngularVelocity;e.Body.Freeze=true;e.Paused=true;}else if(!pause&&e.Paused){e.Body.Freeze=e.Parked;e.Body.LinearVelocity=e.Velocity;e.Body.AngularVelocity=e.Angular;e.Paused=false;}}}
 public void Apply(float dt){foreach(var e in entries){var b=e.Body;var v=e.RaceCar;if(v.externalResetRequested&&!Place(e,true))throw new Exception("Native opponent reset has no drive surface");if(v.servicing&&!e.Parked){if(!Place(e,false))throw new Exception("Native opponent pit has no drive surface");b.Freeze=true;e.Parked=true;}else if(!v.servicing&&e.Parked){b.Freeze=false;e.Parked=false;TyreAppearance.Apply(e.Wheels,v.tyres[0].compound);}
   b.Water=owner.Race.wetness;b.TractionControl=v.traction;b.BrakeStabilityAssist=v.abs;b.Controls=v.servicing?new DriverInput(0,1,0):v.input;
   e.Model.GlobalTransform=new Transform3D(b.GlobalBasis,b.GlobalPosition-b.GlobalBasis.Y*.65f);e.Travel+=b.LinearVelocity.Dot(b.GlobalBasis.Z)*dt;WheelAnimation.Update(e.Wheels,e.Travel,b.WheelAngle);for(int i=0;i<4;i++)e.Wheels[i].Position=e.Homes[i]+Vector3.Up*(b.RestLength-b.StaticSag-b.SpringLength[i]);
  }}
 public bool AllFrozen {get{foreach(var e in entries)if(e.Body==null||!e.Body.Freeze)return false;return true;}}
}
}
