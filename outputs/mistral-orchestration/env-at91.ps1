Set-Location C:/Users/shash/Documents/MySimGodot
$lines = git show 91e6991:.github/workflows/game-review.yml
for ($i=200; $i -lt 206; $i++) { Write-Output ("" + ($i+1) + ": " + $lines[$i].Substring(0,[Math]::Min(200,$lines[$i].Length))) }
