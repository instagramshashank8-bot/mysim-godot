Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content tools/cloud_game_audit.py
for ($i=42; $i -lt 58; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Trim().Substring(0,[Math]::Min(160,$lines[$i].Trim().Length))) }
