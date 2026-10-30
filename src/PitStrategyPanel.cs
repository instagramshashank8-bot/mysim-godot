using Godot;
using MySimulator.Rebuild;
namespace MySim {
public partial class PitStrategyPanel : VBoxContainer {
 public Vehicle Player;public Race Race;OptionButton compound;Button request;
 public override void _Ready(){AnchorLeft=AnchorRight=1;OffsetLeft=-250;OffsetRight=-20;OffsetTop=305;AddThemeConstantOverride("separation",6);AddChild(new Label {Text="PIT STRATEGY"});compound=new OptionButton {CustomMinimumSize=new Vector2(230,40)};foreach(string name in new[]{"SOFT / RED","MEDIUM / YELLOW","HARD / WHITE","WET / BLUE"})compound.AddItem(name);compound.Selected=1;compound.ItemSelected+=i=>{if(Player!=null&&!Player.servicing)Player.nextCompound=(Compound)(int)i;};AddChild(compound);request=new Button {Text="REQUEST PIT STOP",CustomMinimumSize=new Vector2(230,40)};request.Pressed+=()=>{if(Player!=null&&!Player.pitActive&&!Player.servicing)Player.pitRequested=!Player.pitRequested;};AddChild(request);}
 public override void _Process(double delta){Visible=Player!=null&&Race!=null&&!Player.finished;if(!Visible)return;compound.Disabled=Player.servicing;if(!compound.HasFocus())compound.Selected=(int)Player.nextCompound;request.Disabled=!Race.track.PitValid||Player.pitActive||Player.servicing;request.Text=!Race.track.PitValid?"PIT LANE UNAVAILABLE":Player.servicing?"SERVICING":Player.pitActive?"IN PIT LANE":Player.pitRequested?"CANCEL PIT REQUEST":"REQUEST PIT STOP";}
}
}
