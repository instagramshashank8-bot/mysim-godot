Set-Location C:/Users/shash/Documents/MySimGodot
git show 91e6991:.github/workflows/game-review.yml | Select-String -Pattern 'SETTINGS_PERSIST|REFERENCE' | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(180,$_.Line.Length)) }
