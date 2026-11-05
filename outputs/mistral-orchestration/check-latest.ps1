Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41707159183 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("PERSIST4 " + $_.status + " " + $_.conclusion) }
gh run view 41712623422 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("REF2 " + $_.status + " " + $_.conclusion) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
