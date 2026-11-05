Set-Location C:/Users/shash/Documents/MySimGodot
Select-String -Path tools/cloud_game_audit.py -Pattern 'see saved logs' | Select-Object -First 3 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(200,$_.Line.Trim().Length))) }
