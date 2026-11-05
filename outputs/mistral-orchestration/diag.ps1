Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output ("YML_SETTINGS_REFS=" + (git show 18f69a1:.github/workflows/game-review.yml | Select-String -Pattern 'SETTINGS_PERSIST' | Measure-Object).Count)
Write-Output ("MAIN_HOOK=" + (git show 18f69a1:src/Main.cs | Select-String -Pattern 'MYSIM_SETTINGS_PERSIST' | Measure-Object).Count)
Write-Output ("FIXTURE_REFS=" + (git show 18f69a1:src/SettingsPersistChecks.cs | Select-String -Pattern 'corrupt_defaults_types|rename_failure_preserves_live|tempwrite_failure' | Measure-Object).Count)
Write-Output "=== run 41675023687 failed log (first errors) ==="
gh run view 41675023687 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'error CS|##\[error\]|Error' | Select-Object -First 10 | ForEach-Object { Write-Output $_.Line }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
