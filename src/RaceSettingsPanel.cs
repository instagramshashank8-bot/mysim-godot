using Godot;
namespace MySim {
public partial class RaceSettingsPanel : CanvasLayer {
 public override void _Ready(){Layer=80;var shade=new ColorRect {Color=new Color(0,0,0,.8f),AnchorRight=1,AnchorBottom=1};AddChild(shade);var panel=new PanelContainer {AnchorLeft=.12f,AnchorTop=.08f,AnchorRight=.88f,AnchorBottom=.92f};shade.AddChild(panel);var margin=new MarginContainer();foreach(string side in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+side,24);panel.AddChild(margin);var stack=new VBoxContainer();stack.AddThemeConstantOverride("separation",12);margin.AddChild(stack);stack.AddChild(new Label {Text="RACE SETTINGS"});var scroll=new ScrollContainer {Name="SettingsScroll",SizeFlagsVertical=Control.SizeFlags.ExpandFill};stack.AddChild(scroll);var list=new VBoxContainer {SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};list.AddThemeConstantOverride("separation",12);scroll.AddChild(list);
  void Select(string label,string key,string[] names,int fallback=0){list.AddChild(new Label {Text=label});var choice=new OptionButton {Name=key,CustomMinimumSize=new Vector2(0,42)};foreach(string name in names)choice.AddItem(name);choice.Selected=Mathf.Clamp(Settings.Get(key,fallback),0,names.Length-1);choice.ItemSelected+=i=>Settings.Set(key,(int)i);list.AddChild(choice);}
  void Toggle(string label,string key,bool fallback,bool inverse=false){var toggle=new CheckBox {Name=key,Text=label,ButtonPressed=inverse?!Settings.Get(key,fallback):Settings.Get(key,fallback)};toggle.Toggled+=on=>{Settings.Set(key,inverse?!on:on);if(key=="mute")Settings.ApplyAudio();};list.AddChild(toggle);}
  Select("TIME OF DAY","tod",Conditions.TodNames);Select("WEATHER","wet",Conditions.WetNames);Select("RIVALS","rivals",new[]{"Solo","1 rival","2 rivals","3 rivals","4 rivals","5 rivals"});Select("STARTING TYRES","starting_compound",new[]{"Soft / red","Medium / yellow","Hard / white","Wet / blue"},1);
  Toggle("Sound","mute",true,true);Toggle("Screen controls","screen_controls",true);Toggle("ABS","abs",true);Toggle("Traction control","tc",true);Toggle("Tilt steering","tilt",false);
  var done=new Button {Name="SaveBack",Text="SAVE & BACK",CustomMinimumSize=new Vector2(0,44)};done.Pressed+=QueueFree;stack.AddChild(done);done.GrabFocus();
 }
 public override void _UnhandledInput(InputEvent input){if(input is InputEventKey key&&key.Pressed&&(key.Keycode==Key.Escape||key.Keycode==Key.Tab)){GetViewport().SetInputAsHandled();QueueFree();}}
}
}
