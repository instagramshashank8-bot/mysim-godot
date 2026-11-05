Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== rear assembly: built parts in CarLoader (rear wing/diffuser/struts) ==="
Select-String -Path src/CarLoader.cs -Pattern 'REAR','Wing','Diffuser','Strut','wing' -CaseSensitive:$false | Select-Object -First 16 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(125,$_.Line.Trim().Length))) }
Write-Output "=== F1 suspension visual builder: rear pieces ==="
Select-String -Path src/F1SuspensionVisualBuilder.cs -Pattern 'Rear','rear','Wishbone','attach' -CaseSensitive:$false | Select-Object -First 14 | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(125,$_.Line.Trim().Length))) }
