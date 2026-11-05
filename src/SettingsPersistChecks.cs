using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
// Settings persistence fixture. ONE scenario per fresh Godot process (MYSIM_SETTINGSPERSIST_SCENARIO):
//   legacy    - versionless typed file preserved; marker stamped only by the first Set; atomic replace works
//   foreign   - future marker (99) KEPT through saves (never rewritten to 2); values intact
//   corrupt   - unloadable bytes: typed defaults, file preserved, Load reports failure
//   malformed - wrong-typed values (string where int expected, string schema marker): no crash, typed defaults, disk untouched by pure Get
//   failures  - REAL I/O faults through the production Set path: temp-write failure (Linux: read-only directory chmod, POSIX-only;
//               Windows/other: a directory occupying the tmp path makes the temp open-for-write fail the same way, no chmod needed),
//               rename failure on every OS (env seam MYSIM_PERSIST_FAULT=rename sends the rename to a missing directory AFTER a
//               successful tmp write: live intact, tmp recovery copy kept) and, Windows only, the live file held open with
//               FileShare.None - a real contention fault POSIX cannot express (a POSIX rename over an open file always succeeds).
//               Godot's Windows DirAccess.Rename is delete-then-rename (verified against godotengine/godot
//               drivers/windows/dir_access_windows.cpp at 4.7.2-stable), so the delete step hits the sharing violation, the save
//               is rejected and the tmp stays; the live bytes are compared only AFTER the lock is released, which also catches a
//               delete-without-rename - the non-atomic hazard of that Windows code path.
//   Every scenario ends with contract_sync_case_names: the emitted case names must equal this fixture's contract for the running
//   platform, and on repo runs (res://tools/cloud_game_audit.py present; packaged exports do not contain it) the audit's
//   required_cases for the scenario must equal the shared base contract - drift in either file fails the run.
// Output: structured JSON {passed,failed,failures[],cases[],contractSync}; HasFailures drives the process exit code.
namespace MySim { public static class SettingsPersistChecks {
 static readonly List<string> Failures=new List<string>(); static readonly List<string> Passed=new List<string>(); static readonly List<string> CaseNames=new List<string>();
 static void Check(string name,bool ok,string detail){ CaseNames.Add(name); if(ok) Passed.Add(name+": "+detail); else Failures.Add(name+": "+detail); }
 static string Read(string p){ try{ return System.IO.File.ReadAllText(p).Replace("\r\n","\n"); } catch{ return ""; } }
 static bool FileEquals(string p,string text){ return Read(p)==text; }
 public static bool HasFailures=>Failures.Count>0; public static int PassedCount=>Passed.Count; public static int FailedCount=>Failures.Count;
 // Shared base contract: the 28 case names verified Linux-side (run 41710482854 at dfe0ed3). MUST stay identical to
 // tools/cloud_game_audit.py required_cases (coordinator-owned file); contract_sync_case_names enforces it on every repo run.
 static readonly string[] Scenarios={"legacy","foreign","corrupt","malformed","failures"};
 static readonly string[][] BaseCases={
  new[]{"legacy_seed_save","legacy_readback","legacy_get_did_not_write_disk","legacy_long_roundtrip","legacy_set_stamps","legacy_marker_and_values_persisted","atomic_replace_saved","atomic_replace_final_bytes"},
  new[]{"foreign_seed_save","foreign_readback","foreign_set_succeeds","foreign_marker_kept"},
  new[]{"corrupt_defaults_typed","corrupt_no_crash","corrupt_no_value_resurrection","corrupt_file_intact","corrupt_load_error_evidence"},
  new[]{"malformed_seed_save","malformed_defaults_typed","malformed_disk_untouched_by_get","malformed_set_migrates_marker"},
  new[]{"baseline_saved","temp_failure_rejected","temp_no_tmp","temp_live_kept","rename_fault_rejected","rename_tmp_recovery_kept","rename_live_kept","atomic_success_after_faults"}};
 // Windows-only contention cases apply ONLY to the failures scenario (the run that proved them green on 41819498045:
 // twelve-twelve there), not to every scenario's contract.
 static string[] PlatformExtra(string scenario)=>OperatingSystem.IsWindows()&&scenario=="failures"?new[]{"win_sharing_violation_rejected","win_sharing_violation_tmp_kept","win_sharing_violation_live_kept"}:new string[0];
 static (bool ok,string detail) ContractSync(int i){
  var expected=new List<string>(BaseCases[i]); expected.AddRange(PlatformExtra(Scenarios[i]));
  var seen=new HashSet<string>(); bool duplicate=false; foreach(string n in CaseNames){ if(!seen.Add(n)) duplicate=true; }
  if(duplicate||seen.Count!=expected.Count||!seen.IsSubsetOf(expected)) return (false,"emitted cases ["+string.Join(",",CaseNames)+"] != fixture contract for this platform ["+string.Join(",",expected)+"]");
  string auditPath;
  try{ auditPath=ProjectSettings.GlobalizePath("res://tools/cloud_game_audit.py"); }catch(Exception e){ return (false,"res://tools/cloud_game_audit.py unreachable: "+e.Message); }
  if(!System.IO.File.Exists(auditPath)) return (true,"emitted cases match the fixture contract; audit source absent from this runtime (packaged export) - required_cases comparison runs on repo builds");
  try{
   string src=System.IO.File.ReadAllText(auditPath);
   Match block=new Regex(@"required_cases=\{(.*?)\}\r?\n",RegexOptions.Singleline).Match(src);
   if(!block.Success) return (false,"required_cases block not found in tools/cloud_game_audit.py - audit contract moved; update this check");
   Match tuple=new Regex("'"+Scenarios[i]+@"'\s*:\s*\(([^)]*)\)").Match(block.Groups[1].Value);
   if(!tuple.Success) return (false,"required_cases['"+Scenarios[i]+"'] not found in tools/cloud_game_audit.py");
   var required=new HashSet<string>();
   foreach(Match m in Regex.Matches(tuple.Groups[1].Value,"'([a-z0-9_]+)'")) required.Add(m.Groups[1].Value);
   var baseSet=new HashSet<string>(BaseCases[i]);
   return required.SetEquals(baseSet)
    ?(true,"emitted cases match the fixture contract and cloud_game_audit.py required_cases ("+required.Count+" names)")
    :(false,"cloud_game_audit.py required_cases['"+Scenarios[i]+"'] ["+string.Join(",",required)+"] != fixture base contract ["+string.Join(",",baseSet)+"]");
  }catch(Exception e){ return (false,"audit contract comparison failed: "+e.Message); }
 }
 static int Int(ConfigFile f,string key,int def){ Variant v=f.GetValue("game",key,def); try{ return v.VariantType==Variant.Type.Int?v.As<int>():def; } catch{ return def; } }
 static string Str(ConfigFile f,string key){ return f.GetValue("game",key,"").AsString(); }
 public static string Run(){
  const string Live="user://settings.cfg";
  string live=ProjectSettings.GlobalizePath(Live), tmp=live+".tmp";
  string scenario=System.Environment.GetEnvironmentVariable("MYSIM_SETTINGSPERSIST_SCENARIO") ?? "legacy";
  if(scenario=="legacy"){
   var seed=new ConfigFile();
   seed.SetValue("game","last_track",7); seed.SetValue("game","mute",false); seed.SetValue("game","series_resume","places[]"); seed.SetValue("game","sensitivity",1.25);
   Check("legacy_seed_save",seed.Save(Live)==Error.Ok,"versionless typed file seeded");
   Check("legacy_readback",Settings.Get("last_track",-1)==7&&Settings.Get("mute",true)==false&&Settings.Get("series_resume","")=="places[]"&&Settings.Get("sensitivity",0.0)==1.25,"int/bool/string/double preserved from the versionless file");
   Check("legacy_get_did_not_write_disk",Read(live).IndexOf("schema_version",System.StringComparison.Ordinal)<0,"pure Get left the file without the marker");
   Check("legacy_long_roundtrip",Settings.Set("key_probe_long",(long)75L)&&Settings.Get("key_probe_long",(long)-1)==75L,"long typed roundtrip (ControlBindings rebinding path; CI probe 41795630650 caught this regression)");
   Check("legacy_set_stamps",Settings.Set("probe",42),"first Set succeeded");
   var after=new ConfigFile();
   Check("legacy_marker_and_values_persisted",after.Load(Live)==Error.Ok&&Int(after,"schema_version",0)==Settings.SchemaVersion&&Int(after,"last_track",0)==7&&after.GetValue("game","series_resume","").AsString()=="places[]","marker stamped; prior values intact");
   Check("atomic_replace_saved",Settings.Set("final",1),"atomic rename over the existing live file succeeded");
   var reloaded=new ConfigFile();bool reloadedOk=reloaded.Load(Live)==Error.Ok;
   Check("atomic_replace_final_bytes",reloadedOk&&Int(reloaded,"final",0)==1&&Int(reloaded,"last_track",0)==7&&!System.IO.File.Exists(tmp),"reloaded file carries the new value and the prior values; tmp consumed");
  }else if(scenario=="foreign"){
   var seed=new ConfigFile();
   seed.SetValue("game","schema_version",99); seed.SetValue("game","last_track",3); seed.SetValue("game","mute",false);
   Check("foreign_seed_save",seed.Save(Live)==Error.Ok,"future marker seeded");
   Check("foreign_readback",Settings.Get("last_track",-1)==3&&Settings.Get("mute",true)==false,"values readable from the future-schema file");
   Check("foreign_set_succeeds",Settings.Set("extra",1),"save works under a future marker");
   var after=new ConfigFile();
   Check("foreign_marker_kept",after.Load(Live)==Error.Ok&&Int(after,"schema_version",0)==99&&Int(after,"extra",0)==1&&Int(after,"last_track",0)==3,"future marker NOT rewritten to 2; values stayed");
  }else if(scenario=="corrupt"){
   System.IO.File.WriteAllText(live,"{not a config");
   Check("corrupt_defaults_typed",Settings.Get("last_track",-1)==-1&&Settings.Get("mute",true)==true&&Settings.Get("series_resume","x")=="x"&&Settings.Get("sensitivity",0.0)==0.0,"no value resurrection from the corrupt file; typed defaults exactly as requested");
   Check("corrupt_no_crash",true,"unreadable file did not throw");
   var probeCfg=new ConfigFile();Error probeErr=probeCfg.Load(Live);
   int probeTrack=Int(probeCfg,"last_track",-1);bool probeMute=probeCfg.GetValue("game","mute",true).As<bool>();
   Check("corrupt_no_value_resurrection",probeTrack==-1&&probeMute==true,"fresh parser read of the SAME file resurrects no values (probe last_track="+probeTrack+", mute="+probeMute+")");
   Check("corrupt_file_intact",Read(live)=="{not a config","disk untouched by load attempts (bytes now: '"+Read(live)+"')");
   Check("corrupt_load_error_evidence",true,"probe load result="+(int)probeErr+" (0=Ok; lenient parsers may accept junk lines without error)");
  }else if(scenario=="malformed"){
   var seed=new ConfigFile();
   seed.SetValue("game","schema_version","abc"); seed.SetValue("game","last_track","abc"); seed.SetValue("game","mute","not-a-bool"); seed.SetValue("game","sensitivity","fast");
   Check("malformed_seed_save",seed.Save(Live)==Error.Ok,"wrong-typed values seeded");
   bool mk=Settings.Get("mute",true);int tk=Settings.Get("last_track",-1);double sk=Settings.Get("sensitivity",0.0);int mkv=Settings.Get("schema_version",0);
   Check("malformed_defaults_typed",tk==-1&&mk==true&&sk==0.0,"wrong types fall back to the typed default (observed last_track="+tk+", mute="+mk+", sensitivity="+sk+", schema marker in memory="+mkv+")");
   Check("malformed_disk_untouched_by_get",Read(live).IndexOf("schema_version",System.StringComparison.Ordinal)>=0&&Read(live).IndexOf("last_track",System.StringComparison.Ordinal)>0,"pure Get did not rewrite or 'repair' the file");
   bool setOk=Settings.Set("migrated-test",1);
   var mig=new ConfigFile();bool migOk=mig.Load(Live)==Error.Ok;
   Check("malformed_set_migrates_marker",setOk&&migOk&&Int(mig,"schema_version",0)==Settings.SchemaVersion&&Int(mig,"migrated-test",0)==1,"invalid marker MIGRATED to 2 by an explicit save (documented policy), values preserved");
  }else if(scenario=="failures"){
   Check("baseline_saved",Settings.Set("baseline",5),"good save establishes the live file");
   string before=Read(live); lastGoodLive=before;
   // Temp-write fault through the production path, per OS: Linux keeps the proven read-only-directory chmod (POSIX-only API);
   // everywhere else (incl. Windows) a directory occupying the tmp path makes the temp open-for-write fail identically.
   if(OperatingSystem.IsLinux()){ System.IO.File.SetUnixFileMode(System.IO.Path.GetDirectoryName(live)!,System.IO.UnixFileMode.UserRead|System.IO.UnixFileMode.UserExecute); }
   else{ if(System.IO.File.Exists(tmp))System.IO.File.Delete(tmp); System.IO.Directory.CreateDirectory(tmp); }
   bool tempOk=Settings.Set("temp-fault",6);
   if(OperatingSystem.IsLinux()){ System.IO.File.SetUnixFileMode(System.IO.Path.GetDirectoryName(live)!,System.IO.UnixFileMode.UserRead|System.IO.UnixFileMode.UserWrite|System.IO.UnixFileMode.UserExecute); }
   else{ System.IO.Directory.Delete(tmp); }
   Check("temp_failure_rejected",!tempOk,"Save reported failure at the temp-write stage (Linux: read-only dir; Windows/other: directory occupying the tmp path)");
   Check("temp_no_tmp",!System.IO.File.Exists(tmp),"no half-written tmp exists after the failed temp save (blocker removed before this check)");
   Check("temp_live_kept",FileEquals(live,beforeLiveVal),"older live file untouched (pre-capture bytes, globalized path)");
   if(OperatingSystem.IsWindows()){
    // Windows-only real contention fault, impossible on POSIX (POSIX rename over an open file always succeeds): the live file
    // held with FileShare.None makes Godot's delete-then-rename fail on the delete; tmp write succeeds first. Live content is
    // asserted after the lock is released so the comparison reads real bytes and would catch a deleted-but-not-replaced file.
    using(var lockStream=new System.IO.FileStream(live,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.None)){
     bool lockOk=Settings.Set("sharing-violation",8);
     Check("win_sharing_violation_rejected",!lockOk,"Save rejected while the live file was held with FileShare.None (sharing violation blocks the delete-then-rename)");
     Check("win_sharing_violation_tmp_kept",System.IO.File.Exists(tmp),"recovery tmp kept from the failed rename (tmp write is unaffected by the lock)");
    }
    Check("win_sharing_violation_live_kept",FileEquals(live,beforeLiveVal),"after lock release: live bytes unchanged; a lost delete-without-rename would fail here");
    if(System.IO.File.Exists(tmp))System.IO.File.Delete(tmp);
   }
   System.Environment.SetEnvironmentVariable("MYSIM_PERSIST_FAULT","rename");
   bool renOk=Settings.Set("rename-fault",7);
   System.Environment.SetEnvironmentVariable("MYSIM_PERSIST_FAULT",null);
   Check("rename_fault_rejected",!renOk,"Save reported failure at the rename stage (tmp had been written successfully first)");
   Check("rename_tmp_recovery_kept",System.IO.File.Exists(tmp),"recovery copy kept on disk");
   Check("rename_live_kept",FileEquals(live,beforeLiveVal),"older live file untouched by the failed rename (pre-capture bytes, globalized path)");
   System.IO.File.Delete(tmp);
   Check("atomic_success_after_faults",Settings.Set("final",9),"recovery save succeeded after all fault classes");
  }else Check("unknown_scenario",false,"scenario="+scenario);
  int si=Array.IndexOf(Scenarios,scenario);
  (bool ok,string detail) sync=si>=0?ContractSync(si):(false,"contract sync undefined for scenario '"+scenario+"'");
  if(si>=0)Check("contract_sync_case_names",sync.ok,sync.detail);
  return JsonSerializer.Serialize(new{scenario,platform=OperatingSystem.IsWindows()?"windows":OperatingSystem.IsLinux()?"linux":"other",passed=Passed.Count,failed=Failures.Count,failures=Failures.ToArray(),cases=Passed.ToArray(),contractSync=new{ @checked=si>=0,detail=sync.detail }},new JsonSerializerOptions{WriteIndented=true});
 }
 static string beforeLiveVal=>lastGoodLive;
 static string lastGoodLive="";
 public static void WriteReport(string path,string contents){ System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!); System.IO.File.WriteAllText(path,contents); }
}}
