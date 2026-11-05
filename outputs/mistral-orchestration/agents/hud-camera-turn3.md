# HUD / product-controls - turn 3 report (branch mistral/hud-camera)

Base: canonical 2862b04 merged into my branch (clean; no conflicts - canonical had not touched my owned files since 076363b; the merge commit is efcc3ff). My turn-2 commits bf5b598+f2b786d are NOT yet in the canonical tree; this turn builds on them in my branch as instructed.

## 1. This turn's commits (owned files + one new owned file)

- **bf5b598 (turn-2, kept as base):** HUD hierarchy redesign - top strip, standings grouping, speed/gear chip, minimap left rail, ERS baseline.
- **4065cfb (turn 3):** product-controls completion on top:
   - **Real-data HUD additions (src/Hud.cs):**
     - `weatherTag` in the strip (center-right slot): bound to live state only - profile name from `Conditions.WetNames[Conditions.Wet]` (checked-in weather_profiles.json: Clear/Overcast/Rain/Heavy rain/Fog), live surface-water percent from `Race.wetness` (the actual value driven by `Conditions.Advance` each physics frame), `NIGHT` prefix from `Conditions.IsNight`. Displays like `CLEAR`/`OVERCAST`/`RAIN 42%`/`NIGHT RAIN 42%`.
     - `deviceTag` in the strip (right of weather): the actual input modality - `PAD` when a gamepad is connected, else `TOUCH` when screen controls are enabled, else `KEYS`. No fabricated state.
     - Compound in carState: `TYRES M 50%` (single-letter tag derived from the real `Compound` enum value, S/M/H/W) plus ` NEXT W` from `Player.nextCompound` when a pit is requested with a change - so the pit plan appears from the live race state, not a canned string.
   - **Public read-only label accessors on Hud** (SpeedLabel/GearLabel/InfoLabel/CarStateLabel/WeatherLabel/DeviceLabel/StandingsPanel) - binding checks read the rendered text; no setters, no text injection path.
   - **src/SeriesSetupPanel.cs:** keyboard entry fix - the panel now grabs focus on the track `OptionButton` when opened (was: no focus owner, keyboard/controller users could not navigate it without a mouse).
   - **src/ProductControlsScenario.cs (NEW owned file):** the interaction-check fixture - plain Node like ControlsScenario, self-contained (real Race, real Hud, real panels; no simulated display values: every expectation is an independent literal or re-derived from the race model). Assertions:
     1. speed/gear text follows live state across two literal states (11.6 m/s -> "41 km/h"; 0 m/s -> "0 km/h"); reverse -> "GEAR R".
     2. compound/wear/fuel/damage: "TYRES M 50%", "FUEL 33 L", "DAMAGE 25%".
     3. pit plan: "TYRES M 50% NEXT W" when nextCompound=Wet + pitRequested; "REQUESTED (<key>)" from the live binding.
     4. settings->hud remap chain: `key_5` -> O makes the HUD hint show "REQUESTED (O)" next frame (covers the 9ff443c long-typed Get fix through the product surface); binding restored after.
     5. weather: RAIN -> RAIN 42% (live wetness) -> NIGHT RAIN 42% -> back to CLEAR (settings restored after).
     6. device: KEYS/PAD/TOUCH per real configuration (toggled and restored).
     7. session state: LAP 1/3 -> 2/3, LAP TIME 0.0s -> 12.3s, BEST -- -> 87.5s, FINISHED 3 laps; pause text "PAUSED  (Esc / Tab to resume)" via the real Hud.Paused flag; POS n/2 present in a two-car race.
     8. classification rows follow `Race.Standings()` order re-derived from the model (rank prefix 01 + live name with the 18-char cap + live lap state); at least one row rendered.
     9. ERS display: "ERS 87%" from live ersCharge; hybrid visibility tracks the car model (F1 shows, non-hybrid hides).
     10. Focus: ModeMenu opens with keyboard focus on its first action; RaceSettingsPanel focus = SAVE & BACK and Escape closes; SeriesSetupPanel focus = track choice and Escape closes.
     11. Left-rail rect sanity at the fixture canvas (deep per-resolution bounds remains the cloud HUD_RECTS gates' job).
     - Failure mode: any failed Check -> `FAIL product controls ...` + exit 1 (ControlsScenario pattern); all-exact PASS line for gate greps.
3. Wiring proposals (coordinator-owned files; NOT edited by me):
   - src/Main.cs, near the other scenario hooks:
     `if(System.Environment.GetEnvironmentVariable("MYSIM_PRODUCTCHECKS")!=null){AddChild(new ProductControlsScenario());return;}`
   - game-review.yml: new depth choice `product_controls_review` mapping to env `MYSIM_PRODUCTCHECKS: 1`, shard `[0]` (like other single-shard review depths).

## 2. Functional-interaction checklist for the cloud gates

| Gate (env / depth) | Scenario file | What it proves (behaviour asserts, not screenshots) |
|---|---|---|
| `MYSIM_CONTROLCHECKS` (existing depth `controls_gameplay`) | ControlsScenario | remap: store/conflicts/reserved-keys(Tab,arrows)/cancel/restore defaults; `Held()` reads the rebound key and the window-focus guard; touch: throttle edge, simultaneous steer release, pit-open; pause clears pedals; ERS auto/manual, hold-release, pause clears overtake, non-hybrid hides; exit leaves no touch residue. PASS lines: "PASS mirrored wheel bases preserved...", "PASS actual ERS GUI mode...", "PASS screen controls throttle/release...", "PASS controls dialog rebinding..." |
| NEW `MYSIM_PRODUCTCHECKS` (proposed depth `product_controls_review`) | ProductControlsScenario | HUD text = live model state for speed/gear/reverse, compound+wear, fuel, damage, pit plan (mapped compound + requested + remapped key hint), weather/night profile + live wetness, input modality keys/pad/touch, session lap/time/best/finish, paused text, standings rows = model order, ERS number display; panel focus + Escape-close paths; standings rect inside canvas. Gate string: `CHECKS productControls=ok` |
| `MYSIM_FLOWCHECKS` (depth `controls_gameplay`-mapped today) | RaceFlowScenario | menu -> race -> finish -> results -> restart/menu flow through the real Main wiring |
| `MYSIM_RESULTSCHECKS` | ResultsLayoutScenario | results layout, scroll, HUD/touch overlay isolation, main-menu action |
| `MYSIM_RETIREMENT_CHECKS` | RaceRetirementScenario | retirement -> results path |
| `settings_persist_review` (29 Linux cases, 32 Windows) | SettingsPersistChecks | remapped keys PERSIST (legacy_long_roundtrip included) - the durability side of the remap path; contract sync guards the fixture contract |
| `reference_evidence` (+ section-1 viewport fix) | cloud_game_audit + EmitRects | geometry: HUD_RECTS bounds at 5 resolutions/aspects; wheel/cockpit semantics; THIS gate also compiles the full gameplay tree incl. my Hud changes at the released ref |

## 3. Verification of this turn's work

- Full source review of every bound member (Vehicle fields, Race.Add/wetness/Standings/targetLaps, ControlBindings.Get/Set long path, Conditions profile names, Controls focus/escape code paths) - all read from the canonical tree this turn; no API was assumed.
- Formatting literals verified independently (km/h conversion 11.6 m/s -> "41 km/h"; int truncation 33.7 -> "FUEL 33 L"; lap times "12.3s"/"87.5s"; two-space "PAUSED  ").
- NOT compiled locally (cloud-only contract); the fixture and Hud changes compile only at the released ref - the proposed dispatch doubles as the compile gate. If it fails on my files, revert those three files, keep the report.
- Visual approval stays with the human reviewer (bf5b598's ref-hud PNG review still pending; this turn adds no rendering).

## 4. Open items

- ModeMenu focus rule ("4th child grabs") is fragile to content edits (a fourth non-button line would silently un-focus the menu): current behaviour asserted; a structural focus fix inside ModeMenu is a candidate for a later turn if the reviewer wants it.
- If the reviewer wants the pit-service timing visible in the top strip (currently PitServiceDisplay top-centre, unowned file), that remains a coordinator/car-assembly call.
- The `ControlsPanel` fits itself to the viewport with its own scale-aspect logic (untouched); it is not part of HUD_RECTS bounds - optional follow-up to include it in a future geometry gate.
