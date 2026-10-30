using System;
namespace MySimulator.Rebuild {
// Input smoothing only. Vehicle speed-dependent steering lock belongs to Dynamics.
public sealed class SteeringInput {
 public float Command {get;private set;}
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
