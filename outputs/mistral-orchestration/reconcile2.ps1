Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output ("HEAD=" + (git log -1 --format="%h %s"))
Write-Output "=== dirty ==="
git status --short | Select-Object -First 10
Write-Output "=== live runs ==="
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run list -R instagramshashank8-bot/mysim-godot -w game-review.yml --limit 3 --json databaseId,status,conclusion,headSha | ConvertFrom-Json | ForEach-Object { Write-Output ("RUN " + $_.databaseId + " " + $_.status + " " + $_.conclusion + " " + $_.headSha.Substring(0,7)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
