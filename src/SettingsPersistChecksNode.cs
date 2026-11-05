using Godot;
using System;
// Godot-side runner node for the Settings persistence fixture: the ONLY thing this boot does (early-exit host).
// Runs the real Settings runtime against the scenario (MYSIM_SETTINGSPERSIST_SCENARIO), prints the structured
// JSON report, writes it to MYSIM_SETTINGS_REPORT (artifact-visible), and exits non-zero on any failed assertion
// or unexpected exception — cloud_game_audit fails the job unless every scenario reports passed>0, failed==0.
namespace MySim { public sealed partial class SettingsPersistChecksNode : Node {
 public override void _Ready(){
  string report;
  try { report=SettingsPersistChecks.Run(); }
  catch(Exception e){ GD.Print("SETTINGSPERSIST_EXCEPTION ",e); GD.Print("SETTINGSPERSIST_PARTIAL passed=",SettingsPersistChecks.PassedCount," failed=",SettingsPersistChecks.FailedCount); GetTree().Quit(3); return; }
  GD.Print("SETTINGSPERSIST ",report);
  try { SettingsPersistChecks.WriteReport(System.Environment.GetEnvironmentVariable("MYSIM_SETTINGS_REPORT")??"outputs/cloud-game/settings-persist-report.json",report); }
  catch(Exception e){ GD.Print("SETTINGS_REPORT_WRITE_FAILED ",e); GetTree().Quit(4); return; }
  GetTree().Quit(SettingsPersistChecks.HasFailures?1:0);
 }
}}
