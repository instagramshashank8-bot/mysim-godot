Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== binding audit jsons (fence runs) ==="
Get-ChildItem outputs/repair-20261029/cloud -Recurse -Filter 'bindings-*.json' -ErrorAction SilentlyContinue | Select-Object -First 10 | ForEach-Object { Write-Output $_.FullName.Replace((Get-Location).Path + "\outputs\repair-20261029\cloud\","") }
Write-Output "=== cutout jsons ==="
Get-ChildItem outputs/repair-20261029/cloud -Recurse -Filter 'cutout-*.json' -ErrorAction SilentlyContinue | Select-Object -First 10 | ForEach-Object { Write-Output $_.FullName.Replace((Get-Location).Path + "\outputs\repair-20261029\cloud\","") }
