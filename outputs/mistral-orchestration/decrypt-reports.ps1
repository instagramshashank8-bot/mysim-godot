Set-Location C:/Users/shash/Documents/MySimGodot
$env:MYSIM_INTAKE_KEY=[IO.File]::ReadAllText("C:/Users/shash/.codex/private/mysim-review/review-20261031.key").Trim()
$files = Get-ChildItem outputs/repair-20261029/cloud/settings-persist-result -Recurse -Filter *.enc
foreach ($f in $files) {
  $dest = $f.FullName -replace "\.enc$",""
  python tools/cloud_decrypt.py $f.FullName $dest 2>&1 | Out-Null
  Write-Output ("DEC " + $f.Name + " rc=" + $LASTEXITCODE)
}
Remove-Item Env:MYSIM_INTAKE_KEY -ErrorAction SilentlyContinue
Get-ChildItem outputs/repair-20261029/cloud/settings-persist-result -Recurse -Include *.json,*.txt -File | Select-Object -First 12 -ExpandProperty FullName
