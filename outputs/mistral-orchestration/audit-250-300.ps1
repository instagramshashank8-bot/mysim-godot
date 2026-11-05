Set-Location C:/Users/shash/Documents/MySimGodot
$lines = git show 91e6991:tools/cloud_game_audit.py
for ($i=252; $i -lt 300; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Substring(0,[Math]::Min(165,$lines[$i].Length))) }
