using Godot;
using System;
namespace MySim {
public static class NativePhysicsClock {
 public static void Configure(){int hz=int.Parse(System.Environment.GetEnvironmentVariable("MYSIM_NATIVE_PHYSICS_HZ")??"120");if(hz!=120&&hz!=240)throw new ArgumentOutOfRangeException("MYSIM_NATIVE_PHYSICS_HZ");Engine.PhysicsTicksPerSecond=hz;GD.Print("NATIVE_PHYSICS_HZ=",hz);}
}
}

