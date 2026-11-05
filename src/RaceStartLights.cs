using Godot;
namespace MySim {
// Shared race countdown is the sole authority: red lights disappear when racing starts.
public partial class RaceStartLights : Control {
 public Hud Hud;readonly StyleBoxFlat panel=new(){BgColor=new Color(.025f,.03f,.04f,.94f),CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8};
 public override void _Ready(){MouseFilter=MouseFilterEnum.Ignore;AnchorLeft=AnchorRight=.5f;OffsetLeft=-130;OffsetRight=130;OffsetTop=11;OffsetBottom=69;}
 public override void _Process(double delta){Visible=Hud?.Race!=null&&!Hud.Race.started;QueueRedraw();}
 public int LitCount=>Hud?.Race==null||Hud.Race.started?0:Mathf.Clamp(6-Mathf.CeilToInt(Hud.Race.countdown),1,5);
 public override void _Draw(){
  if(Hud?.Race==null||Hud.Race.started)return;
  DrawStyleBox(panel,new Rect2(Vector2.Zero,Size));
  int lit=LitCount;
  for(int i=0;i<5;i++){var centre=new Vector2(34+i*48,29);DrawCircle(centre,18,new Color(.12f,.13f,.15f));DrawCircle(centre,14,i<lit?new Color(.95f,.035f,.025f):new Color(.19f,.035f,.035f));}
 }
}
}
