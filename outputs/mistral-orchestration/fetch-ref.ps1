Set-Location C:/Users/shash/Documents/MySimGodot
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
gh run view 41700551316 -R instagramshashank8-bot/mysim-godot --json status,conclusion | ConvertFrom-Json | ForEach-Object { Write-Output ("REF417 " + $_.status + " " + $_.conclusion) }
gh run download 41700551316 -R instagramshashank8-bot/mysim-godot -D outputs/repair-20261029/cloud/reference-evidence-41700551316 2>&1 | Out-Null
Remove-Item Env:GH_TOKEN -ErrorAction SilentlyContinue
$env:MYSIM_INTAKE_KEY=[IO.File]::ReadAllText("C:/Users/shash/.codex/private/mysim-review/review-20261031.key").Trim()
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316 -Recurse -Filter *.enc | ForEach-Object {
  $dest = $_.FullName -replace "\.enc$",""
  python tools/cloud_decrypt.py $_.FullName $dest 2>&1 | Out-Null
}
Remove-Item Env:MYSIM_INTAKE_KEY -ErrorAction SilentlyContinue
Get-ChildItem outputs/repair-20261029/cloud/reference-evidence-41700551316 -Recurse -Filter *.png | Select-Object -First 14 -ExpandProperty FullName
