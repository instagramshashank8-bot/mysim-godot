Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content .github/workflows/game-review.yml
Write-Output ("line76len=" + $lines[75].Length)
Write-Output ("line76head=" + $lines[75].Substring(0,[Math]::Min(360,$lines[75].Length)))
Write-Output "=== reference/drive branch lines in audit ==="
Select-String -Path tools/cloud_game_audit.py -Pattern 'ref-lap','drive_count','non fence render','REFERENCE' | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(110,$_.Line.Trim().Length))) }
