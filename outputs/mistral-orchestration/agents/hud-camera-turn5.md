# HUD/product controls — turn-5 report (branch mistral/hud-camera)

Incident: run 41835648940 at f2eba7e (my 5305458 fix was included) — the product_controls gate FAILED at the standings check, second product failure.

## 1. Failing assertion

    standings row not bound to live order: row 0 = CLASSIFICATION

## 2. Root cause

`RaceStandingsPanel.Refresh()` (src/RaceStandingsPanel.cs) prepends a HEADER label (`Text="CLASSIFICATION"`, font 13) as the FIRST child of the `StandingsRows` VBox, then appends the data rows (each named `Car<car.id>`) after it. My fixture iterated `rows.GetChildren()` from index 0, so it mapped the header onto "data row 0" and compared `"CLASSIFICATION"` against the model-derived `"01 <name> L1"` expectation. The PANEL is correct (title + rows is the intended layout); the fixture's indexing was wrong. The failure line proved the fixture's own mislabeling: the text it printed for "row 0" was the header's, which is exactly the header-first layout.

## 3. Fix (commit ec7925f, my file only — src/ProductControlsScenario.cs)

Per the directive — skip the header, assert the data rows by name:

1. First child asserted by content: `kids[0]` is a Label with text `CLASSIFICATION` (catches header moves/renames too).
2. Each DATA row is located BY NAME: `rows.GetNodeOrNull<Label>("Car"+car.id)` per car in `race.Standings()` — no child-index assumptions at all.
3. Each named row's text is pinned to the slot the LIVE standings assigns to that same car: `(rank).ToString("00") + "  " + carName(18-char cap) + "  " + liveLapState`. Order-agnostic view binding: the view is checked against the model per car, not per child position, so a future sort change cannot fail the fixture by indexing.
4. The trailing guard now reads `standings panel rendered no data rows`.

## 4. Forward-vet of the assertions that had not yet executed

| Check | Vetting | Verdict |
|---|---|---|
| `ERS 87%` (0.87f*100) | 0.87f=0.87000000477 → product 87.0000005 → (int) 87 | pass ✓ |
| `RAIN 42%` (race.wetness=0.42f*100) | 0.42f=0.4199999924; true product 41.99999924 is within half-ulp of 42.0f → float product IS 42.0f → (int) 42 | pass ✓ |
| `NIGHT RAIN 42%` | same product, prefix from the lighting profile | pass ✓ |
| `CLEAR` restore | wet=0/wetness=0 → no pct, no night | pass ✓ |
| `KEYS`/`PAD` then `TOUCH` | assertion accepts both ARM states; the TOUCH toggle is exact | pass ✓ |
| ModeMenu/RaceSettingsPanel/SeriesSetupPanel focus + Escape | scene-tree-level GUI focus (headless-compatible); expected owners: first action / `SaveBack` / the series `OptionButton`; commands identical to the `ControlsScenario` key-event precedent | pass expected |
| Standings rect bound | PanelContainer at (12,80), hugs content well inside the canvas | pass ✓ |

Both prior failure classes (turn-4 precision, turn-5 header indexing) are now fixed in the same file; no remaining known landmine — if the next red line appears it will name its assert precisely as these two did.

## 5. State + request

- My branch reconciled to f2eba7e (fast-forward; merged my 99964a4), fix committed on top as ec7925f (src/ProductControlsScenario.cs only, 10+/7-). Working tree clean.
- REQUEST: coordinator re-dispatches product_controls_review at a ref containing ec7925f.
