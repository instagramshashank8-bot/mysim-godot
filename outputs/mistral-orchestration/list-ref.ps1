Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== downloaded reference evidence tree ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316 -Recurse -File | Select-Object -First 16 -ExpandProperty FullName
