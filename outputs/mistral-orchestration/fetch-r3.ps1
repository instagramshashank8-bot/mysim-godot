Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41707159183 -R instagramshashank8-bot/mysim-godot --json jobs --jq '.jobs[] | .name + " " + .conclusion'
gh run download 41707159183 -R instagramshashank8-bot/mysim-godot -D outputs/repair-20261029/cloud/settings-persist-r3 2>&1 | Out-Null
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
Get-ChildItem -Recurse outputs/repair-20261029/cloud/settings-persist-r3 -Filter 'settings-*' -File | Select-Object -First 10 -ExpandProperty FullName
