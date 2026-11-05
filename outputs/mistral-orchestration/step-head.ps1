Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content .github/workflows/game-review.yml
Write-Output "=== the render/audit step header (search the if for the audit run) ==="
for ($i=214; $i -lt 245; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Substring(0,[Math]::Min(150,$lines[$i].Length))) }
