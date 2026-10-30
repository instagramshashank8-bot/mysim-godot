using Godot;
using MySimulator.Rebuild;
namespace MySim {
public partial class PitStrategyPanel : PanelContainer {
 public Vehicle Player;public Race Race;public bool Open;OptionButton compound;Button request;
 public void Toggle(){Open=!Open;}
 public override void _Ready(){Theme=MySimTheme.Build();AnchorLeft=AnchorRight=1;OffsetLeft=-294;OffsetRight=-24;OffsetTop=235;Visible=false;
  var stack=new VBoxContainer();stack.AddThemeConstantOverride("separation",12);AddChild(stack);
  var heading=new HBoxContainer();stack.AddChild(heading);heading.AddChild(new Label {Text="PIT STRATEGY",SizeFlagsHorizontal=SizeFlags.ExpandFill});
  var close=new Button {Name="CLOSE",Text="?",TooltipText="Close pit strategy",CustomMinimumSize=new Vector2(40,40)};close.Pressed+=()=>Open=false;heading.AddChild(close);
  stack.AddChild(new Label {Text="NEXT TYRES"});compound=new OptionButton {Name="COMPOUND",CustomMinimumSize=new Vector2(230,44)};
  foreach(string name in new[]{"SOFT / RED","MEDIUM / YELLOW","HARD / WHITE","WET / BLUE"})compound.AddItem(name);
  compound.Selected=1;compound.ItemSelected+=i=>{if(Player!=null&&!Player.servicing)Player.nextCompound=(Compound)(int)i;};stack.AddChild(compound);
  request=new Button {Name="REQUEST",Text="REQUEST PIT STOP",CustomMinimumSize=new Vector2(230,48)};
  request.Pressed+=()=>{if(Player!=null&&!Player.pitActive&&!Player.servicing&&Race!=null&&Race.track.PitValid){Player.pitRequested=!Player.pitRequested;Open=false;}};stack.AddChild(request);
 }
 public override void _Process(double delta){Visible=Open&&Player!=null&&Race!=null&&!Player.finished;if(!Visible)return;compound.Disabled=Player.servicing;if(!compound.HasFocus())compound.Selected=(int)Player.nextCompound;request.Disabled=!Race.track.PitValid||Player.pitActive||Player.servicing;request.Text=!Race.track.PitValid?"PIT LANE UNAVAILABLE":Player.servicing?"SERVICING":Player.pitActive?"IN PIT LANE":Player.pitRequested?"CANCEL PIT REQUEST":"REQUEST PIT STOP";}
}
}
