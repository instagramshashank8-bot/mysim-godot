Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== GroundFixer / material colors (find albedo/green) ==="
Select-String -Path src/GroundFixer.cs -Pattern 'Color|albedo|green|texture|shader' -CaseSensitive:$false | Select-Object -First 15 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(130,$_.Line.Trim().Length))) }
Write-Output "=== search whole src for grass/garish constants ==="
Get-ChildItem src -Filter *.cs -Recurse | Select-String -Pattern 'GrassColor|grass_|mat_grass|BrightGreen|0\.[2-9]f,\s*0\.[89]f,\s*0\.[2-5]f' -CaseSensitive:$false | Select-Object -First 12 | ForEach-Object { Write-Output ("" + $_.Filename + ":" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length))) }
