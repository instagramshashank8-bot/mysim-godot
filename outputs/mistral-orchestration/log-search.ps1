Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$logs = gh run view 41700551316 -R instagramshashank8-bot/mysim-godot --log 2>$null
$logs | Select-String -Pattern 'COMPLETE reference|ref-car16|reference evidence|COMPLETE ' | Select-Object -First 12 | ForEach-Object { Write-Output $_.Line.Substring(0,[Math]::Min(220,$_.Line.Length)) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
