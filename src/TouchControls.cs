using Godot;
namespace MySim {
// Mouse/touch controls are visible by default and can be disabled in Settings.
public partial class TouchControls : CanvasLayer {
 public DriveController Drive;
 public static float Steer,Throttle,Brake;public static bool Active;static TouchControls current;bool leftHeld,rightHeld,gasHeld,brakeHeld;readonly System.Collections.Generic.Dictionary<int,string> fingers=new();readonly System.Collections.Generic.Dictionary<string,Button> drivingButtons=new();Window inputWindow;Button pauseButton;
 public override void _Ready(){
  Layer=5;current=this;Clear();Active=Settings.Get("screen_controls",true)||DisplayServer.IsTouchscreenAvailable()||System.Environment.GetEnvironmentVariable("MYSIM_TOUCH")!=null;
  inputWindow=GetWindow();inputWindow.FocusExited+=Clear;
  if(!Active)return;
  var root=new Control { AnchorRight=1,AnchorBottom=1,MouseFilter=Control.MouseFilterEnum.Ignore };AddChild(root);
  Make(root,"LEFT",new Vector2(30,588),new Vector2(96,96),v=>{leftHeld=v;UpdateSteer();});
  Make(root,"RIGHT",new Vector2(140,588),new Vector2(96,96),v=>{rightHeld=v;UpdateSteer();});
  Make(root,"GAS",new Vector2(1162,538),new Vector2(86,146),v=>{gasHeld=v;UpdateSteer();});
  Make(root,"BRAKE",new Vector2(1038,588),new Vector2(108,96),v=>{brakeHeld=v;UpdateSteer();});
  Action(root,"PIT",new Vector2(24,94),()=>{if(Drive?.Player!=null)Drive.Player.pitRequested=true;});
  Action(root,"CAMERA",new Vector2(112,94),()=>Drive?.CycleCamera());
  pauseButton=Action(root,"PAUSE",new Vector2(208,94),()=>{if(Drive?.Race!=null){Drive.Race.paused=!Drive.Race.paused;Clear();}});
 }
 public override void _Process(double delta){if(pauseButton!=null&&Drive?.Race!=null){var button=(RaceControlButton)pauseButton;string kind=Drive.Race.paused?"RESUME":"PAUSE";if(button.Kind!=kind){button.Kind=kind;button.QueueRedraw();}}}
 void UpdateSteer(){
  bool Held(string action,bool mouse)=>mouse||fingers.ContainsValue(action);
  Steer=(Held("RIGHT",rightHeld)?1:0)-(Held("LEFT",leftHeld)?1:0);
  Throttle=Held("GAS",gasHeld)?1:0;Brake=Held("BRAKE",brakeHeld)?1:0;
  foreach(var pair in drivingButtons)((RaceControlButton)pair.Value).SetHeld(pair.Key=="LEFT"?Held("LEFT",leftHeld):pair.Key=="RIGHT"?Held("RIGHT",rightHeld):pair.Key=="GAS"?Throttle>0:Brake>0);
 }
 public override void _Input(InputEvent input){
  if(!Active||input is not InputEventScreenTouch touch)return;
  if(!touch.Pressed){if(fingers.Remove(touch.Index)){UpdateSteer();GetViewport().SetInputAsHandled();}return;}
  foreach(var pair in drivingButtons)if(pair.Value.GetGlobalRect().HasPoint(touch.Position)){
   fingers[touch.Index]=pair.Key;UpdateSteer();GetViewport().SetInputAsHandled();return;
  }
 }
 void Clear(){foreach(var button in drivingButtons.Values)((RaceControlButton)button).SetHeld(false);fingers.Clear();leftHeld=rightHeld=gasHeld=brakeHeld=false;if(current==this)Steer=Throttle=Brake=0;}
 public override void _ExitTree(){if(IsInstanceValid(inputWindow))inputWindow.FocusExited-=Clear;Clear();if(current==this){current=null;Active=false;}}
 void Make(Control parent,string text,Vector2 pos,Vector2 size,System.Action<bool> set){
  bool right=text=="GAS"||text=="BRAKE";float left=pos.X-(right?1280:0),top=pos.Y-720;var button=new RaceControlButton {Kind=text,TooltipText=text=="GAS"?"Throttle ? hold":text+" ? hold",Name=text,Text="",AnchorLeft=right?1:0,AnchorRight=right?1:0,AnchorTop=1,AnchorBottom=1,OffsetLeft=left,OffsetRight=left+size.X,OffsetTop=top,OffsetBottom=top+size.Y,FocusMode=Control.FocusModeEnum.None};button.AddThemeFontSizeOverride("font_size",22);
  drivingButtons[text]=button;button.ButtonDown+=()=>set(true);button.ButtonUp+=()=>set(false);parent.AddChild(button);
 }
 static Button Action(Control parent,string text,Vector2 pos,System.Action action){var button=new RaceControlButton {Kind=text,TooltipText=text,Name=text,Text="",Position=pos,Size=new Vector2(text=="CAMERA"?88:80,38),FocusMode=Control.FocusModeEnum.None};button.Pressed+=action;parent.AddChild(button);return button;}
}
}
