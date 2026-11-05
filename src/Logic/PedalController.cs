namespace MySimulator.Rebuild {
 // Brake-to-reverse uses a latched gear: speed rising in reverse must not select forward again.
 public sealed class PedalController {
  float reverseHold; bool reversing;
  public bool Reversing => reversing;
  public DriverInput Update(float throttle,float brake,float steer,bool handbrake,float longitudinalSpeed,float dt,bool allowReverse) {
   if (!allowReverse) { reversing=false; reverseHold=0; }
   else if (!reversing) {
    if (brake>.1f && throttle<.05f && System.Math.Abs(longitudinalSpeed)<1.2f) reverseHold+=dt; else reverseHold=0;
    if(reverseHold>.25f) reversing=true;
   } else if (throttle>.05f && longitudinalSpeed>-.5f) { reversing=false; reverseHold=0; }
   // Forward pedal brakes a reversing car before changing direction; releasing both pedals coasts.
   if(reversing) return new DriverInput(0,throttle>.05f?throttle:brake,steer){handbrake=handbrake,reverse=throttle<.05f && brake>.1f};
   return new DriverInput(throttle,brake,steer){handbrake=handbrake};
  }
  // Profile-boundary reset: the brake-to-reverse latch must not leak into the next validation scenario.
  public void Reset(){reverseHold=0;reversing=false;}
 }
}
