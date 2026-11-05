Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$jobs = gh api "repos/instagramshashank8-bot/mysim-godot/actions/runs/41700551316/jobs" | ConvertFrom-Json
$render = $jobs.jobs | Where-Object { $_.name -like "render (1)" } | Select-Object -First 1
#Redirect the redirect location for the REAL log:
$logUrl = gh api "repos/instagramshashank8-bot/mysim-godot/actions/jobs/$($render.id)/logs" --include 2>&1 | Select-String -Pattern 'location:' | ForEach-Object { $_.ToString().Split(' ')[-1] }
Write-Output ("LOGURL=" + $logUrl)
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
Invoke-WebRequest -Uri $logUrl -Headers @{ Authorization = "token $env:GH_TOKEN" } -OutFile $env:TEMP\joblog2.txt
Write-Output ("LOGBYTES=" + (Get-Item $env:TEMP\joblog2.txt).Length)
$txt = Get-Content $env:TEMP\joblog2.txt
Write-Output ("HITS=" + ($txt | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW").Count)
($txt | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW","REFERENCE_EVIDENCE" | Select-Object -First 4) | ForEach-Object { $l=$_.Line; Write-Output $l.Substring(0,[Math]::Min(200,$l.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
