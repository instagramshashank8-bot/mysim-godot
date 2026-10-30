using System;
namespace MySimulator.Rebuild {
// Input smoothing only. Vehicle speed-dependent steering lock belongs to Dynamics.
public sealed class SteeringInput {
 public float Command {get;private set;}
 // Simulation headings increase toward +X while a +Z-facing Godot camera's
 // screen-right axis is -X. Convert human intent once; AI stays in model coordinates.
 public static float ToSimulation(float screenRight)=>-MathUtil.Clamp(MathUtil.Finite(screenRight)?screenRight:0,-1,1);
 public float Step(float requested,float dt,bool analog){
  requested=MathUtil.Finite(requested)?MathUtil.Clamp(requested,-1,1):0;
  if(!MathUtil.Finite(dt)||dt<=0)return Command;
  if(analog){Command=requested;return Command;}
  float rate=Math.Abs(requested)>.01f?2.6f:5f;
  if(Math.Sign(requested)!=Math.Sign(Command)&&Math.Abs(requested)>.01f)rate=5f;
  Command+=MathUtil.Clamp(requested-Command,-rate*dt,rate*dt);return Command;
 }
}
}
