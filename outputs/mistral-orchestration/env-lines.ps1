Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content .github/workflows/game-review.yml
for ($i=202; $i -lt 208; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Substring(0,[Math]::Min(240,$lines[$i].Length))) }
