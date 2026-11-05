Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== multi-view / closeup / camera-position infra in audit ==="
Select-String -Path tools/cloud_game_audit.py -Pattern 'VIEW','closeup','CAMERA','CAM_','car_shape_review' -CaseSensitive:$false | Select-Object -First 14 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length))) }
Write-Output "=== CarViewVisibility / camera modes in src ==="
Select-String -Path src/CarViewVisibility.cs,src/DriveController.cs -Pattern 'Camera|Transform|View|offset|distance' -CaseSensitive:$false | Select-Object -First 14 | ForEach-Object { Write-Output ("" + $_.Filename + ":" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length))) }
