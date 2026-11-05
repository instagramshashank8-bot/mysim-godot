Select-String -Path C:/Users/shash/Documents/MySimGodot/.github/workflows/game-review.yml -SimpleMatch -Pattern 'settings_persist_review' | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(90,$_.Line.Trim().Length))) }
Write-Output "---ref---"
Select-String -Path C:/Users/shash/Documents/MySimGodot/.github/workflows/game-review.yml -SimpleMatch -Pattern 'reference_evidence' | ForEach-Object { Write-Output ("" + $_.LineNumber + ": " + $_.Line.Trim().Substring(0,[Math]::Min(90,$_.Line.Trim().Length))) }
