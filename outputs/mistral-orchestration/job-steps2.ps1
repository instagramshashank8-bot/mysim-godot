Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$steps = gh api repos/instagramshashank8-bot/mysim-godot/actions/runs/41700551316/jobs --jq '.jobs[0].steps[] | .name + " => " + .conclusion'
$steps
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
