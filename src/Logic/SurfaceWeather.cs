using System;
namespace MySimulator.Rebuild {
// Surface water evolves continuously from rainfall, drainage and evaporation rates (per second).
public sealed class SurfaceWeather {
 public float Water {get;private set;}
 public SurfaceWeather(float initial){if(!float.IsFinite(initial)||initial<0||initial>1)throw new ArgumentOutOfRangeException(nameof(initial));Water=initial;}
 public void Step(float seconds,float rainfall,float drying){
  if(!float.IsFinite(seconds)||seconds<0||!float.IsFinite(rainfall)||rainfall<0||!float.IsFinite(drying)||drying<0)throw new ArgumentOutOfRangeException("Weather rates/time must be finite and nonnegative");
  float rate=rainfall+drying;if(rate==0||seconds==0)return;float equilibrium=rainfall/rate;Water=MathUtil.Clamp(equilibrium+(Water-equilibrium)*(float)Math.Exp(-rate*seconds),0,1);
 }
}
}
