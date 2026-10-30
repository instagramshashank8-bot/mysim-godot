using Godot;
using System;
namespace MySim {
// Exercises the real controls dialog through button signals and input events; restores every original binding.
public partial class ControlsScenario : Node {
 void Check(bool value,string why){if(!value)throw new Exception(why);}
 async System.Threading.Tasks.Task KeyEvent(Key key){Input.ParseInputEvent(new InputEventKey {Keycode=key,PhysicalKeycode=key,Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Input.ParseInputEvent(new InputEventKey {Keycode=key,PhysicalKeycode=key,Pressed=false});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 public override async void _Ready(){
  var original=new Key[ControlBindings.Actions.Length];for(int i=0;i<original.Length;i++)original[i]=ControlBindings.Get(i);
  try {
   var wheelRoot=new Node3D();AddChild(wheelRoot);var wheelPivots=new Node3D[4];
   for(int i=0;i<4;i++){float baseYaw=i%2==0?0:Mathf.Pi;var pivot=new Node3D {Rotation=new Vector3(0,baseYaw,0)};pivot.SetMeta("steer_base_yaw",baseYaw);pivot.SetMeta("radius",.33f);pivot.AddChild(new Node3D {Name="Spin"});wheelRoot.AddChild(pivot);wheelPivots[i]=pivot;}
   foreach(float steering in new[]{0f,.52f,-.52f,0f}){WheelAnimation.Update(wheelPivots,2,steering);for(int i=0;i<4;i++){var pivot=wheelPivots[i];float expected=pivot.GetMeta("steer_base_yaw").AsSingle()+(i<2?steering:0);Check(Mathf.Abs(Mathf.Wrap(pivot.Rotation.Y-expected,-Mathf.Pi,Mathf.Pi))<.0001f,"wheel side orientation or steering travel changed");var spin=pivot.GetNode<Node3D>("Spin");Check(spin.Rotation.X*Mathf.Cos(pivot.GetMeta("steer_base_yaw").AsSingle())>0,"forward rolling wheel spins backwards");}}
   wheelRoot.QueueFree();GD.Print("PASS mirrored wheel bases preserved, full steering and forward spin on both sides");
   ControlBindings.Reset();var panel=new ControlsPanel();AddChild(panel);
   await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   var content=panel.GetNode<Control>("Content");var size=GetViewport().GetVisibleRect().Size;Check(content.Position.X>=0&&content.Position.Y>=0&&content.Position.Y+700*content.Scale.Y<=size.Y+1,"controls extend outside viewport");
   var binding=panel.GetNode<Button>("Content/Binding0");binding.EmitSignal(BaseButton.SignalName.Pressed);await KeyEvent(Key.K);
   Check(ControlBindings.Get(0)==Key.K,"new throttle key not stored");
   Input.ParseInputEvent(new InputEventKey {Keycode=Key.K,PhysicalKeycode=Key.K,Pressed=true});await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(!DisplayServer.WindowIsFocused() ? !ControlBindings.Held(0) : ControlBindings.Held(0),"rebound throttle or inactive-window input guard failed");await KeyEvent(Key.K);
   binding.EmitSignal(BaseButton.SignalName.Pressed);await KeyEvent(Key.S);Check(ControlBindings.Get(0)==Key.K,"conflicting brake key accepted");
   await KeyEvent(Key.Left);Check(ControlBindings.Get(0)==Key.K,"reserved steering arrow bound to throttle");
   await KeyEvent(Key.Escape);Check(IsInstanceValid(panel)&&!panel.IsQueuedForDeletion(),"cancel closed the entire dialog");
   binding.EmitSignal(BaseButton.SignalName.Pressed);await KeyEvent(Key.Tab);Check(ControlBindings.Get(0)==Key.K,"reserved pause key accepted");await KeyEvent(Key.Escape);
   panel.GetNode<Button>("Content/RestoreDefaults").EmitSignal(BaseButton.SignalName.Pressed);Check(ControlBindings.Get(0)==Key.W,"reset did not restore controls");
   if(DisplayServer.GetName()!="headless")GetViewport().GetTexture().GetImage().SavePng(System.Environment.GetEnvironmentVariable("MYSIM_SHOT")??"outputs/cloud-game/controls.png");
   await KeyEvent(Key.Escape);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(!IsInstanceValid(panel),"back did not close controls");
   bool originalScreen=Settings.Get("screen_controls",true);
   Settings.Set("screen_controls",true);
   try {
    var car=new MySimulator.Rebuild.Race(0,1,0);var player=car.Add("Input test",true,MySimulator.Rebuild.Compound.Medium);
    var drive=new DriveController {Race=car,Player=player};
    var hud=new Hud {Race=car,Player=player};AddChild(hud);drive.Hud=hud;
    var view=new Camera3D {Position=new Vector3(0,3,-8)};AddChild(view);view.LookAt(new Vector3(0,3,0),Vector3.Up);Check(view.GlobalBasis.X.X<-.99f,"Native chase camera horizontal convention changed");view.QueueFree();
    var oldControls=new TouchControls {Drive=drive};AddChild(oldControls);
    var newControls=new TouchControls {Drive=drive};AddChild(newControls);oldControls.QueueFree();
    await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    Check(TouchControls.Active,"old race disabled new controls");
    Button Find(string name) { foreach(var node in newControls.FindChildren("*","Button",true,false))if(node.Name==name)return (Button)node;throw new Exception("Missing GUI control "+name); }
    Find("GAS").EmitSignal(BaseButton.SignalName.ButtonDown);Check(TouchControls.Throttle==1,"screen throttle did not engage");Find("GAS").EmitSignal(BaseButton.SignalName.ButtonUp);Check(TouchControls.Throttle==0,"screen throttle stuck");
    Find("LEFT").EmitSignal(BaseButton.SignalName.ButtonDown);Find("RIGHT").EmitSignal(BaseButton.SignalName.ButtonDown);Check(TouchControls.Steer==0,"opposite steering buttons conflict");Find("LEFT").EmitSignal(BaseButton.SignalName.ButtonUp);Check(TouchControls.Steer==1,"steering release loses other held button");Find("RIGHT").EmitSignal(BaseButton.SignalName.ButtonUp);
    Find("PIT").EmitSignal(BaseButton.SignalName.Pressed);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
    var strategy=hud.PitStrategy;Check(strategy.Open&&strategy.Visible,"GUI pit options failed to open");
    var compound=(OptionButton)strategy.FindChild("COMPOUND",true,false);compound.Select(3);compound.EmitSignal(OptionButton.SignalName.ItemSelected,3L);
    ((Button)strategy.FindChild("REQUEST",true,false)).EmitSignal(BaseButton.SignalName.Pressed);Check(player.pitRequested&&player.nextCompound==MySimulator.Rebuild.Compound.Wet&&!strategy.Open,"GUI pit request/compound/close flow failed");
    Find("PAUSE").EmitSignal(BaseButton.SignalName.Pressed);Check(car.paused&&TouchControls.Throttle==0&&TouchControls.Steer==0,"GUI pause did not clear pedals");
    Find("PAUSE").EmitSignal(BaseButton.SignalName.Pressed);Check(!car.paused&&TouchControls.Throttle==0,"GUI resume did not restore racing safely");
    newControls.QueueFree();await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(!TouchControls.Active&&TouchControls.Throttle==0,"screen controls leak after exit");hud.QueueFree();drive.Free();
    GD.Print("PASS screen controls throttle/release, simultaneous steering, pit, pause and restart lifecycle");
   }finally{Settings.Set("screen_controls",originalScreen);}
   GD.Print("PASS controls dialog rebinding, persistence, physical input, conflict rejection, reserved pause, cancel, defaults and back");GetTree().Quit();
  }catch(Exception e){GD.PrintErr("FAIL controls dialog ",e);GetTree().Quit(1);}
  finally {for(int i=0;i<original.Length;i++)Settings.Set("key_"+i,(long)original[i]);}
 }
}
}
