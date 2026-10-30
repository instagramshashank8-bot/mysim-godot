using Godot;
using System;
namespace MySim {
public partial class NavigationScenario : Node {
 void Check(bool yes,string reason){if(!yes)throw new Exception(reason);}
 Button Find(Node root,string name){foreach(var node in root.FindChildren("*","Button",true,false))if(node.Name.ToString()==name)return (Button)node;throw new Exception("Missing mode action "+name);}
 async System.Threading.Tasks.Task Capture(string suffix){for(int i=0;i<3;i++)await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);string path=System.Environment.GetEnvironmentVariable("MYSIM_SHOT");if(path!=null&&DisplayServer.GetName()!="headless")GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.ChangeExtension(path,null)+suffix+".png");}
 public override async void _Ready(){try{int quick=0,series=0,host=0,join=0;var menu=new ModeMenu {QuickRace=()=>quick++,Series=()=>series++,CreateRoom=()=>host++,JoinRoom=()=>join++};AddChild(menu);await Capture("-home");Check(Find(menu,"SINGLEPLAYER")!=null&&Find(menu,"LAN")!=null,"Home modes absent");Find(menu,"SINGLEPLAYER").EmitSignal(BaseButton.SignalName.Pressed);await Capture("-single");Check(quick==0&&series==0,"Choosing mode prematurely starts race");Find(menu,"QUICKRACE").EmitSignal(BaseButton.SignalName.Pressed);Find(menu,"RACESERIES").EmitSignal(BaseButton.SignalName.Pressed);Check(quick==1&&series==1,"Race type actions not routed");menu.Home();Find(menu,"LAN").EmitSignal(BaseButton.SignalName.Pressed);await Capture("-lan");Check(host==0&&join==0,"LAN selection prematurely opens room");Find(menu,"CREATEROOM").EmitSignal(BaseButton.SignalName.Pressed);Find(menu,"JOINROOM").EmitSignal(BaseButton.SignalName.Pressed);Check(host==1&&join==1,"LAN actions not routed");GD.Print("PASS mode-first navigation routing");GetTree().Quit();}catch(Exception e){GD.PushError(e.ToString());GetTree().Quit(1);}}
}
}
