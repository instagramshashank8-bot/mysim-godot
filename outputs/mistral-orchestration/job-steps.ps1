Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41700551316 -R instagramshashank8-bot/mysim-godot --json jobs --jq '.jobs[0].steps[] | .name + " => " + .conclusion'
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
