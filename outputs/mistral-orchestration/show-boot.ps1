Set-Location C:/Users/shash/Documents/MySimGodot
git show 91e6991:tools/cloud_game_audit.py | Select-String -SimpleMatch -Pattern "def scenario_boot","scenario_boot\(" | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line.Trim().Substring(0,[Math]::Min(160,$_.Line.Trim().Length)) }
