Set-Location C:/Users/shash/Documents/MySimGodot
$env:MYSIM_INTAKE_KEY=[IO.File]::ReadAllText("C:/Users/shash/.codex/private/mysim-review/review-20261031.key").Trim()
Write-Output "=== sizes of reference envelopes ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316 -Recurse -Filter *.enc | ForEach-Object { Write-Output ("" + $_.Name + " " + $_.Length) }
Write-Output "=== verbose decrypt of shard 0 ==="
python tools/cloud_decrypt.py outputs/repair-20261029/cloud/reference-evidence-41700551316/encrypted-review-0-attempt-1/game-review-41700551316-1-0.enc outputs/repair-20261029/cloud/reference-evidence-41700551316/verbose-0
Write-Output "=== contents after verbose decrypt ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316/verbose-0 -Recurse -File | ForEach-Object { Write-Output ($_.FullName.Replace("C:/Users/shash/Documents/MySimGodot/outputs/repair-20261029/cloud/reference-evidence-41700551316/verbose-0/","") + " " + $_.Length) }
Remove-Item Env:MYSIM_INTAKE_KEY -ErrorAction SilentlyContinue
