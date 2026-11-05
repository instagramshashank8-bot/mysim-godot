using Godot;
using MySimulator.Rebuild;
using System;
namespace MySim {
// Product layer: the hud and product panels display live race state and keep keyboard focus usable.
// Pure checks - no synthetic display values: every expectation is an independent literal or re-derived from the model.
// Wiring hook (coordinator-owned Main.cs one-liner) and the workflow depth mapping are proposals in the turn-3 report.
public partial class ProductControlsScenario : Node {
 void Check(bool value,string why){if(!value)throw new Exception(why);}
 async System.Threading.Tasks.Task Frame(){await ToSignal(GetTree(),SceneTree.SignalName.ProcessFrame);}
 public override async void _Ready(){
  try{
   var race=new Race(0,3,0);var player=race.Add("YOU",true,Compound.Medium);var opp=race.Add("RIVAL",false,Compound.Medium);
   var hud=new Hud{Race=race,Player=player};AddChild(hud);
   await Frame();await Frame();
   // Speed/gear binding: two independent literal states prove the pipe is live, not a stored string (speed is m/s; the label is km/h).
   player.speed=11.6f;player.gear=3;await Frame();await Frame();
   Check(hud.SpeedLabel.Text=="41 km/h"&&hud.GearLabel.Text=="GEAR 3","hud speed/gear did not follow the race state: '"+hud.SpeedLabel.Text+"' '"+hud.GearLabel.Text+"'");
   player.speed=0f;player.gear=-1;await Frame();await Frame();
   Check(hud.SpeedLabel.Text=="0 km/h"&&hud.GearLabel.Text=="GEAR R","hud zero speed or reverse gear wrong: '"+hud.SpeedLabel.Text+"' '"+hud.GearLabel.Text+"'");
   // Compound/wear/fuel/damage: independent literals for the values, model term for the compound letter.
   player.fuel=33.7f;player.damage=0.25f;foreach(var t in player.tyres)t.wear=0.5f;await Frame();await Frame();
   Check(hud.CarStateLabel.Text.StartsWith("TYRES M 50%"),"hud tyre compound/wear not bound to live tyres: '"+hud.CarStateLabel.Text+"'");
   Check(hud.CarStateLabel.Text.Contains("FUEL 33 L")&&hud.CarStateLabel.Text.Contains("DAMAGE 25%"),"hud fuel/damage not bound to live state: '"+hud.CarStateLabel.Text+"'");
   // Pit plan: requesting a stop with a different compound announces the plan from the model.
   player.nextCompound=Compound.Wet;player.pitRequested=true;await Frame();await Frame();
   Check(hud.CarStateLabel.Text.Contains("TYRES M 50% NEXT W"),"hud missed the live pit plan: '"+hud.CarStateLabel.Text+"'");
   Check(hud.CarStateLabel.Text.Contains("REQUESTED ("+OS.GetKeycodeString(ControlBindings.Get(5))+")"),"pit key hint not read from the bindings: '"+hud.CarStateLabel.Text+"'");
   // Settings -> hud chain: remap the pit key and the hint must follow on the next frame (long-typed Get path included).
   Key pitBefore=ControlBindings.Get(5);player.pitRequested=false;player.nextCompound=player.tyres[0].compound;
   Settings.Set("key_5",(long)Key.O);player.pitRequested=true;await Frame();await Frame();
   Check(hud.CarStateLabel.Text.Contains("REQUESTED (O)"),"pit key hint did not follow the remapped binding: '"+hud.CarStateLabel.Text+"'");
   player.pitRequested=false;Settings.Set("key_5",(long)pitBefore);
   // Weather bound to the active profile plus LIVE surface water; night bound to the lighting profile.
   Settings.Set("wet",2);await Frame();await Frame();
   Check(hud.WeatherLabel.Text=="RAIN","weather tag not bound to the active profile: '"+hud.WeatherLabel.Text+"'");
   race.wetness=0.42f;await Frame();await Frame();
   Check(hud.WeatherLabel.Text=="RAIN 42%","weather tag not bound to live surface water: '"+hud.WeatherLabel.Text+"'");
   Settings.Set("tod",3);await Frame();await Frame();
   Check(hud.WeatherLabel.Text=="NIGHT RAIN 42%","night tag not bound to the lighting profile: '"+hud.WeatherLabel.Text+"'");
   Settings.Set("tod",0);Settings.Set("wet",0);race.wetness=0f;await Frame();await Frame();
   Check(hud.WeatherLabel.Text=="CLEAR","weather tag did not return to the active profile: '"+hud.WeatherLabel.Text+"'");
   // Input modality reflects the actual configuration (headless: no pads).
   Check(hud.DeviceLabel.Text=="KEYS"||hud.DeviceLabel.Text=="PAD","device modality unknown: '"+hud.DeviceLabel.Text+"'");
   bool touchBefore=Settings.Get("screen_controls",false);Settings.Set("screen_controls",true);await Frame();await Frame();
   Check(hud.DeviceLabel.Text=="TOUCH","touch modality not shown when screen controls are enabled: '"+hud.DeviceLabel.Text+"'");
   // Touch layout structure (a second hud built while screen controls are on): two-row strip, the speed cluster
   // below it, the minimap below that - no overlap, everything inside the canvas.
   var touchHud=new Hud{Race=race,Player=player};AddChild(touchHud);await Frame();await Frame();
   float cvw=GetViewport().GetVisibleRect().Size.X,cvh=GetViewport().GetVisibleRect().Size.Y;
   var stripRect=touchHud.StripPanel.GetGlobalRect();var spRect=touchHud.SpeedLabel.GetGlobalRect();var mapRect=touchHud.MiniMapPanel.GetGlobalRect();
   Check(stripRect.End.Y<=spRect.Position.Y+1,"touch speed cluster overlaps the strip: strip="+stripRect+" speed="+spRect);
   Check(spRect.End.Y<=mapRect.Position.Y+1,"touch minimap overlaps the speed cluster: map="+mapRect+" speed="+spRect);
   Check(stripRect.End.X<=cvw+1&&spRect.End.X<=cvw+1&&mapRect.End.X<=cvw+1&&mapRect.End.Y<=cvh+1,"touch hud exceeds the canvas: strip="+stripRect+" speed="+spRect+" map="+mapRect);
   touchHud.QueueFree();await Frame();await Frame();
   Settings.Set("screen_controls",touchBefore);await Frame();await Frame();
   // Chip cohesion: the standings, the ERS and the desktop speed chip resolve to the shared surface (6px corners,
   // translucent), and the standings sits below the strip on the shared left rail.
   var standBox=hud.StandingsPanel.GetThemeStylebox("panel") as StyleBoxFlat;Check(standBox!=null&&standBox.CornerRadiusTopLeft==6&&standBox.BgColor.A<0.95f,"standings surface is not the shared chip");
   var ersBox=hud.HybridControls.GetThemeStylebox("panel") as StyleBoxFlat;Check(ersBox!=null&&ersBox.CornerRadiusTopLeft==6&&ersBox.BgColor.A<0.95f,"ers surface is not the shared chip");
   Check(hud.SpeedLabel.GetParent().GetParent() is PanelContainer,"desktop speed pair is not chip-wrapped");
   var standRect=hud.StandingsPanel.GetGlobalRect();Check(standRect.Position.Y>=hud.StripPanel.GetGlobalRect().End.Y-1,"standings surface overlaps the strip: "+standRect);
   // Session state: lap, timing, finish - independent literal formats. Two-car race shows the live POS grouping.
   Check(hud.InfoLabel.Text.Contains("POS")&&hud.InfoLabel.Text.Contains("/2"),"hud position grouping missing in a two-car race: '"+hud.InfoLabel.Text+"'");
   Check(hud.InfoLabel.Text.Contains("LAP 1/3")&&hud.InfoLabel.Text.Contains("LAP TIME 0.0s")&&hud.InfoLabel.Text.Contains("BEST -- (no valid lap)"),"hud session values not bound to live race state: '"+hud.InfoLabel.Text+"'");
   player.lapElapsed=12.34f;player.bestLap=87.52f;player.completedLaps=1;await Frame();await Frame();
   Check(hud.InfoLabel.Text.Contains("LAP 2/3")&&hud.InfoLabel.Text.Contains("LAP TIME 12.3s")&&hud.InfoLabel.Text.Contains("BEST 87.520s"),"hud timing not bound to live lap state: '"+hud.InfoLabel.Text+"'");
   player.finished=true;await Frame();await Frame();
   Check(hud.InfoLabel.Text.Contains("FINISHED 3 laps"),"hud finish state not bound to live race state: '"+hud.InfoLabel.Text+"'");
   player.finished=false;player.completedLaps=0;
   // Cockpit camera contract: the seat, the 9-deg instrument pitch and the FOV are the asserted source of truth
   // (the reviewer's standing reject: halo dominance and the unreadable wheel/dash at the level eye line).
   Check(DriveController.CockpitFov==68f,"cockpit fov drifted from the contract");
   Check(DriveController.CockpitSeat(true).IsEqualApprox(new Vector3(0,0.88f,0.05f)),"open-wheel cockpit seat moved");
   Check(DriveController.CockpitSeat(false).IsEqualApprox(new Vector3(-0.35f,0.88f,0.05f)),"closed-car cockpit seat moved");
   var cockpitBasis=DriveController.CockpitPose(new Basis());
   Check(cockpitBasis.Z.Y>0.14f&&cockpitBasis.Z.Y<0.17f,"cockpit eye-line pitch moved out of the instrument band: "+cockpitBasis.Z.Y.ToString("0.000"));
   Check(cockpitBasis.X.X<-0.99f,"cockpit camera handedness wrong (mirrored view)");
   // Pause display uses the same flag the game sets; paused state freezes the live readouts (no fake injection).
   hud.Paused=true;await Frame();await Frame();
   Check(hud.InfoLabel.Text=="PAUSED  (Esc / Tab to resume)","paused hud text missing: '"+hud.InfoLabel.Text+"'");
   hud.Paused=false;await Frame();await Frame();
   // Pause display uses the same flag the game sets; paused state freezes the live readouts (no fake injection).
   hud.Paused=true;await Frame();await Frame();
   Check(hud.InfoLabel.Text=="PAUSED  (Esc / Tab to resume)","paused hud text missing: '"+hud.InfoLabel.Text+"'");
   hud.Paused=false;await Frame();await Frame();
   // Bindings panel: the pause shows the live binding list; the setting hides it; a remap reflects without
   // reopening pause; the panel sits inside the canvas, right of the centered pause column.
   hud.Paused=true;await Frame();await Frame();
   Check(hud.BindingsNode!=null&&hud.BindingsNode.Visible,"bindings panel hidden under pause while show_bindings is on");
   var key0=(Label)hud.BindingsNode.FindChild("Key0",true,false);Check(key0!=null,"bindings row Key0 missing");
   Check(key0.Text==OS.GetKeycodeString(ControlBindings.Get(0)),"bindings row not live from ControlBindings: '"+key0.Text+"'");
   Key throttleBefore=ControlBindings.Get(0);Settings.Set("key_0",(long)Key.J);await Frame();await Frame();
   Check(key0.Text=="J","remapped throttle key not reflected in the bindings panel: '"+key0.Text+"'");
   Settings.Set("key_0",(long)throttleBefore);
   Settings.Set("show_bindings",false);await Frame();await Frame();
   Check(!hud.BindingsNode.Visible,"show_bindings off did not hide the panel");
   Settings.Set("show_bindings",true);await Frame();await Frame();
   Check(hud.BindingsNode.Visible,"show_bindings on did not restore the panel");
   var bindRect=hud.BindingsNode.GetGlobalRect();float bindW=GetViewport().GetVisibleRect().Size.X;
   Check(bindRect.Position.X>=741&&bindRect.End.X<=bindW-15&&bindRect.Position.Y>=15&&bindRect.End.Y<=GetViewport().GetVisibleRect().Size.Y-1,"bindings panel outside the designed right column: "+bindRect);
   hud.Paused=false;await Frame();await Frame();
   // Classification: the panel's first child is its HEADER; data rows are named Car<id>. Each named row
   // must carry the slot the LIVE standings gives that same car (order-agnostic view binding).
   player.progress=0.10f;opp.progress=0.20f;for(int i=0;i<16;i++)await Frame();
   var rows=hud.StandingsPanel.GetNode<VBoxContainer>("StandingsRows");var kids=rows.GetChildren();int shown=0;
   Check(kids.Count>0&&kids[0] is Label headerLabel&&headerLabel.Text=="CLASSIFICATION","standings header missing or moved: '"+(kids.Count>0&&kids[0] is Label h?h.Text:"<none>")+"'");
   var rankOrder=race.Standings();
   for(int i=0;i<rankOrder.Count;i++){var car=rankOrder[i];var rowLabel=rows.GetNodeOrNull<Label>("Car"+car.id);Check(rowLabel!=null,"standings data row missing for car "+car.id);
    string name=CarCatalog.Names[car.carModel];if(name.Length>18)name=name.Substring(0,18);
    // Gap column: the leader row carries none; a trailing running car carries its live metres deficit to the leader.
    string gap="";if(i>0&&!car.finished&&!car.retired){float metres=(race.DistanceScore(rankOrder[0])-race.DistanceScore(car))*race.track.length;if(metres>=1f)gap="  +"+(int)metres+"m";}
    Check(rowLabel.Text==(i+1).ToString("00")+"  "+name+"  L"+System.Math.Min(car.completedLaps+1,race.targetLaps)+gap,"standings row not bound to live order: car "+car.id+" = '"+rowLabel.Text+"'");
    if(i==1)Check(gap.Length>0&&rankOrder[0].id!=rankOrder[1].id,"trailing row lost the live gap column: '"+rowLabel.Text+"'");
    if(i==0)Check(!rankOrder[0].finished&&rowLabel.Text.EndsWith("L"+System.Math.Min(car.completedLaps+1,race.targetLaps)),"leader row gained a gap or lost its state");
    shown++;}
   Check(shown>0,"standings panel rendered no data rows");
   // ERS charge display equals the live battery; deploying state toggles from the model.
   int modelBefore=player.carModel;player.carModel=16;await Frame();await Frame();
   var hybrid=hud.HybridControls;Check(hybrid!=null&&hybrid.Visible,"hybrid controls missing on the reference F1 car");
   var charge=(Label)hybrid.FindChildren("*","Label",true,false)[0];
   player.ersCharge=0.87f;await Frame();await Frame();
   Check(charge.Text=="ERS 87%","ers charge display not bound to the live battery: '"+charge.Text+"'");
   player.carModel=modelBefore;await Frame();await Frame();
   // Sense-of-speed integrity: track scale, lap timing and displayed speed tell one story (route data is metres; the recorded clean lap is 87.52 s).
   var circuit=new Circuit(0);Check(circuit.length>1500f&&circuit.length<2500f,"track0 route scale out of range: "+circuit.length);
   float avgKmh=circuit.length/87.52f*3.6f;Check(Mathf.Abs(avgKmh-79.25f)<1.5f,"clean-lap telemetry vs route scale inconsistent: "+avgKmh.ToString("0.0")+" km/h over "+circuit.length.ToString("0")+" m");
   // Wheel rotation is distance over radius on every corner, mirrored sides included (omega vs travelled distance).
   var wheelRig=new Node3D();AddChild(wheelRig);var rig=new Node3D[4];
   for(int i=0;i<4;i++){float baseYaw=i%2==0?0f:Mathf.Pi;var pivot=new Node3D{Rotation=new Vector3(0,baseYaw,0)};pivot.SetMeta("steer_base_yaw",baseYaw);pivot.SetMeta("radius",.5f);pivot.AddChild(new Node3D{Name="Spin"});wheelRig.AddChild(pivot);rig[i]=pivot;}
   WheelAnimation.Update(rig,5f,0f);
   Check(Mathf.Abs(rig[0].GetNode<Node3D>("Spin").Rotation.X-10f)<1e-4f,"wheel angle != distance/radius: "+rig[0].GetNode<Node3D>("Spin").Rotation.X);
   Check(Mathf.Abs(rig[1].GetNode<Node3D>("Spin").Rotation.X+10f)<1e-4f,"mirrored wheel angle sign wrong: "+rig[1].GetNode<Node3D>("Spin").Rotation.X);
   WheelAnimation.Update(rig,7f,0f);
   foreach(var pivot in rig){var spinNode=pivot.GetNode<Node3D>("Spin");Check(Mathf.Abs(Mathf.Abs(spinNode.Rotation.X)*.5f-7f)<1e-4f,"wheel surface speed inconsistent with the distance travelled: "+spinNode.Rotation.X);}
   wheelRig.QueueFree();
   // Sound integrity: every sample the controller can play exists in the shipped assets (no synthetic loops); mute reaches the master bus.
   Check(ResourceLoader.Exists("res://assets/Resources/RealRacing/EngineIdle.wav"),"engine idle sample missing (fake-loop guard)");
   foreach(var clip in new[]{"transmission_whine_on_very_low","transmission_whine_on_low","transmission_whine_on_mid","transmission_whine_on_high","transmission_whine_off_very_low","transmission_whine_off_low","transmission_whine_off_mid","transmission_whine_off_high","spool_01","turbo_01"})Check(ResourceLoader.Exists("res://assets/Resources/RealRacing/Audio/"+clip+".wav"),"authentic audio layer sample missing: "+clip);
   bool muteBefore=Settings.Get("mute",true);Settings.Set("mute",true);Settings.ApplyAudio();Check(AudioServer.IsBusMute(0),"mute setting did not mute the master bus");
   Settings.Set("mute",false);Settings.ApplyAudio();Check(!AudioServer.IsBusMute(0),"unmute did not clear the master bus");Settings.Set("mute",muteBefore);Settings.ApplyAudio();
   // Focus: product panels open with keyboard focus on their primary control; escape closes.
   var menu=new ModeMenu();AddChild(menu);await Frame();await Frame();
   Check(GetViewport().GuiGetFocusOwner()!=null,"mode menu opened without keyboard focus");
   menu.QueueFree();await Frame();await Frame();
   var settings=new RaceSettingsPanel();AddChild(settings);await Frame();await Frame();
   Check(GetViewport().GuiGetFocusOwner()!=null&&GetViewport().GuiGetFocusOwner().Name=="SaveBack","settings panel focus is not SAVE & BACK");
   Input.ParseInputEvent(new InputEventKey{Keycode=Key.Escape,Pressed=true});await Frame();await Frame();
   Check(!IsInstanceValid(settings),"settings panel escape did not close");
   var series=new SeriesSetupPanel();AddChild(series);await Frame();await Frame();
   Check(GetViewport().GuiGetFocusOwner()!=null&&GetViewport().GuiGetFocusOwner() is OptionButton,"series panel focus is not the track choice");
   Input.ParseInputEvent(new InputEventKey{Keycode=Key.Escape,Pressed=true});await Frame();await Frame();
   Check(!IsInstanceValid(series),"series panel escape did not close");
   // Left-rail rect sanity at the fixture canvas (deep per-resolution bounds stays with the cloud HUD_RECTS gates).
   float vw=GetViewport().GetVisibleRect().Size.X,vh=GetViewport().GetVisibleRect().Size.Y;
   var panelRect=hud.StandingsPanel.GetGlobalRect();
   Check(panelRect.Position.X>=-1&&panelRect.Position.Y>=-1&&panelRect.Position.X+panelRect.Size.X<=vw+1&&panelRect.Position.Y+panelRect.Size.Y<=vh+1,"standings panel outside canvas: pos="+panelRect.Position+" size="+panelRect.Size+" canvas="+vw+"x"+vh);
   GD.Print("PASS product hud binding: speed/gear/timing/finish, compound+pits, weather/night, device, ERS, focus and escape");
   if(DisplayServer.GetName()!="headless"&&System.Environment.GetEnvironmentVariable("MYSIM_SHOT") is string productShot)GetViewport().GetTexture().GetImage().SavePng(productShot);
   GD.Print("CHECKS productControls=ok");GetTree().Quit();
  }catch(Exception e){GD.PrintErr("FAIL product controls ",e);GetTree().Quit(1);}
 }
}
}
