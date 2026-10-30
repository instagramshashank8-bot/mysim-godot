using Godot;
using System;
namespace MySim {
// Runs against real UI signals; restores persisted choices even when an assertion fails.
public partial class SettingsScenario : Node {
 void Check(bool value,string reason){if(!value)throw new Exception(reason);}
 T Find<T>(Node panel,string name) where T:Node {foreach(var n in panel.FindChildren("*","",true,false))if(n.Name.ToString()==name)return (T)n;throw new Exception("Missing settings control "+name);}
 public override async void _Ready(){
  string[] keys={"tod","wet","rivals","starting_compound"};int[] previous=new int[keys.Length];for(int i=0;i<keys.Length;i++)previous[i]=Settings.Get(keys[i],keys[i]=="starting_compound"?1:0);bool mute=Settings.Get("mute",true);
  try {
   var panel=new RaceSettingsPanel();AddChild(panel);for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   foreach(var pair in new[]{("tod",Conditions.TodNames.Length),("wet",Conditions.WetNames.Length),("rivals",6),("starting_compound",4)}){
    var control=Find<OptionButton>(panel,pair.Item1);Check(control.ItemCount==pair.Item2,"Missing choices: "+pair.Item1);
    for(int choice=0;choice<pair.Item2;choice++){control.Selected=choice;control.EmitSignal(OptionButton.SignalName.ItemSelected,(long)choice);Check(Settings.Get(pair.Item1,-1)==choice,"Selection not persisted: "+pair.Item1);}
   }
   var sound=Find<CheckBox>(panel,"mute");sound.ButtonPressed=false;sound.EmitSignal(BaseButton.SignalName.Toggled,false);Check(Settings.Get("mute",false),"Sound toggle failed to mute");
   for(int frame=0;frame<3;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   if(DisplayServer.GetName()!="headless")GetViewport().GetTexture().GetImage().SavePng(System.Environment.GetEnvironmentVariable("MYSIM_SHOT")??"outputs/cloud-game/settings.png");
   var scroll=Find<ScrollContainer>(panel,"SettingsScroll");scroll.EnsureControlVisible(Find<CheckBox>(panel,"tilt"));for(int frame=0;frame<3;frame++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);
   var tilt=Find<CheckBox>(panel,"tilt");var viewport=GetViewport().GetVisibleRect();Check(viewport.Encloses(tilt.GetGlobalRect()),"Bottom settings cannot be scrolled into view");
   if(DisplayServer.GetName()!="headless"){string path=System.Environment.GetEnvironmentVariable("MYSIM_SHOT")??"outputs/cloud-game/settings.png";GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(path,null)+"-scrolled.png");}
   Find<Button>(panel,"SaveBack").EmitSignal(BaseButton.SignalName.Pressed);await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);Check(!IsInstanceValid(panel),"Settings back button did not close modal");
   GD.Print("PASS race settings real controls persistence and close");GetTree().Quit();
  }catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}finally{for(int i=0;i<keys.Length;i++)Settings.Set(keys[i],previous[i]);Settings.Set("mute",mute);Settings.ApplyAudio();}
 }
}
}
