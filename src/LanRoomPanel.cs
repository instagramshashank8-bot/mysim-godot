using Godot;
using System;
using MySimulator.Rebuild;
namespace MySim {
// Real LAN handshake/lobby; native3D multiplayer race integration is a separate gate.
public partial class LanRoomPanel : CanvasLayer {
 public bool Hosting;public int HostPort=LanChannel.Port;GameSession session;Label status;OptionButton track;SpinBox laps;LineEdit address;Button connect;
 public override void _Ready(){Layer=60;var shade=new ColorRect {Theme=MySimTheme.Build(),Color=MySimTheme.Ink,AnchorRight=1,AnchorBottom=1};AddChild(shade);var panel=new PanelContainer {AnchorLeft=.12f,AnchorRight=.88f,AnchorTop=.08f,AnchorBottom=.92f};shade.AddChild(panel);var box=new VBoxContainer();box.AddThemeConstantOverride("separation",14);panel.AddChild(box);box.AddChild(new Label {Text=Hosting?"LAN / CREATE ROOM":"LAN / JOIN ROOM"});status=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(status);
  if(Hosting){track=new OptionButton();foreach(string name in SourceRoutes.Names)track.AddItem(name);track.Selected=Mathf.Clamp(Settings.Get("track",0),0,SourceRoutes.Names.Length-1);box.AddChild(track);box.AddChild(new Label {Text="LAPS"});laps=new SpinBox {MinValue=1,MaxValue=20,Value=Settings.Get("laps",3)};box.AddChild(laps);AddButton(box,"SESSION OPTIONS",()=>AddChild(new RaceSettingsPanel {SessionLabel="LAN HOST"}));AddButton(box,"CREATE ROOM",BeginHost);}
  else{box.AddChild(new Label {Text="HOST ADDRESS / PORT"});address=new LineEdit {PlaceholderText="192.168.1.20:44880",CustomMinimumSize=new Vector2(0,48)};box.AddChild(address);connect=AddButton(box,"JOIN ROOM",BeginJoin);}
  box.AddChild(new Label {Text="LAN race launch is unavailable in this build.",AutowrapMode=TextServer.AutowrapMode.WordSmart});AddButton(box,"LEAVE ROOM",QueueFree);status.Text=Hosting?"Choose room conditions, then create the room.":"Enter the host address on the same network.";
 }
 Button AddButton(Node parent,string text,Action action){var button=new Button {Text=text,CustomMinimumSize=new Vector2(0,48)};button.Pressed+=()=>action();parent.AddChild(button);return button;}
 void BeginHost(){try{session?.Dispose();var race=new Race(track.Selected,(int)laps.Value,Conditions.WetnessValue);race.Add("HOST",true,Compound.Medium);session=new GameSession(race);session.Host(new LanChannel(true,HostPort),"MySim room");status.Text="ROOM OPEN / "+LanChannel.Addresses()+":"+HostPort+" / waiting for another player";}catch(Exception e){status.Text="Could not open room: "+e.Message;}}
 void BeginJoin(){try{session?.Dispose();string target=address.Text.Trim();int split=target.LastIndexOf(':');if(split<0)target+=":"+LanChannel.Port;var race=new Race(0,3,0);session=new GameSession(race);session.Join(new LanChannel(false),target,"PLAYER");connect.Disabled=true;}catch(Exception e){status.Text="Could not join: "+e.Message;}}
 public override void _Process(double delta){if(session==null)return;try{session.Tick((float)delta);if(session.connected)status.Text="CONNECTED / "+session.race.cars.Count+" players / "+session.race.track.name;else if(session.role==SessionRole.Client)status.Text=session.status;if(connect!=null&&session.silence>5&&!session.connected){connect.Disabled=false;status.Text="No host response. Check the address and network, then retry.";}}catch(Exception e){status.Text="Room connection error: "+e.Message;session.Dispose();session=null;if(connect!=null)connect.Disabled=false;}}
 public override void _ExitTree(){session?.Dispose();}
}
}
