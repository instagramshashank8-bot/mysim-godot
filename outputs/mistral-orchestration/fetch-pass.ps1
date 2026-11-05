Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run download 41710482854 -R instagramshashank8-bot/mysim-godot -D outputs/repair-20261029/cloud/settings-persist-pass 2>&1 | Out-Null
$sum = Get-ChildItem outputs/repair-20261029/cloud/settings-persist-pass -Recurse -Filter 'settings-persist-summary.json' | Select-Object -First 1
if ($sum) { Get-Content $sum.FullName }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
