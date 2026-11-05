Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content tools/cloud_game_audit.py
for ($i=19; $i -lt 34; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Trim().Substring(0,[Math]::Min(170,$lines[$i].Trim().Length))) }
