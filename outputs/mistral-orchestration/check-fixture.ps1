Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41699253306 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("FIXTURE_RUN " + $_.status + " " + $_.conclusion) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
