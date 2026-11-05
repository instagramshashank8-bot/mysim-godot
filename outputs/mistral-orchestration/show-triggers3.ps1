Set-Location C:/Users/shash/Documents/MySimGodot
git show 91e6991:tools/cloud_game_audit.py | Select-String -SimpleMatch -Pattern "SETTINGS_PERSIST","REFERENCE" | Select-Object -First 8 | ForEach-Object { Write-Output $_.Line.Trim().Substring(0,[Math]::Min(140,$_.Line.Trim().Length)) }
