Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$runs = gh run list -R instagramshashank8-bot/mysim-godot -w game-review.yml --limit 3 --json databaseId,status,conclusion,headSha | ConvertFrom-Json
foreach ($r in $runs) { Write-Output ("RUN " + $r.databaseId + " " + $r.status + " " + $r.conclusion + " " + $r.headSha.Substring(0,7)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
