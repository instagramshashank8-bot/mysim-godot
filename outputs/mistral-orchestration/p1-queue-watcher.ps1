param()
$ErrorActionPreference='Continue'
$root='C:/Users/shash/Documents/MySimGodot'
Set-Location $root
$env:GH_TOKEN=(gh auth token -u instagramshashank8-bot).Trim()
$repo='instagramshashank8-bot/mysim-godot'
$queue = @(
  @{ ref='mistral/p1-badge-baseline-rebase'; depth='car_evidence';      inputs=@{cars='16'};  tag='P1-LEG-A badges-candidates-removed' },
  @{ ref='codex/repair-20261029';                  depth='car_evidence';      inputs=@{cars='16'};  tag='P1-LEG-B badges-candidates-active (same code lineage)' },
  @{ ref='codex/repair-20261029';    depth='native_geometry_recovery'; inputs=@{cars='16';physics_backend='jolt';physics_hz='240';rotational_tyres='true'}; tag='RIDE-A baseline (clean-lap config jolt/240/rotational)' },
  @{ ref='codex/repair-20261029';    depth='native_geometry_recovery'; inputs=@{cars='16';physics_backend='jolt';physics_hz='240';rotational_tyres='true'}; tag='RIDE-B ride-height candidate'; extra=@{native_ride_height='true'} }
)
$stateFile="$root/outputs/mistral-orchestration/p1queue-state.json"
$idx = 0
if (Test-Path $stateFile) { $idx = (Get-Content $stateFile -Raw | ConvertFrom-Json).next }
$total = $queue.Count
while ($idx -lt $total) {
  $runs = gh run list -R $repo --limit 12 --json status | ConvertFrom-Json
  $inflight = ($runs | Where-Object { $_.status -in 'in_progress','queued','pending','waiting' }).Count
  if ($inflight -lt 2) {
    $item = $queue[$idx]
    $args = @('workflow','run','game-review.yml','-R',$repo,'--ref',$item.ref,'-f',"depth=$($item.depth)")
    foreach ($k in $item.inputs.Keys) { $args += @('-f',"$k=$($item.inputs[$k])") }
    if ($item.extra) { foreach ($k in $item.extra.Keys) { $args += @('-f',"$k=$($item.extra[$k])") } }
    gh @args 2>&1 | Out-Null
    Start-Sleep -Seconds 15
    $run = gh run list -R $repo --limit 1 --json databaseId,status | ConvertFrom-Json
    $id = $run[0].databaseId
    Add-Content "$root/outputs/mistral-orchestration/p1queue-log.txt" ("dispatched idx=$idx id=$id tag=$($item.tag)")
    $idx++
    Set-Content $stateFile (ConvertTo-Json @{ next=$idx })
  } else { Start-Sleep -Seconds 60 }
}
Add-Content "$root/outputs/mistral-orchestration/p1queue-log.txt" 'QUEUE COMPLETE'
