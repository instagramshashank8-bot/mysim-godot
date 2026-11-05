$lines = Get-Content C:/Users/shash/Documents/MySimGodot/src/DriveController.cs
Write-Output "=== 160-186 ==="
for ($i=159; $i -lt 186; $i++) { $l = $lines[$i]; Write-Output ("" + ($i+1) + ": " + $l.Substring(0, [Math]::Min(190, $l.Length))) }
