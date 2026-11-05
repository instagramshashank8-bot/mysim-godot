Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== ALL settings-branch triggers in the committed audit (91e6991 and HEAD) ==="
git show 91e6991:tools/cloud_game_audit.py | Select-String -SimpleMatch -Pattern "os.environ.get" | Select-String -SimpleMatch -Pattern "SETTINGS","REFERENCE","FENCE","IDENTITY" | Select-Object -First 10 | ForEach-Object { Write-Output $_.Line.Trim().Substring(0,[Math]::Min(120,$_.Line.Trim().Length)) }
