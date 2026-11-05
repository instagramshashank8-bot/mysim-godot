Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$jobs = gh api "repos/instagramshashank8-bot/mysim-godot/actions/runs/41700551316/jobs" | ConvertFrom-Json
foreach ($j in $jobs.jobs) {
  if ($j.name -like "render*") {
    Write-Output ("JOB " + $j.name + " " + $j.conclusion)
    foreach ($s in $j.steps) { Write-Output ("STEP " + $s.number + " [" + $s.name + "] => " + $s.conclusion) }
  }
}
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
