Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output "=== 41688779865 failed log ==="
gh run view 41688779865 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'error CS|##\[error\]|AssertionError|assert|FAIL' | Select-Object -First 8 | ForEach-Object { Write-Output $_.Line }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
