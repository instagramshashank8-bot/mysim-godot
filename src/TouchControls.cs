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
  Make(root,"LEFT",new Vector2(32,562),new Vector2(130,118),v=>{leftHeld=v;UpdateSteer();});
  Make(root,"RIGHT",new Vector2(174,562),new Vector2(130,118),v=>{rightHeld=v;UpdateSteer();});
  Make(root,"GAS",new Vector2(1100,500),new Vector2(148,180),v=>{gasHeld=v;UpdateSteer();});
  Make(root,"BRAKE",new Vector2(940,562),new Vector2(148,118),v=>{brakeHeld=v;UpdateSteer();});
  Action(root,"PIT",new Vector2(24,80),()=>{if(Drive?.Player!=null)Drive.Player.pitRequested=true;});
  Action(root,"CAMERA",new Vector2(132,80),()=>Drive?.CycleCamera());
  pauseButton=Action(root,"PAUSE",new Vector2(264,80),()=>{if(Drive?.Race!=null){Drive.Race.paused=!Drive.Race.paused;Clear();}});
 }
 public override void _Process(double delta){if(pauseButton!=null&&Drive?.Race!=null)pauseButton.Text=Drive.Race.paused?"RESUME":"PAUSE";}
 void UpdateSteer(){
  bool Held(string action,bool mouse)=>mouse||fingers.ContainsValue(action);
  Steer=(Held("RIGHT",rightHeld)?1:0)-(Held("LEFT",leftHeld)?1:0);
  Throttle=Held("GAS",gasHeld)?1:0;Brake=Held("BRAKE",brakeHeld)?1:0;
 }
 public override void _Input(InputEvent input){
  if(!Active||input is not InputEventScreenTouch touch)return;
  if(!touch.Pressed){if(fingers.Remove(touch.Index)){UpdateSteer();GetViewport().SetInputAsHandled();}return;}
  foreach(var pair in drivingButtons)if(pair.Value.GetGlobalRect().HasPoint(touch.Position)){
   fingers[touch.Index]=pair.Key;UpdateSteer();GetViewport().SetInputAsHandled();return;
  }
 }
 void Clear(){fingers.Clear();leftHeld=rightHeld=gasHeld=brakeHeld=false;if(current==this)Steer=Throttle=Brake=0;}
 public override void _ExitTree(){if(IsInstanceValid(inputWindow))inputWindow.FocusExited-=Clear;Clear();if(current==this){current=null;Active=false;}}
 void Make(Control parent,string text,Vector2 pos,Vector2 size,System.Action<bool> set){
  bool right=text=="GAS"||text=="BRAKE";float left=pos.X-(right?1280:0),top=pos.Y-720;var button=new Button {Name=text,Text=text,AnchorLeft=right?1:0,AnchorRight=right?1:0,AnchorTop=1,AnchorBottom=1,OffsetLeft=left,OffsetRight=left+size.X,OffsetTop=top,OffsetBottom=top+size.Y,FocusMode=Control.FocusModeEnum.None};button.AddThemeFontSizeOverride("font_size",22);
  drivingButtons[text]=button;Style(button);button.ButtonDown+=()=>set(true);button.ButtonUp+=()=>set(false);parent.AddChild(button);
 }
 static void Style(Button button){
  button.AddThemeColorOverride("font_color",Colors.White);button.AddThemeColorOverride("font_hover_color",Colors.White);button.AddThemeColorOverride("font_pressed_color",Colors.White);
  foreach(string state in new[]{"normal","hover","pressed"})button.AddThemeStyleboxOverride(state,new StyleBoxFlat {BgColor=state=="pressed"?new Color(.06f,.35f,.42f,.98f):new Color(.035f,.05f,.07f,.92f),BorderColor=new Color(.45f,.62f,.7f,.9f),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8});
 }
 static Button Action(Control parent,string text,Vector2 pos,System.Action action){var button=new Button {Name=text,Text=text,Position=pos,Size=new Vector2(100,40),FocusMode=Control.FocusModeEnum.None};Style(button);button.Pressed+=action;parent.AddChild(button);return button;}
}
}
