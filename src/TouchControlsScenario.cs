using Godot;
using System;
namespace MySim {
public partial class TouchControlsScenario : Node {
 void Check(bool yes,string reason){if(!yes)throw new Exception(reason);}
 public override async void _Ready(){try{
  var controls=new TouchControls();AddChild(controls);
  for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Vector2 Centre(string name)=>((Button)controls.FindChild(name,true,false)).GetGlobalRect().GetCenter();
  void Finger(int id,string action,bool pressed)=>controls._Input(new InputEventScreenTouch {Index=id,Position=Centre(action),Pressed=pressed});
  Finger(11,"LEFT",true);Finger(22,"GAS",true);
  Check(TouchControls.Steer==-1&&TouchControls.Throttle==1,"Simultaneous steering/throttle failed");
  Finger(11,"LEFT",false);Check(TouchControls.Steer==0&&TouchControls.Throttle==1,"Releasing steering released another finger");
  Finger(33,"BRAKE",true);Check(TouchControls.Brake==1&&TouchControls.Throttle==1,"Independent brake finger failed");
  Finger(22,"GAS",false);Check(TouchControls.Throttle==0&&TouchControls.Brake==1,"Releasing gas released brake");
  Finger(33,"BRAKE",false);Finger(44,"LEFT",true);Finger(55,"RIGHT",true);
  Check(TouchControls.Steer==0,"Opposing fingers did not cancel");
  GetWindow().EmitSignal(Window.SignalName.FocusExited);
  Check(TouchControls.Steer==0&&TouchControls.Throttle==0&&TouchControls.Brake==0,"Focus loss stuck controls");
  var race=new MySimulator.Rebuild.Race(0,3,0);var player=race.Add("PLAYER",true,MySimulator.Rebuild.Compound.Medium);
  var strategy=new PitStrategyPanel {Race=race,Player=player};AddChild(strategy);
  strategy.Toggle();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  Check(strategy.Visible,"Pit options did not open");var compounds=(OptionButton)strategy.FindChild("COMPOUND",true,false);compounds.Select(3);compounds.EmitSignal(OptionButton.SignalName.ItemSelected,3L);
  ((Button)strategy.FindChild("REQUEST",true,false)).EmitSignal(BaseButton.SignalName.Pressed);
  Check(player.pitRequested&&player.nextCompound==MySimulator.Rebuild.Compound.Wet&&!strategy.Open,"Pit request lost compound or did not close");
  strategy.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
  string path=System.Environment.GetEnvironmentVariable("MYSIM_SHOT");if(path!=null)GetViewport().GetTexture().GetImage().SavePng(path);
  GD.Print("PASS independent touch fingers and focus clearing");GetTree().Quit();
 }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
}
