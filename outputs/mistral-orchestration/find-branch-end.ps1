Select-String -Path C:/Users/shash/Documents/MySimGodot/tools/cloud_game_audit.py -Pattern 'CAR_SHAPE_REVIEW','PANEL','previe','PREVIEW' | Select-Object -First 10 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(130,$_.Line.Trim().Length))) }
Write-Output "=== tail ==="
Get-Content C:/Users/shash/Documents/MySimGodot/tools/cloud_game_audit.py | Select-Object -Last 4
