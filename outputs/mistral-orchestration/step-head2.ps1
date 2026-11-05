Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content .github/workflows/game-review.yml
for ($i=196; $i -lt 213; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Substring(0,[Math]::Min(150,$lines[$i].Length))) }
