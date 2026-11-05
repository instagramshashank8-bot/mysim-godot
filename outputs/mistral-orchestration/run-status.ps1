Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output ("LOCAL_HEAD=" + (git log -1 --format=%h) + " branch=" + (git rev-parse --abbrev-ref HEAD))
Write-Output ("REMOTE_HEAD=" + (git ls-remote o_instagramshashank8-bot codex/repair-20261029).Substring(0,7))
Write-Output "=== last 4 workflows ==="
$runs = gh run list -R instagramshashank8-bot/mysim-godot -w game-review.yml --limit 4 --json databaseId,status,conclusion,headSha | ConvertFrom-Json
foreach ($r in $runs) { Write-Output ("RUN " + $r.databaseId + " " + $r.status + " " + $r.conclusion + " " + $r.headSha.Substring(0,7)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
