Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== reference run LOG: shot lines ==="
Select-String -Path outputs/repair-20261029/cloud/reference-evidence-41700551316/game-runtime-*-attempt-1/import.log -Pattern 'COMPLETE reference|ref-car16|ref-lap' | Select-Object -First 12 | ForEach-Object { Write-Output ($_.Filename + ": " + $_.Line.Substring(0,[Math]::Min(150,$_.Line.Length))) }
