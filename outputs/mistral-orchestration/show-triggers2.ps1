Set-Location C:/Users/shash/Documents/MySimGodot
git show 91e6991:tools/cloud_game_audit.py | Select-String -SimpleMatch -Pattern "environ.get" | Select-Object -First 14 | ForEach-Object { Write-Output $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length)) }
