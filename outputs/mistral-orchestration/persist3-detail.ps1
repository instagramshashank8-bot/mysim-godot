Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output "=== 41707159183 failure detail (the malformed scenario only) ==="
gh run view 41707159183 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'malformed|corrupt_file_intact' | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line.Substring([Math]::Max(0,$_.Line.Length-1100)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
