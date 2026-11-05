Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41707159183 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'AssertionError|missingCases|failures' | Select-Object -First 3 | ForEach-Object { Write-Output $_.Line.Substring([Math]::Max(0,$_.Line.Length-1500)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
