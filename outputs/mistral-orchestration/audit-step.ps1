Set-Location C:/Users/shash/Documents/MySimGodot
$lines = Get-Content .github/workflows/game-review.yml
for ($i=268; $i -lt 290; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i]) }
