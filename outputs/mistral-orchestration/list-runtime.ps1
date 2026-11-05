Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== game-runtime-0 contents (evidence pngs live here) ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316/game-runtime-0-attempt-1 -Recurse -File | Select-Object -First 20 -ExpandProperty FullName
