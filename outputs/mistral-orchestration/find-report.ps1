Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== settings-named files ==="
Get-ChildItem outputs/repair-20261029/cloud/settings-persist-result -Recurse -File | Where-Object { $_.Name -match 'settings|persist' } | Select-Object -First 12 -ExpandProperty FullName
Write-Output "=== SETTINGSPERSIST in logs ==="
Get-ChildItem outputs/repair-20261029/cloud/settings-persist-result -Recurse -Include *.log,*.txt -File | Select-String -Pattern 'SETTINGSPERSIST' -List | Select-Object -First 6 | ForEach-Object { Write-Output $_.Path; Write-Output $_.Line }
Write-Output "=== runtime-0 outputs ==="
Get-ChildItem outputs/repair-20261029/cloud/settings-persist-result/game-runtime-0-attempt-1 -Recurse -File | Select-Object -First 15 -ExpandProperty FullName
