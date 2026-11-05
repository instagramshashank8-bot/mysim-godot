Set-Location C:/Users/shash/Documents/MySimGodot
# Frozen report update + evidence-commit
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$runs = gh run list -R instagramshashank8-bot/mysim-godot -w game-review.yml --limit 5 --json databaseId,status,conclusion,headSha | ConvertFrom-Json
foreach ($r in $runs) { Write-Output ("RUN " + $r.databaseId + " " + $r.status + " " + $r.conclusion + " " + $r.headSha.Substring(0,7)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
Write-Output "=== evidence listing (both ref runs + persist pass) ==="
Write-Output ("persist-pass summary exists: " + (Test-Path outputs/repair-20261029/cloud/settings-persist-pass/game-runtime-0-attempt-1/outputs/cloud-game/settings-persist-summary.json))
Write-Output ("ref evidence png count: " + (Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41788844369 -Recurse -Filter *.png | Measure-Object).Count)
