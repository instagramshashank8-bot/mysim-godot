Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$log = gh run view 41700551316 -R instagramshashank8-bot/mysim-godot --job $(gh api "repos/instagramshashank8-bot/mysim-godot/actions/runs/41700551316/jobs" | ConvertFrom-Json).jobs | Where-Object { $_.name -like "render*" } | Select-Object -First 1 -ExpandProperty id
Write-Output ("JOBID=" + $log)
$full = gh api "repos/instagramshashank8-bot/mysim-godot/actions/jobs/$log/logs" 2>$null
$full | Select-String -SimpleMatch -Pattern "SETTINGS_PERSIST_REVIEW","REFERENCE_EVIDENCE","MYSIM_SETTINGS_PERSIST" | Select-Object -First 8 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(160,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
