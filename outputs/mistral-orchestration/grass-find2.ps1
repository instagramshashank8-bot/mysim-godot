Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== how the ground quad gets its color ==="
Get-ChildItem src -Filter *.cs -Recurse | Select-String -Pattern 'AlbedoColor','PlaneMesh','GroundPlane','Color\(0\.' | Select-Object -First 20 | ForEach-Object { Write-Output ("" + $_.Filename + ":" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(130,$_.Line.Trim().Length))) }
Write-Output "=== GroundFixer main body (first 30 lines) ==="
Get-Content src/GroundFixer.cs -TotalCount 30 | ForEach-Object { Write-Output $_ }
