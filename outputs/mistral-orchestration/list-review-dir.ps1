Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== decrypted review dir 0 (full) ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316/encrypted-review-0-attempt-1 -Recurse -File | ForEach-Object { Write-Output $_.FullName.Replace("C:/Users/shash/Documents/MySimGodot/outputs/repair-20261029/cloud/reference-evidence-41700551316/encrypted-review-0-attempt-1/","") }
Write-Output "=== review dir 3 (full) ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316/encrypted-review-3-attempt-1 -Recurse -File | ForEach-Object { Write-Output $_.FullName.Replace("C:/Users/shash/Documents/MySimGodot/outputs/repair-20261029/cloud/reference-evidence-41700551316/encrypted-review-3-attempt-1/","") }
