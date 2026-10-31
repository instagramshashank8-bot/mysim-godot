using Godot;
using MySimulator.Rebuild;
using System;
namespace MySim {
// Cross-component checks against the actual integrated controller, body, race clock and camera nodes.
public partial class NativeGameplayChecks : Node {
 public NativeRaceController Drive;public bool Completed;float wallTime;int phase,frames,cycles;bool waitingUI;Vector3 pausedAt;float raceClock;
 void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public override async void _PhysicsProcess(double delta){if(Completed||waitingUI)return;try{wallTime+=(float)delta;if(Drive.Chassis==null)return;
  if(phase==0&&wallTime>=5){Drive.Race.paused=true;phase=1;frames=0;return;}
  if(phase==1){if(++frames<3)return;Check(Drive.Chassis.Freeze,"pause did not freeze native body");Check(Drive.Traffic.AllFrozen,"pause did not freeze native opponents");pausedAt=Drive.Chassis.GlobalPosition;raceClock=Drive.Race.clock;frames=0;phase=2;return;}
  if(phase==2){Check(Drive.Chassis.GlobalPosition.DistanceTo(pausedAt)<.0001f,"native body moved while paused");Check(Mathf.Abs(Drive.Race.clock-raceClock)<.000001f,"race clock advanced while paused");if(++frames<60)return;Drive.Race.paused=false;phase=3;frames=0;return;}
  if(phase==3){if(++frames<3)return;Check(!Drive.Chassis.Freeze,"native body stayed frozen after resume");Check(Drive.Hud.Player==Drive.Player&&Drive.Hud.Race==Drive.Race,"actual HUD lost native race binding");waitingUI=true;Drive.Hud.TogglePitStrategy();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(Drive.Hud.PitStrategy.Visible,"strategy panel did not open for native player");Drive.Hud.TogglePitStrategy();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(!Drive.Hud.PitStrategy.Visible,"strategy panel did not close");Drive.CycleCamera();phase=5;frames=0;waitingUI=false;return;}
  if(phase==5){if(++frames<2)return;bool expected=CarViewVisibility.WheelsVisible(Drive.CarIndex,Drive.CameraMode==2);foreach(var pivot in Drive.WheelPivots)Check(pivot.IsVisibleInTree()==expected,"camera/pit visibility policy lost a wheel assembly");if(Drive.CameraMode==2&&CarCatalog.RaceClass(Drive.CarIndex)==1)Check(Mathf.Abs(Drive.Car.ToLocal(Drive.Cam.GlobalPosition).X)<.001f,"open-wheel cockpit camera is offset from driver centreline");if(++cycles<4){Drive.CycleCamera();frames=0;return;}Check(Drive.CameraMode==0,"native camera cycle did not return to chase");Completed=true;GD.Print("PASS integrated native pause, clock, resume, pit options and four camera modes");}
 }catch(Exception e){SetPhysicsProcess(false);GD.PushError(e.ToString());CleanExit.Request(this,1);}}
}
}
