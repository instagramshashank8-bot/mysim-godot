using Godot;
using System;
using MySimulator.Rebuild;
namespace MySim {
// Real LAN handshake/lobby; native3D multiplayer race integration is a separate gate.
public partial class LanRoomPanel : CanvasLayer {
 public bool Hosting;public int HostPort=LanChannel.Port;GameSession session;Label status;OptionButton track;SpinBox laps;LineEdit address;Button connect;OptionButton carPicker;Button startButton;
 // The host app wires this: the panel transfers the LIVE session (never disposed) to the
 // race view when the client's start gate opens. The host keeps the session in the panel
 // until the in-race integration lands (its lobby keeps ticking and serving STATE).
 public Action<GameSession> LaunchLan;
 // Two-instance test automation: MYSIM_LAN_JOIN pre-fills+starts the join; MYSIM_LAN_AUTOSTART
 // fires START RACE once a client is connected (2 s settle). Default-off; the menu path is
 // unchanged for humans.
 public string AutoJoinAddress; bool autoStart; double autoStartTimer;
 public override void _Ready(){Layer=60;var shade=new ColorRect {Theme=MySimTheme.Build(),Color=MySimTheme.Ink,AnchorRight=1,AnchorBottom=1};AddChild(shade);var panel=new PanelContainer {AnchorLeft=.12f,AnchorRight=.88f,AnchorTop=.08f,AnchorBottom=.92f};shade.AddChild(panel);var box=new VBoxContainer();box.AddThemeConstantOverride("separation",14);panel.AddChild(box);box.AddChild(new Label {Text=Hosting?"LAN / CREATE ROOM":"LAN / JOIN ROOM"});status=new Label {AutowrapMode=TextServer.AutowrapMode.WordSmart};box.AddChild(status);
  if(Hosting){track=new OptionButton();foreach(string name in SourceRoutes.Names)track.AddItem(name);track.Selected=Mathf.Clamp(Settings.Get("track",0),0,SourceRoutes.Names.Length-1);box.AddChild(track);box.AddChild(new Label {Text="LAPS"});laps=new SpinBox {MinValue=1,MaxValue=20,Value=Settings.Get("laps",3)};box.AddChild(laps);AddButton(box,"SESSION OPTIONS",()=>AddChild(new RaceSettingsPanel {SessionLabel="LAN HOST"}));AddButton(box,"CREATE ROOM",BeginHost);}
  else{box.AddChild(new Label {Text="YOUR CAR"});carPicker=new OptionButton();foreach(string carName in CarCatalog.Names)carPicker.AddItem(carName);carPicker.Selected=Mathf.Clamp(Settings.Get("car",0),0,CarCatalog.Count-1);box.AddChild(carPicker);
  box.AddChild(new Label {Text="HOST ADDRESS / PORT"});address=new LineEdit {PlaceholderText="192.168.1.20:44880",CustomMinimumSize=new Vector2(0,48)};box.AddChild(address);connect=AddButton(box,"JOIN ROOM",BeginJoin);}
  box.AddChild(new Label {Text="LAN race-scene launch is still pending in this build (handshake and sync bridge are in).",AutowrapMode=TextServer.AutowrapMode.WordSmart});
  if(Hosting){startButton=AddButton(box,"START RACE",()=>{try{session.StartRace();status.Text="Race starting...";var launch=LaunchLan;var handedOver=session;launch?.Invoke(handedOver);}catch(Exception e){status.Text=e.Message;}});startButton.Disabled=true;
   if(System.Environment.GetEnvironmentVariable("MYSIM_LAN_AUTOSTART")=="1"){autoStart=true;BeginHost();}}
  else if(AutoJoinAddress!=null){address.Text=AutoJoinAddress;BeginJoin();}AddButton(box,"LEAVE ROOM",QueueFree);status.Text=Hosting?"Choose room conditions, then create the room.":"Enter the host address on the same network.";
 }
 Button AddButton(Node parent,string text,Action action){var button=new Button {Text=text,CustomMinimumSize=new Vector2(0,48)};button.Pressed+=()=>action();parent.AddChild(button);return button;}
 void BeginHost(){try{session?.Dispose();var race=new Race(track.Selected,(int)laps.Value,Conditions.WetnessValue);race.Add("HOST",true,Compound.Medium);session=new GameSession(race);session.Host(new LanChannel(true,HostPort),"MySim room");status.Text="ROOM OPEN / "+LanChannel.Addresses()+":"+HostPort+" / waiting for another player";}catch(Exception e){status.Text="Could not open room: "+e.Message;}}
 void BeginJoin(){try{session?.Dispose();string target=address.Text.Trim();int split=target.LastIndexOf(':');if(split<0)target+=":"+LanChannel.Port;var race=new Race(0,3,0);session=new GameSession(race);session.localModel=carPicker.Selected;session.Join(new LanChannel(false),target,"PLAYER");connect.Disabled=true;}catch(Exception e){status.Text="Could not join: "+e.Message;}}
 public override void _Process(double delta){if(session==null)return;if(autoStart){autoStartTimer+=delta;if(startButton!=null&&session.connected&&!session.startRequested&&autoStartTimer>2)startButton.EmitSignal(BaseButton.SignalName.Pressed);}try{session.Tick((float)delta);if(startButton!=null)startButton.Disabled=!(session.connected&&!session.startRequested);if(session.RaceAdopted){status.Text="Race running";session=null;QueueFree();return;}if(session.startReceived){status.Text="Race starting";var launch=LaunchLan;var handedOver=session;session=null;QueueFree();launch?.Invoke(handedOver);return;}if(session.connected)status.Text="CONNECTED / "+session.race.cars.Count+" players / "+session.race.track.name;else if(session.role==SessionRole.Client)status.Text=session.status;if(connect!=null&&session.silence>5&&!session.connected){connect.Disabled=false;status.Text="No host response. Check the address and network, then retry.";}}catch(Exception e){status.Text="Room connection error: "+e.Message;session.Dispose();session=null;if(connect!=null)connect.Disabled=false;if(startButton!=null)startButton.Disabled=true;}}
 public override void _ExitTree(){session?.Dispose();}
 public override void _UnhandledInput(InputEvent input){if(input is InputEventKey key&&key.Pressed&&key.Keycode==Key.Escape){GetViewport().SetInputAsHandled();QueueFree();}}

}
}
