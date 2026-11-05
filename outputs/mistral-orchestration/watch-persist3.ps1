Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run watch 41707159183 -R instagramshashank8-bot/mysim-godot --exit-status --interval 60 2>&1 | Select-Object -Last 2
gh run view 41707159183 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("PERSIST3 " + $_.status + " " + $_.conclusion) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
