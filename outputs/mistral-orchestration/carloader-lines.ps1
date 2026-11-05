Set-Location C:/Users/shash/Documents/MySimGodot
Write-Output "=== CarLoader rear/livery family material application (lines 95-160) ==="
$lines = Get-Content src/CarLoader.cs
for ($i=95; $i -lt 160; $i++) { if ($i -lt $lines.Count) { $l = $lines[$i]; if ($l.Trim().Length -gt 0) { Write-Output ("" + ($i+1) + ": " + $l.Trim().Substring(0,[Math]::Min(150,$l.Trim().Length))) } } }
