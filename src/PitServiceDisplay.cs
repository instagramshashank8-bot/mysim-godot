using Godot;
using MySimulator.Rebuild;
namespace MySim {
public partial class PitServiceDisplay : PanelContainer {
 public Vehicle Player;Label title,clock;Label[] wheels=new Label[4];ProgressBar progress;
 public override void _Ready(){
  MouseFilter=MouseFilterEnum.Ignore;AnchorLeft=AnchorRight=.5f;OffsetLeft=-185;OffsetRight=185;OffsetTop=110;
  var style=new StyleBoxFlat {BgColor=new Color(.025f,.035f,.05f,.94f),BorderColor=new Color(.25f,.65f,.7f),BorderWidthLeft=2,BorderWidthRight=2,BorderWidthTop=2,BorderWidthBottom=2,ContentMarginLeft=16,ContentMarginRight=16,ContentMarginTop=12,ContentMarginBottom=12,CornerRadiusTopLeft=8,CornerRadiusTopRight=8,CornerRadiusBottomLeft=8,CornerRadiusBottomRight=8};AddThemeStyleboxOverride("panel",style);
  var stack=new VBoxContainer();AddChild(stack);title=new Label();title.AddThemeFontSizeOverride("font_size",22);stack.AddChild(title);clock=new Label();stack.AddChild(clock);
  var grid=new GridContainer {Columns=2};stack.AddChild(grid);for(int i=0;i<4;i++){wheels[i]=new Label {CustomMinimumSize=new Vector2(155,26)};grid.AddChild(wheels[i]);}
  progress=new ProgressBar {ShowPercentage=false,MaxValue=PitServiceTimeline.Duration,CustomMinimumSize=new Vector2(330,8)};stack.AddChild(progress);Visible=false;
 }
 public override void _Process(double delta){
  Visible=Player!=null&&Player.servicing;if(!Visible)return;float elapsed=PitServiceTimeline.Duration-Player.serviceRemaining;
  title.Text=PitServiceTimeline.Stage(elapsed);clock.Text=$"{elapsed:0.0}s  /  {PitServiceTimeline.Duration:0.0}s    {Player.nextCompound.ToString().ToUpperInvariant()} TYRES";progress.Value=elapsed;
  string[] names={"FRONT LEFT","FRONT RIGHT","REAR LEFT","REAR RIGHT"};for(int i=0;i<4;i++){
   string state=elapsed<PitServiceTimeline.Loosen(i)?"WAIT":elapsed<PitServiceTimeline.Remove(i)?"LOOSEN":elapsed<PitServiceTimeline.Fit(i)?"CHANGE":elapsed<PitServiceTimeline.Secure(i)?"SECURE":"READY";
   wheels[i].Text=names[i]+"  "+state;wheels[i].Modulate=state=="READY"?new Color(.3f,1,.6f):Colors.White;
  }
 }
}
}
