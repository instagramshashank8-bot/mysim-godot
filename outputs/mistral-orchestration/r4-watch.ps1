Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run watch 41710482854 -R instagramshashank8-bot/mysim-godot --exit-status --interval 60 2>&1 | Select-Object -Last 3
gh run view 41710482854 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("PERSIST_DFE " + $_.status + " " + $_.conclusion) }
gh run download 41710482854 -R instagramshashank8-bot/mysim-godot -D outputs/repair-20261029/cloud/settings-persist-r4 2>&1 | Out-Null
Write-Output "=== r4 settings summary ==="
if (Test-Path outputs/repair-20261029/cloud/settings-persist-r4) { Get-ChildItem outputs/repair-20261029/cloud/settings-persist-r4 -Recurse -Filter 'settings-persist-summary.json' | Select-Object -First 1 | ForEach-Object { Get-Content $_.FullName } }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
