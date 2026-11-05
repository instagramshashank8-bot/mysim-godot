Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== full runtime-0 tree (all files) ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316/game-runtime-0-attempt-1 -Recurse -File | ForEach-Object { Write-Output $_.FullName.Replace("C:/Users/shash/Documents/MySimGodot/outputs/repair-20261029/cloud/reference-evidence-41700551316/game-runtime-0-attempt-1/","") }
Write-Output "=== runtime-1 same ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316/game-runtime-1-attempt-1 -Recurse -File | ForEach-Object { Write-Output $_.Name }
