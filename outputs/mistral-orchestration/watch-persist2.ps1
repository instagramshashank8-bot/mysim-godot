Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run watch 41703220126 -R instagramshashank8-bot/mysim-godot --exit-status --interval 60 2>&1 | Select-Object -Last 2
gh run view 41703220126 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("PERSIST2 " + $_.status + " " + $_.conclusion) }
if (-not $?) { }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
