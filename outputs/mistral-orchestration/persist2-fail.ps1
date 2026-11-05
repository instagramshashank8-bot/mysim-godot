Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output "=== PERSIST2 failed log (contract/assert lines) ==="
gh run view 41703220126 -R instagramshashank8-bot/mysim-godot --log-failed 2>&1 | Select-String -Pattern 'fixture FAILED|AssertionError|SETTINGSPERSIST|missingCases|exit' | Select-Object -First 8 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(900,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
