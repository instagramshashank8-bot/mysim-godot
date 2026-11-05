Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh api "repos/instagramshashank8-bot/mysim-godot/actions/jobs/126142931300/logs" > $env:TEMP/joblog.txt
$txt = Get-Content $env:TEMP/joblog.txt
$txt | Select-String -SimpleMatch -Pattern "SETTINGS_PERSIST_REVIEW","REFERENCE_EVIDENCE" | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(170,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
