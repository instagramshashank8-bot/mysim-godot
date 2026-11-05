Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$log = gh run view 41700551316 -R instagramshashank8-bot/mysim-godot --log 2>$null
Write-Output ("LOG_LINES=" + ($log | Measure-Object).Count)
$log | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW:" | Select-Object -First 3 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(200,$_.Line.Length)) }
$log | Select-String -SimpleMatch "REFERENCE_EVIDENCE:" | Select-Object -First 3 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(200,$_.Line.Length)) }
$log | Select-String -SimpleMatch "COMPLETE settings persistence" | Select-Object -First 2 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(160,$_.Line.Length)) }
$log | Select-String -SimpleMatch "COMPLETE reference evidence" | Select-Object -First 2 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(160,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
