Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== grass / road / foliage color constants ==="
Select-String -Path src/GroundFixer.cs,src/UnityMaterialBinder.cs,src/TrackMaterialBinder.cs,src/Conditions.cs,src/SkyDome.cs -Pattern 'new Color\(0?\.[0-9]+f?,\s*0?\.[0-9]+f?,\s*0?\.[0-9]+f?' | Select-Object -First 24 | ForEach-Object { Write-Output ("" + $_.Filename + ":" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length))) }
