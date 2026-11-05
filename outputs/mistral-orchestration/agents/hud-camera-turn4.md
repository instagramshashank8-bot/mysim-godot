# HUD/product controls — turn-4 report (branch mistral/hud-camera)

Incident: run 41830853569 at d9940a2 — the product_controls gate FAILED in my ProductControlsScenario at runtime. Compile OK. Both attempts (product-controls-1 and product-controls-2; 0.97 s / 0.87 s; no timeout) failed IDENTICALLY at the same check. Artifact downloaded read-only to `outputs/repair-20261029/prodfail-41830853569/game-runtime-0-attempt-1/` (product-controls-1.log, product-controls-2.log, product-controls-1-process.json/-2-process.json).

## 1. Exact failing assertion (log, both attempts)

    FAIL product controls System.Exception: hud timing not bound to live lap state:
    'POS 1/2   LAP 2/3   LAP TIME 12.3s   BEST 87.520s'

Process JSONs: returncode 1, no timeouts, no engine errors — the scenario reached the timing block and aborted there.

## 2. Root cause — MY assertion used the wrong format precision, the HUD was correct

The HUD formats best-lap with THREE decimals (`BEST " + Player.bestLap.ToString("0.000") + "s"`) — this has been the HUD's best-lap convention since the original design (milliseconds are the lap-timing convention; LAP TIME uses the coarser `0.0`). The rendered `BEST 87.520s` for bestLap=87.52 was CORRECT. My fixture carried the LAP TIME precision over into the BEST check and expected `BEST 87.5s` — an assertion bug, not a HUD bug. Every assertion BEFORE this one passed at runtime (speed/gear incl. reverse, compound+wear, fuel, damage, pit plan NEXT W, the REQUESTED key hint, and the full settings→HUD remap chain to `REQUESTED (O)`), so the HUD↔model binding path itself is proven live.

## 3. Fix (commit 5305458, my file only)

- src/ProductControlsScenario.cs: the timing check now asserts `BEST 87.520s` (the real convention). **No HUD code change — the HUD needed none.**

## 4. Systemic vetting of the assertions that had not yet executed at the failure point

All remaining checks re-derived numerically so a re-run should not red-run on a new precision surprise:

| Check | Value path | Verdict |
|---|---|---|
| `RAIN` after Settings.Set("wet",2) | profile 2 name "Rain" → "RAIN" | pass ✓ |
| `RAIN 42%` after race.wetness=0.42f | 0.42f=0.4199999924; float product 0.42f*100f rounds to exactly 42.0f (true value within half-ulp, ulp(42)=3.81e-6); (int)→42 | pass ✓ |
| `NIGHT RAIN 42%` (tod=3) | same product | pass ✓ |
| `CLEAR` after restore | wet=0, wetness=0 → no pct, no night | pass ✓ |
| `ERS 87%` | 0.87f=0.8700000048 → product 87.0000004768 → (int) 87 | pass ✓ |
| `FUEL 33 L` | (int)33.7 = 33 | pass ✓ |
| `DAMAGE 25%` / `TYRES M 50%` | 0.25 and 0.5 are exact-representable | pass ✓ |
| standings rows | robust to the 18-char name cap (StartsWith rank+cap-name+live lap state) | pass expected |
| focus/escape + bounds | logic checks; headless GUI focus is scene-tree level | pass expected |

One residual unknown: if the cloud headless environment exposes udev input devices that Godot enumerates as joypads, DeviceTag renders `PAD` instead of `KEYS` — the assertion accepts both, and the TOUCH toggle is asserted absolutely, so this cannot red-run.

## 5. Reconciliation + re-run request

- My branch was fast-forwarded to d9940a2: the merged ProductControlsScenario = my turn-3 version + the coordinator's flush-arg compile fix (own commit; the python-ism CS0103 had been caught by 41830517930), and the `MYSIM_PRODUCTCHECKS` host hook is wired in Main.
- The assertion-precision fix is committed as 5305458 (my file only; no HUD code changed; no assertion weakened — the corrected check is stricter in meaning: it now pins the real three-decimal lap timing).
- REQUEST: coordinator re-dispatches the product_controls_review depth at a ref containing 5305458.
