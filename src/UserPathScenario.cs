using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
// USER-PATH input harness (spec: agents/verification-turn8.md; implemented turn 9) - JSON-scripted DEVICE-level input
// injection into the REAL running game: key events -> Input -> ControlBindings.Held -> DriveController - the layer the
// DV profiles never touch (DrivingValidation sets ph.Throttle/Brake/Steer directly on the simulation; verified: no
// Input API in that file).
// Injection, verified against sources before use:
// - keys: Input.ParseInputEvent(InputEventKey{Keycode,PhysicalKeycode,Pressed}) - godot 4.7.2-stable core/input/input.cpp
//   _parse_input_event_impl updates physical_keys_pressed, which Input.IsPhysicalKeyPressed (ControlBindings.Held) reads;
//   the same pattern is proven green by the existing ControlsScenario/DrivingScenario CI runs.
// - gamepad: Input.ParseInputEvent(InputEventJoypadMotion) DOES update axis state (set_joy_axis in the same parse path;
//   Input::get_joy_axis reads that state) - BUT DriveController gates the pad branch on Input.GetConnectedJoypads(), and
//   device registration happens ONLY in Input::joy_connection_changed (OS-driven; no script-level joypad-add API exists
//   in 4.7.2). On a cloud runner with no physical pad the pad branch never runs, so the shipped profiles use the
//   KEYBOARD path exclusively (the game's DEFAULT control scheme); device:'pad' stays supported in the schema for a
//   future virtual/HIL setup and every event is recorded either way.
// REQUIRES MYSIM_INPUTCHECKS=1 (ControlBindings.Held returns false when the window is unfocused otherwise) and the
// Main.cs hook MUST NOT also spawn DrivingScenario under MYSIM_USERPATH (it injects its own key script and exits the
// process; see the hook request in agents/verification-turn9.md). This node also defends itself: on every tick it
// removes a DrivingScenario sibling if the unguarded hook added one.
// Script (MYSIM_USERPATH_SCRIPT, JSON; the profiles live in data/userpath_profiles.json):
//   {"profile":name,"durationSeconds":n,"startOnRaceStart":bool,
//    "events":[{"t":sec,"device":"key"|"pad","key":"W"|,"axis":"left_stick_x|left_stick_y|right_stick_x|right_stick_y|rt|lt",
//               "action":"press"|"release"|"value","value":0..1}],
//    "asserts":[{"id":name,"kind":...,"params..."}]}
// Assert kinds (evaluated over the 10 Hz telemetry): speedAbove{t,value}, speedBelow{t,value},
//   heldEchoAtLeast{action,from,to,minFraction}, heldEchoAtMost{action,from,to,maxFraction},
//   yawRateMagnitudeAfter{afterT,window:minMagnitude via to}, lateralOscillationsAtLeast{from,to,minCount},
//   forwardProgressAtLeast{from,to,minMeters}, reverseReached{t:before,limit}; noNonFinite is always added.
// Outputs: userpath-events.jsonl (every injected event), userpath-telemetry.json (the 10 Hz samples incl. the held[]
//   echo), MYSIM_USERPATH_REPORT {profile,platform,passed,failed,failures[],checks[],events,samples}; Quit(1) on any
//   failure. The clock starts when the player exists (startOnRaceStart=false) or when Race.started flips (default) so
//   profile time is independent of load time; a 90 s wait limit fails loudly (grid-hold class) instead of hanging CI.
namespace MySim { public sealed partial class UserPathScenario : Node {
 public DriveController Drive=null!;
 sealed class ScriptEvent{ public double t{get;set;} public string device{get;set;}="key"; public string key{get;set;}=""; public string axis{get;set;}=""; public string action{get;set;}="press"; public double value{get;set;} }
 sealed class AssertSpec{ public string id{get;set;}=""; public string kind{get;set;}=""; public double t{get;set;} public double from{get;set;} public double to{get;set;} public double value{get;set;} public double minFraction{get;set;} public double maxFraction{get;set;} public double minMagnitude{get;set;} public double minMeters{get;set;} public double limit{get;set;} public int minCount{get;set;} public string action{get;set;}=""; }
 sealed class ScriptDoc{ public string profile{get;set;}="userpath"; public double durationSeconds{get;set;}=10; public bool startOnRaceStart{get;set;}=true; public List<ScriptEvent> events{get;set;}=new(); public List<AssertSpec> asserts{get;set;}=new(); }
 sealed class Sample{ public double t{get;set;} public float speed{get;set;} public float heading{get;set;} public float yawRate{get;set;} public float steering{get;set;} public float rpm{get;set;} public float throttle{get;set;} public float brake{get;set;} public float steerCmd{get;set;} public float x{get;set;} public float z{get;set;} public float damage{get;set;} public int gear{get;set;} public bool[] held{get;set;}=new bool[ControlBindings.Actions.Length]; }
 List<ScriptEvent> script=new(); List<AssertSpec> asserts=new(); readonly List<Sample> samples=new();
 readonly List<string> failures=new(); readonly List<string> passed=new(); long eventsWritten;
 double elapsed,lastSample,startWait; int nextEvent; bool clockRunning,ended; bool startOnRaceStart=true; double duration=10;
 string profileName="userpath",reportPath="",telemetryPath="",eventsPath="";
 static string J(double v)=>v.ToString("0.000###",CultureInfo.InvariantCulture);
 static string P(string path){ var d=Path.GetDirectoryName(Path.GetFullPath(path)); if(!string.IsNullOrEmpty(d))Directory.CreateDirectory(d); return Path.GetFullPath(path); }
 static string PlatformName()=>OperatingSystem.IsWindows()?"windows":OperatingSystem.IsLinux()?"linux":"other";
 public override void _Ready(){
  try{
   string scriptPath=System.Environment.GetEnvironmentVariable("MYSIM_USERPATH_SCRIPT")??"";
   reportPath=System.Environment.GetEnvironmentVariable("MYSIM_USERPATH_REPORT")??"outputs/cloud-game/userpath-report.json";
   var dir=Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
   telemetryPath=System.Environment.GetEnvironmentVariable("MYSIM_USERPATH_TELEMETRY")??Path.Combine(dir,"userpath-telemetry.json");
   eventsPath=System.Environment.GetEnvironmentVariable("MYSIM_USERPATH_EVENTS")??Path.Combine(dir,"userpath-events.jsonl");
   if(scriptPath.Length==0){ Fail("no MYSIM_USERPATH_SCRIPT"); return; }
   var doc=JsonSerializer.Deserialize<ScriptDoc>(File.ReadAllText(scriptPath),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
   if(doc==null){ Fail("script deserialized to null"); return; }
   profileName=string.IsNullOrEmpty(doc.profile)?profileName:doc.profile;
   script=doc.events??new List<ScriptEvent>(); asserts=doc.asserts??new List<AssertSpec>();
   duration=doc.durationSeconds>0?doc.durationSeconds:10; startOnRaceStart=doc.startOnRaceStart;
   File.WriteAllText(P(eventsPath),""); eventsWritten=0;
   GD.Print("USERPATH start profile=",profileName," events=",script.Count," asserts=",asserts.Count," duration=",duration," startOnRaceStart=",startOnRaceStart);
  }catch(Exception e){ Fail("setup: "+e.Message); }
 }
 public override void _Process(double delta){
  if(ended)return;
  try{
   // Defensive: the MYSIM_INPUTCHECKS gate also spawns DrivingScenario (Main.cs:211), which injects its own key script
   // and exits the process; under THIS mode it must not run. Every tick, so a later-added sibling is caught too.
   var parent=GetParent();
   if(parent!=null){ foreach(Node n in parent.GetChildren()){ if(n is DrivingScenario){ n.QueueFree(); GD.Print("USERPATH removed DrivingScenario sibling"); } } }
   if(!clockRunning){
    if(Drive==null||Drive.Player==null){ startWait+=delta; if(startWait>90)Fail("player never appeared"); return; }
    if(startOnRaceStart&&(Drive.Race==null||!Drive.Race.started)){ startWait+=delta; if(startWait>90)Fail("race never started within 90s (grid-hold class)"); return; }
    clockRunning=true; GD.Print("USERPATH clock start");
   }
   elapsed+=delta;
   while(nextEvent<script.Count&&script[nextEvent].t<=elapsed){ Inject(script[nextEvent]); nextEvent++; }
   if(elapsed-lastSample>=0.1){ lastSample=elapsed; SampleNow(); }
   if(elapsed>=duration&&nextEvent>=script.Count) Finish();
  }catch(Exception e){ Fail("tick: "+e.Message); }
 }
 void Inject(ScriptEvent e){
  try{
   if(e.device=="pad"){
    JoyAxis ax;
    switch(e.axis){ case "left_stick_x":ax=JoyAxis.LeftX;break; case "left_stick_y":ax=JoyAxis.LeftY;break; case "right_stick_x":ax=JoyAxis.RightX;break; case "right_stick_y":ax=JoyAxis.RightY;break; case "rt":ax=JoyAxis.TriggerRight;break; case "lt":ax=JoyAxis.TriggerLeft;break; default: Fail("unknown pad axis '"+e.axis+"'"); return; }
    Input.ParseInputEvent(new InputEventJoypadMotion{ Device=0,Axis=ax,AxisValue=(float)e.value });
    LogEvent(e.t,"pad",e.axis,"value",e.value);
   }else{
    if(!Enum.TryParse(e.key,out Key k)){ Fail("unknown key '"+e.key+"'"); return; }
    bool press=e.action!="release";
    Input.ParseInputEvent(new InputEventKey{ Keycode=k,PhysicalKeycode=k,Pressed=press });
    LogEvent(e.t,"key",e.key,press?"press":"release",press?1:0);
   }
  }catch(Exception ex){ Fail("inject: "+ex.Message); }
 }
 void LogEvent(double t,string device,string code,string action,double value){
  try{ File.AppendAllText(P(eventsPath),"{\"t\":"+J(t)+",\"device\":\""+device+"\",\"code\":\""+code+"\",\"action\":\""+action+"\",\"value\":"+J(value)+"}\n"); eventsWritten++; }
  catch(Exception e){ Fail("event log: "+e.Message); }
 }
 void SampleNow(){
  var v=Drive.Player; if(v==null)return;
  var s=new Sample{ t=Math.Round(elapsed,3),speed=v.speed,heading=v.heading,yawRate=v.yawRate,steering=v.steering,rpm=v.rpm,gear=v.gear,x=v.position.x,z=v.position.z,damage=v.damage };
  s.throttle=v.input.throttle; s.brake=v.input.brake; s.steerCmd=v.input.steer;
  for(int i=0;i<ControlBindings.Actions.Length;i++) s.held[i]=ControlBindings.Held(i);
  samples.Add(s);
 }
 int ActionIndex(string name){
  if(int.TryParse(name,out int idx)&&idx>=0&&idx<ControlBindings.Actions.Length) return idx;
  for(int i=0;i<ControlBindings.Actions.Length;i++) if(ControlBindings.Actions[i]==name) return i;
  return -1;
 }
 Sample SampleAt(double t){ Sample best=null; double bestD=double.MaxValue; foreach(var s in samples){ double d=Math.Abs(s.t-t); if(d<bestD){bestD=d;best=s;} } return best; }
 bool AllFinite(){ foreach(var s in samples){ if(!float.IsFinite(s.speed)||!float.IsFinite(s.heading)||!float.IsFinite(s.yawRate)||!float.IsFinite(s.steering)||!float.IsFinite(s.x)||!float.IsFinite(s.z)) return false; } return true; }
 double HeldFraction(string action,double from,double to,bool want){ int idx=ActionIndex(action); if(idx<0)return -1; int hit=0,total=0; foreach(var s in samples){ if(s.t>=from&&s.t<=to){ total++; if(s.held[idx]==want)hit++; } } return total==0?-1:(double)hit/total; }
 double MeanYawRate(double from,double to){ double sum=0;int n=0; foreach(var s in samples){ if(s.t>=from&&s.t<=to){ sum+=s.yawRate;n++; } } return n==0?double.NaN:sum/n; }
 int SignChanges(double from,double to){ int c=0; int prev=0; foreach(var s in samples){ if(s.t<from||s.t>to)continue; int cur=Math.Abs(s.yawRate)<0.02f?0:(s.yawRate>0?1:-1); if(cur!=0&&prev!=0&&cur!=prev)c++; if(cur!=0)prev=cur; } return c; }
 double ForwardProgress(double from,double to){ var a=SampleAt(from);var b=SampleAt(to); if(a==null||b==null)return double.NaN; float dx=b.x-a.x,dz=b.z-a.z; float fx=(float)Math.Sin(a.heading),fz=(float)Math.Cos(a.heading); return dx*fx+dz*fz; }
 double MinSpeed(double before){ double m=double.MaxValue; foreach(var s in samples){ if(s.t<=before&&s.speed<m)m=s.speed; } return m; }
 void Check(string name,bool ok,string detail){ if(ok) passed.Add(name+": "+detail); else failures.Add(name+": "+detail); }
 void Evaluate(){
  Check("noNonFinite",AllFinite(),"all "+samples.Count+" recorded samples finite ("+eventsWritten+" events injected)");
  foreach(var a in asserts){
   switch(a.kind){
    case "speedAbove":{ var s=SampleAt(a.t); bool ok=s!=null&&s.speed>=a.value; Check(a.id,ok,"speed@"+J(a.t)+"="+(s==null?"none":J(s.speed))+" (need >= "+J(a.value)+")"); break; }
    case "speedBelow":{ var s=SampleAt(a.t); bool ok=s!=null&&s.speed<=a.value; Check(a.id,ok,"speed@"+J(a.t)+"="+(s==null?"none":J(s.speed))+" (need <= "+J(a.value)+")"); break; }
    case "heldEchoAtLeast":{ double f=HeldFraction(a.action,a.from,a.to,true); bool ok=f>=a.minFraction; Check(a.id,ok,"Held('"+a.action+"') true in ["+J(a.from)+","+J(a.to)+"] fraction="+(f<0?"none":J(f))+" (need >= "+J(a.minFraction)+")"); break; }
    case "heldEchoAtMost":{ double f=HeldFraction(a.action,a.from,a.to,true); bool ok=f>=0&&f<=a.maxFraction; Check(a.id,ok,"Held('"+a.action+"') in ["+J(a.from)+","+J(a.to)+"] fraction="+(f<0?"none":J(f))+" (need <= "+J(a.maxFraction)+")"); break; }
    case "yawRateMagnitudeAfter":{ double win=Math.Max(0.1,a.to); double m=MeanYawRate(a.t,a.t+win); bool ok=!double.IsNaN(m)&&Math.Abs(m)>=a.minMagnitude; Check(a.id,ok,"mean |yawRate| over ["+J(a.t)+","+(a.t+win)+"]="+(double.IsNaN(m)?"none":J(Math.Abs(m)))+" (need >= "+J(a.minMagnitude)+")"); break; }
    case "lateralOscillationsAtLeast":{ int c=SignChanges(a.from,a.to); Check(a.id,c>=a.minCount,"yawRate sign changes in ["+J(a.from)+","+J(a.to)+"]="+c+" (need >= "+a.minCount+")"); break; }
    case "forwardProgressAtLeast":{ double d=ForwardProgress(a.from,a.to); Check(a.id,!double.IsNaN(d)&&d>=a.minMeters,"forward progress ["+J(a.from)+","+J(a.to)+"]="+(double.IsNaN(d)?"none":J(d))+" m (need >= "+J(a.minMeters)+")"); break; }
    case "reverseReached":{ double before=a.t>0?a.t:a.to; double m=MinSpeed(before); bool ok=m<=a.limit; Check(a.id,ok,"min forward-speed before "+J(before)+"="+(m==double.MaxValue?"none":J(m))+" (need <= "+J(a.limit)+")"); break; }
    default: Check(a.id,false,"unknown assert kind '"+a.kind+"'"); break;
   }
  }
 }
 void EmitReport(){
  var report=JsonSerializer.Serialize(new{ profile=profileName,platform=PlatformName(),passed=passed.Count,failed=failures.Count,failures=failures.ToArray(),checks=passed.ToArray(),events=eventsWritten,samples=samples.Count },new JsonSerializerOptions{WriteIndented=true});
  File.WriteAllText(P(reportPath),report);
  File.WriteAllText(P(telemetryPath),JsonSerializer.Serialize(new{ profile=profileName,samples=samples },new JsonSerializerOptions{WriteIndented=true}));
  GD.Print("USERPATH ",report);
 }
 void Finish(){
  if(ended)return; ended=true;
  Evaluate();
  try{ EmitReport(); }catch(Exception e){ GD.PrintErr("USERPATH report write failed: ",e.Message); }
  GetTree().Quit(failures.Count>0?1:0);
 }
 void Fail(string why){
  if(ended)return; ended=true;
  failures.Add("harness: "+why);
  try{ EmitReport(); }catch(Exception e){ GD.PrintErr("USERPATH fail-report write failed: ",e.Message); }
  GetTree().Quit(1);
 }
}}
