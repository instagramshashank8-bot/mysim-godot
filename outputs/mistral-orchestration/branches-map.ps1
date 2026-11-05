Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== which branches print COMPLETE (committed HEAD) ==="
Select-String -Path tools/cloud_game_audit.py -Pattern 'COMPLETE|sys.exit\(' | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(100,$_.Line.Trim().Length))) }
