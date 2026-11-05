Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$log = gh run view 41785711764 -R instagramshashank8-bot/mysim-godot --log 2>$null
Write-Output ("LOG_LINES=" + ($log | Measure-Object).Count)
$log | Select-String -SimpleMatch "COMPLETE" | Select-Object -First 8 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(180,$_.Line.Length)) }
$log | Select-String -SimpleMatch "ref-car16" | Select-Object -First 4 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(180,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
