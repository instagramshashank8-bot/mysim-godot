Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41712623422 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'AssertionError|Error:|Traceback|missing|##\[error\]' | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(400,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
