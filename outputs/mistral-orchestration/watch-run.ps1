Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run watch 41688779865 -R instagramshashank8-bot/mysim-godot --exit-status --interval 30 2>&1 | Select-Object -Last 3
gh run view 41688779865 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("RESULT " + $_.status + " " + $_.conclusion) }
gh run download 41688779865 -R instagramshashank8-bot/mysim-godot -D outputs/repair-20261029/cloud/settings-persist-result 2>&1 | Out-Null
if (Test-Path outputs/repair-20261029/cloud/settings-persist-result) { Get-ChildItem -Recurse outputs/repair-20261029/cloud/settings-persist-result -File | Select-Object -First 8 -ExpandProperty FullName }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
