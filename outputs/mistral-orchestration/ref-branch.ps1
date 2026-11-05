Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== reference branch at d5d42ce ==="
git show d5d42ce:tools/cloud_game_audit.py | Select-String -SimpleMatch -Pattern "ref-lap","REFERENCE_EVIDENCE","REFERENCE_EVIDENCE_RUN" | Select-Object -First 10 | ForEach-Object { Write-Output $_.Line.Trim().Substring(0,[Math]::Min(150,$_.Line.Trim().Length)) }
Write-Output "=== current tree diff vs d5d42ce for audit py ==="
git diff d5d42ce HEAD -- tools/cloud_game_audit.py | Select-Object -First 30
