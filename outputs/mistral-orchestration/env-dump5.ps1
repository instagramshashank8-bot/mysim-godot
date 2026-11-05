Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$raw = & gh api "repos/instagramshashank8-bot/mysim-godot/actions/jobs/126142931300/logs" -H "Accept: application/vnd.github.raw+json" | Out-String
[System.IO.File]::WriteAllText("$env:TEMP/joblog.txt", $raw)
Write-Output ("JOBLOG_BYTES=" + (Get-Item "$env:TEMP/joblog.txt").Length)
$txt = Get-Content $env:TEMP/joblog.txt
Write-Output ("HITS_SETTINGS=" + ($txt | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW" | Measure-Object).Count)
Write-Output ("HITS_REF=" + ($txt | Select-String -SimpleMatch "REFERENCE_EVIDENCE" | Measure-Object).Count)
if (($txt | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW").Count -gt 0) {
  ($txt | Select-String -SimpleMatch "SETTINGS_PERSIST_REVIEW" | Select-Object -First 3) | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(200,$_.Line.Length)) }
}
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
