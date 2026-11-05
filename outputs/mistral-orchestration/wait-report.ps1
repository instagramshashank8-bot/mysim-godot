Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
# Single blocking wait on the settings fixture run, then a summary dump; no polling loop inside the worker.
gh run watch 41699253306 -R instagramshashank8-bot/mysim-godot --exit-status --interval 45 2>&1 | Select-Object -Last 2
gh run view 41699253306 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("PERSIST_RESULT " + $_.status + " " + $_.conclusion) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
