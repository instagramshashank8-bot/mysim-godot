Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
Write-Output "=== fixture run latest log tail (any scenario errors) ==="
gh run view 41699253306 -R instagramshashank8-bot/mysim-godot --log 2>$null | Select-String -Pattern 'SETTINGSPERSIST|fixture FAILED|COMPLETE settings|error CS|SUMMARY' | Select-Object -First 14 | ForEach-Object { Write-Output $_.Line }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
