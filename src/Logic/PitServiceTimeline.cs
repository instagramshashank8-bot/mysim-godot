using System;
namespace MySimulator.Rebuild {
// One deterministic service clock for visual actions, wheel motion and sound triggers.
public static class PitServiceTimeline {
 public const float Duration=7.5f,LiftEnd=.8f,LowerStart=6.4f,LowerEnd=7.2f;
 public static float Loosen(int wheel)=>1f+wheel*.6f;
 public static float Remove(int wheel)=>Loosen(wheel)+.4f;
 public static float Fit(int wheel)=>3.4f+wheel*.6f;
 public static float Secure(int wheel)=>Fit(wheel)+.5f;
 static float Smooth(float value){value=MathUtil.Clamp(value,0,1);return value*value*(3-2*value);}
 public static float Lift(float elapsed)=>elapsed<0?0:elapsed<LiftEnd?.14f*Smooth(elapsed/LiftEnd):elapsed<LowerStart?.14f:.14f*(1-Smooth((elapsed-LowerStart)/(LowerEnd-LowerStart)));
 public static float WheelOut(float elapsed,int wheel){float remove=Remove(wheel),fit=Fit(wheel);if(elapsed<remove||elapsed>=fit+.45f)return 0;if(elapsed<remove+.35f)return Smooth((elapsed-remove)/.35f);if(elapsed<fit)return 1;return 1-Smooth((elapsed-fit)/.45f);}
 public static string Stage(float elapsed){if(elapsed<0)return "READY";if(elapsed<LiftEnd)return "JACK UP";if(elapsed<3.2f)return "WHEELS OFF";if(elapsed<6.3f)return "FIT & SECURE";if(elapsed<LowerEnd)return "JACK DOWN";return "RELEASE";}
 public static bool Crossed(float before,float now,float marker)=>before<marker&&now>=marker;
}
}
