Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$jobs = gh api "repos/instagramshashank8-bot/mysim-godot/actions/runs/41700551316/jobs" | ConvertFrom-Json
$render = $jobs.jobs | Where-Object { $_.name -like "render (1)" } | Select-Object -First 1
Write-Output ("JOBID=" + $render.id + " run=" + $render.run_id)
$log = gh api "repos/instagramshashank8-bot/mysim-godot/actions/jobs/$($render.id)/logs" -H "Accept: application/vnd.github.raw+json" -H "X-GitHub-Api-Version: 2022-11-28" > $env:TEMP/rawjob.txt 2>&1
Write-Output ("RAWBYTES=" + (Get-Item $env:TEMP/rawjob.txt).Length)
$raw = Get-Content $env:TEMP/rawjob.txt -ErrorAction SilentlyContinue
Write-Output ("count_settings=" + (($raw | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW").Count))
($raw | Select-String -SimpleMatch "REFERENCE_EVIDENCE" | Select-Object -First 2) | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
