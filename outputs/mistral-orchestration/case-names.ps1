Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== current fixture corrupt/malformed case names ==="
Select-String -Path src/SettingsPersistChecks.cs -Pattern 'Check\("' | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(110,$_.Line.Trim().Length))) }
