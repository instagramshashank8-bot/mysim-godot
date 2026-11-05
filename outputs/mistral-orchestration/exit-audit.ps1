Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== all indent-0 print/sys.exit lines (should be only TRUE mode-end exits) ==="
Select-String -Path tools/cloud_game_audit.py -Pattern '^print\(' | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(110,$_.Line.Trim().Length))) }
python -m py_compile tools/cloud_game_audit.py
if ($LASTEXITCODE -eq 0) { Write-Output "PY-OK" } else { Write-Output "PY-FAIL" }
