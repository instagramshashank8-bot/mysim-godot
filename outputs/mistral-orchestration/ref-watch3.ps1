Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run watch 41788844369 -R instagramshashank8-bot/mysim-godot --exit-status --interval 90 2>&1 | Select-Object -Last 3
gh run view 41788844369 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("REF3 " + $_.status + " " + $_.conclusion) }
gh run download 41788844369 -R instagramshashank8-bot/mysim-godot -D outputs/repair-20261029/cloud/reference-evidence-41788844369 2>&1 | Out-Null
$env:MYSIM_INTAKE_KEY=[IO.File]::ReadAllText("C:/Users/shash/.codex/private/mysim-review/review-20261031.key").Trim()
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41788844369 -Recurse -Filter *.enc | ForEach-Object {
  $dest = $_.FullName -replace "\.enc$",""
  python tools/cloud_decrypt.py $_.FullName $dest 2>&1 | Out-Null
}
Remove-Item Env:MYSIM_INTAKE_KEY -ErrorAction SilentlyContinue
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41788844369 -Recurse -Filter *ref-car16*.png | ForEach-Object { Write-Output ("PNG " + $_.Name + " " + $_.Length) }
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41788844369 -Recurse -Filter *ref-lap*.png | ForEach-Object { Write-Output ("PNG " + $_.Name + " " + $_.Length) }
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
