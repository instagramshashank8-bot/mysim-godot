Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== review dirs with PNGs? ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316 -Recurse -Filter *.png | Select-Object -First 16 -ExpandProperty FullName
Write-Output "=== runtime artifacts? ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316 -Recurse -Directory | Select-Object -First 12 -ExpandProperty FullName
