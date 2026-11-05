Set-Location C:/Users/grant/Documents/MySimGodot 2>$null; Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== Hud.cs panel construction (top bar / standings / speed) ==="
Select-String -Path src/Hud.cs -Pattern 'Panel|PanelContainer|MarginContainer|Anchor|OffsetTop|OffsetBottom|CustomMinimumSize|Position|Size = new Vector2' | Select-Object -First 25 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length))) }
Write-Output "=== road/grass colors ==="
Select-String -Path src/TrackMaterialBinder.cs,src/DriveSurfaceLoader.cs -Pattern 'Color\(|AlbedoColor|Grass|grass|road' -CaseSensitive:$false | Select-Object -First 20 | ForEach-Object { Write-Output ("" + $_.Filename + ":" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(110,$_.Line.Trim().Length))) }
