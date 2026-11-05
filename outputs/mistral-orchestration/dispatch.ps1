Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output ("HEAD=" + (git log -1 --format=%h))
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
git -c credential.helper="!gh auth git-credential" push o_instagramshashank8-bot codex/repair-20261029 2>&1 | Select-Object -Last 1
gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref codex/repair-20261029 -f depth=settings_persist_review
Start-Sleep -Seconds 12
$runs = gh run list -R instagramshashank8-bot/mysim-godot -w game-review.yml --limit 2 --json databaseId,status,headSha | ConvertFrom-Json
foreach ($r in $runs) { Write-Output ("RUN " + $r.databaseId + " " + $r.status + " " + $r.headSha.Substring(0,7)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
