Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output "=== failed jobs ==="
gh run view 41699253306 -R instagramshashank8-bot/mysim-godot --json jobs --jq '.jobs[] | select(.conclusion=="failure") | .name'
Write-Output "=== failed-step errors ==="
gh run view 41699253306 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'error CS|AssertionError|assert|FAIL|##\[error\]|Traceback|Exception' | Select-Object -First 14 | ForEach-Object { Write-Output ($_.Line -replace '^\S+\s+\S+\s+','') }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
