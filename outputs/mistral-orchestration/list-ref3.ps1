Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== REFERENCE EVIDENCE PNGs (run 41788844369) ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41788844369 -Recurse -Filter *.png | ForEach-Object { Write-Output ($_.Name + " " + $_.Length) }
Write-Output "=== COMPLETE line evidence ==="
Select-String -Path outputs/repair-20261029/cloud/reference-evidence-41788844369/encrypted-review-0-attempt-1/review/* -Pattern 'reference evidence' -ErrorAction SilentlyContinue | Select-Object -First 2 | ForEach-Object { Write-Output $_.Line }
