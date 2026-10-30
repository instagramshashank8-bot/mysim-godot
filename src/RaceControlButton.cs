using Godot;
namespace MySim {
// Resolution-independent race controls: translucent thumb pads and pedal silhouettes.
public partial class RaceControlButton : Button {
 public string Kind;bool held;
 static readonly Color White=new Color(.91f,.96f,.98f),Teal=new Color(.25f,.85f,.79f),Amber=new Color(1f,.65f,.32f);
 public override void _Ready(){Text="";MouseDefaultCursorShape=CursorShape.PointingHand;FocusMode=FocusModeEnum.None;
  foreach(string state in new[]{"normal","hover","pressed","focus"})AddThemeStyleboxOverride(state,new StyleBoxEmpty());
  MouseEntered+=QueueRedraw;MouseExited+=QueueRedraw;ButtonDown+=QueueRedraw;ButtonUp+=QueueRedraw;Resized+=QueueRedraw;
 }
 public void SetHeld(bool value){if(held==value)return;held=value;QueueRedraw();}
 void Line(Vector2 a,Vector2 b,Color color,float width=2)=>DrawLine(a,b,color,width,true);
 public override void _Draw(){
  bool down=held||IsPressed();bool pedal=Kind=="GAS"||Kind=="BRAKE",steer=Kind=="LEFT"||Kind=="RIGHT";
  Color accent=Kind=="BRAKE"?Amber:Teal;float radius=steer?Size.X*.5f:pedal?18:12;
  var panel=new StyleBoxFlat {BgColor=down?new Color(accent.R,accent.G,accent.B,.23f):new Color(.025f,.045f,.065f,pedal?.46f:.32f),BorderColor=down?accent:new Color(.75f,.86f,.91f,IsHovered()?.5f:.22f),BorderWidthLeft=1,BorderWidthRight=1,BorderWidthTop=1,BorderWidthBottom=1,CornerRadiusTopLeft=(int)radius,CornerRadiusTopRight=(int)radius,CornerRadiusBottomLeft=(int)radius,CornerRadiusBottomRight=(int)radius};
  DrawStyleBox(panel,new Rect2(Vector2.One,Size-Vector2.One*2));Color ink=down?accent:White;var c=Size*.5f;
  if(steer){float sign=Kind=="LEFT"?-1:1;var tip=c+new Vector2(sign*11,0);Line(c+new Vector2(-sign*4,-13),tip,ink,3);Line(tip,c+new Vector2(-sign*4,13),ink,3);DrawArc(c,Size.X*.39f,Mathf.Pi*.2f,Mathf.Pi*.8f,24,new Color(ink.R,ink.G,ink.B,.2f),1,true);return;}
  if(pedal){c.Y-=10;float width=Kind=="GAS"?30:51,height=Kind=="GAS"?72:36;
   var points=new[]{c+new Vector2(-width*.36f,-height*.5f),c+new Vector2(width*.36f,-height*.5f),c+new Vector2(width*.5f,height*.5f),c+new Vector2(-width*.5f,height*.5f)};
   DrawColoredPolygon(points,new Color(ink.R,ink.G,ink.B,down?.24f:.08f));DrawPolyline(new[]{points[0],points[1],points[2],points[3],points[0]},ink,1.5f,true);
   if(Kind=="GAS")for(int i=0;i<5;i++){float y=-height*.3f+i*height*.15f;Line(c+new Vector2(-width*.25f,y),c+new Vector2(width*.25f,y),ink,2);}
   else for(int i=0;i<5;i++){float x=-width*.3f+i*width*.15f;Line(c+new Vector2(x,-height*.28f),c+new Vector2(x,height*.28f),ink,2);}
   Caption(Kind=="GAS"?"THROTTLE":"BRAKE",new Vector2(0,Size.Y-14),11,down?accent:White);return;
  }
  c.X=19;
  if(Kind=="CAMERA"){DrawRect(new Rect2(c-new Vector2(8,6),new Vector2(16,12)),ink,false,1.5f);DrawArc(c,3,0,Mathf.Tau,20,ink,1.5f,true);}
  else if(Kind=="PAUSE"){Line(c+new Vector2(-3,-6),c+new Vector2(-3,6),ink,2.5f);Line(c+new Vector2(3,-6),c+new Vector2(3,6),ink,2.5f);}
  else if(Kind=="RESUME")DrawColoredPolygon(new[]{c+new Vector2(-4,-7),c+new Vector2(6,0),c+new Vector2(-4,7)},ink);
  else {Line(c+new Vector2(-7,6),c+new Vector2(7,6),ink);Line(c+new Vector2(-5,6),c+new Vector2(-5,-6),ink);Line(c+new Vector2(-5,-6),c+new Vector2(5,-6),ink);Line(c+new Vector2(5,-6),c+new Vector2(5,6),ink);}
  DrawString(ThemeDB.FallbackFont,new Vector2(33,c.Y+4),Kind=="CAMERA"?"VIEW":Kind,HorizontalAlignment.Left,-1,11,ink);
 }
 void Caption(string text,Vector2 at,int size,Color color){DrawString(ThemeDB.FallbackFont,at,text,HorizontalAlignment.Center,Size.X,size,color);}
}
}
