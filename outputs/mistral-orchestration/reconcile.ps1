Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== HEAD / status ==="
git log -1 --format="%h %s"
git status --short | Select-Object -First 8
Write-Output "=== preserved runtime0 evidence (failed ref run) ==="
Get-ChildItem outputs/repair-20261029/cloud/reference-failed-41712623422-runtime0/outputs/cloud-game -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 14 | ForEach-Object { Write-Output ($_.FullName.Replace((Get-Location).Path + "\outputs\repair-20261029\cloud\reference-failed-41712623422-runtime0\","") + "  " + $_.Length + "B") }
Write-Output "=== run 41712623422 process jsons (attempt timing) ==="
Get-ChildItem outputs/repair-20261029/cloud/settings-persist-r3 -ErrorAction SilentlyContinue | Out-Null
Get-ChildItem "outputs/repair-20261029/cloud" -Directory -Filter "reference-*" | ForEach-Object { Write-Output $_.Name }
