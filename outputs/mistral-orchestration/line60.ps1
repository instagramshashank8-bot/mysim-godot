Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== line 60 at HEAD ==="
git show HEAD:src/SettingsPersistChecks.cs | Select-String -Pattern 'malformed_disk' | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(180,$_.Line.Length)) }
git diff HEAD -- src/SettingsPersistChecks.cs | Select-Object -First 6
