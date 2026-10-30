using Godot;
using System;
namespace MySim {
public partial class ModeMenu : Control {
 public Action QuickRace,Series,CreateRoom,JoinRoom;
 VBoxContainer content;
 public override void _Ready(){AnchorRight=1;AnchorBottom=1;Theme=MySimTheme.Build();var background=new ColorRect {Color=MySimTheme.Ink,AnchorRight=1,AnchorBottom=1};AddChild(background);var panel=new PanelContainer {AnchorLeft=.16f,AnchorRight=.84f,AnchorTop=.14f,AnchorBottom=.86f};background.AddChild(panel);var margin=new MarginContainer();foreach(string side in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+side,28);panel.AddChild(margin);content=new VBoxContainer();content.AddThemeConstantOverride("separation",16);margin.AddChild(content);Home();}
 void Page(string title,string subtitle){foreach(var child in content.GetChildren()){content.RemoveChild(child);child.QueueFree();}content.AddChild(new Label {Text="MYSIM  /  RACE STUDIO"});var heading=new Label {Text=title};heading.AddThemeFontSizeOverride("font_size",32);content.AddChild(heading);content.AddChild(new Label {Text=subtitle,AutowrapMode=TextServer.AutowrapMode.WordSmart});}
 void Button(string text,Action action){var button=new Godot.Button {Name=text.Replace(" ",""),Text=text,CustomMinimumSize=new Vector2(0,54)};button.Pressed+=()=>action?.Invoke();content.AddChild(button);if(content.GetChildCount()==4)button.GrabFocus();}
 public void Home(){Page("CHOOSE YOUR RACE","Select a mode. Build your session next.");Button("SINGLE PLAYER",Solo);Button("LAN",Lan);}
 public void Solo(){Page("SINGLE PLAYER","Choose a single race or build a series of tracks.");Button("QUICK RACE",()=>QuickRace?.Invoke());Button("RACE SERIES",()=>Series?.Invoke());Button("BACK",Home);}
 public void Lan(){Page("LAN","Create a room on your network, or join an existing host.");Button("CREATE ROOM",()=>CreateRoom?.Invoke());Button("JOIN ROOM",()=>JoinRoom?.Invoke());Button("BACK",Home);}
}
}
