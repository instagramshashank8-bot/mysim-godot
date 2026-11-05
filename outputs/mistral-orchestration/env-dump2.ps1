Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$jobs = gh api "repos/instagramshashank8-bot/mysim-godot/actions/runs/41700551316/jobs" | ConvertFrom-Json
$render = $jobs.jobs | Where-Object { $_.name -eq "render (1)" } | Select-Object -First 1
Write-Output ("JOBID=" + $render.id)
$txt = gh api "repos/instagramshashank8-bot/mysim-godot/actions/jobs/$($render.id)/logs"
$txt | Select-String -SimpleMatch -Pattern "SETTINGS_PERSIST_REVIEW","REFERENCE_EVIDENCE" | Select-Object -First 6 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(160,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
