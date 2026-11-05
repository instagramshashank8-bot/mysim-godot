using Godot;
using MySimulator.Rebuild;
namespace MySim {
// Classification comes from the race model; no invented time gaps or scripted positions.
public partial class RaceStandingsPanel : PanelContainer {
 public Race Race;public Vehicle Player;VBoxContainer rows;float elapsed;
 public override void _Ready(){Name="RaceStandings";Position=new Vector2(12,80);CustomMinimumSize=new Vector2(255,0);MouseFilter=MouseFilterEnum.Ignore;Theme=MySimTheme.Build();AddThemeStyleboxOverride("panel",Hud.Chip());rows=new VBoxContainer {Name="StandingsRows"};rows.AddThemeConstantOverride("separation",3);AddChild(rows);Refresh();}
 public override void _Process(double delta){elapsed+=(float)delta;if(elapsed<.2f)return;elapsed=0;Refresh();}
 public void Refresh(){if(rows==null)return;foreach(var n in rows.GetChildren()){rows.RemoveChild(n);n.QueueFree();}if(Race==null)return;
  var title=new Label {Text="CLASSIFICATION"};title.AddThemeFontSizeOverride("font_size",13);title.AddThemeColorOverride("font_color",new Color(.7f,.78f,.84f));rows.AddChild(title);var order=Race.Standings();
  float leaderScore=order.Count>0?Race.DistanceScore(order[0]):0f;
  for(int i=0;i<order.Count;i++){var car=order[i];if(i>=5&&car!=Player)continue;string state=car.retired?"OUT":car.finished?"FIN":car.servicing||car.pitActive?"PIT":"L"+System.Math.Min(car.completedLaps+1,Race.targetLaps);string name=CarCatalog.Names[System.Math.Clamp(car.carModel,0,CarCatalog.Count-1)];if(name.Length>18)name=name.Substring(0,18);
   // Gap column = each car's live distance deficit to the leader in metres (the results board's measure, leader-referenced).
   string gap="";if(i>0&&!car.finished&&!car.retired){float metres=(leaderScore-Race.DistanceScore(car))*Race.track.length;if(metres>=1f)gap="  +"+(int)metres+"m";}
   var row=new Label {Name="Car"+car.id,Text=(i+1).ToString("00")+"  "+name+"  "+state+gap};row.AddThemeFontSizeOverride("font_size",14);if(car==Player)row.AddThemeColorOverride("font_color",MySimTheme.Accent);rows.AddChild(row);}
 }
}
}
