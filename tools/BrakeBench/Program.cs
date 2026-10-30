using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MySimulator.Rebuild;
// Flat, uninterrupted road fixture: tyre/ABS dynamics only, not native collision validation.
class Program {
 static int Main(string[] args){
  int shard=int.Parse(args[0]);var circuit=new Circuit(0);var results=new List<object>();int failed=0;
  for(int car=shard*8;car<Math.Min(24,(shard+1)*8);car++)
  foreach(Compound compound in Enum.GetValues<Compound>())foreach(float water in new[]{0f,.5f,1f})
  foreach(float kmh in new[]{50f,100f})foreach(int hz in new[]{120,240}){
   var v=new Vehicle {carModel=car,onDrivingSurface=true,fuel=45,velocity=new Vec(0,kmh/3.6f),speed=kmh/3.6f,input=new DriverInput(0,1,0)};
   foreach(var tyre in v.tyres)tyre.Replace(compound);
   float time=0,distance=0;bool finite=true;float dt=1f/hz;
   while(time<60&&v.velocity.Magnitude>.5f){var before=v.position;Dynamics.Step(v,dt,water,circuit);distance+=(v.position-before).Magnitude;time+=dt;
    if(!float.IsFinite(v.position.x)||!float.IsFinite(v.position.z)||!float.IsFinite(v.velocity.Magnitude)){finite=false;break;}}
   bool stopped=finite&&v.velocity.Magnitude<=.5f&&distance>0;if(!stopped)failed++;
   results.Add(new {car,compound=compound.ToString(),water,initialKmh=kmh,hz,stopped,finite,seconds=time,metres=distance,finalSpeed=v.velocity.Magnitude,planningGrip=v.PlanningWeatherGrip(water)});
  }
  File.WriteAllText("braking-"+shard+".json",JsonSerializer.Serialize(new {fixture="flat continuous road; no collisions, native body, slope or real-world calibration",results,failed},new JsonSerializerOptions {WriteIndented=true}));
  Console.WriteLine($"Brake fixture shard {shard}: {results.Count} cases, {failed} failed stops");return failed==0?0:1;
 }
}
