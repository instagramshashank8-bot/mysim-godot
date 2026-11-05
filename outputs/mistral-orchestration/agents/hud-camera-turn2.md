# HUD / camera specialist - turn 2 report (branch mistral/hud-camera)

Base for this turn: 076363b (merged into my branch; my turn-1 commits 19ce505/7f22ec8 are ancestors of it). This turn's commit: bf5b598 (src/Hud.cs, src/RaceStandingsPanel.cs, src/RaceStartLights.cs - 35 insertions, 11 deletions; only my owned files). Read-only inputs: REVIEW_PACKET.md session-2 section, VISUAL_REVIEW.md (both reviews), JOB_REGISTRY.json (2026-11-04T11:20Z). No dispatches, no workflow edits, no runs; one read-only `gh run view` for the status of the in-flight run affecting my scope.

## 1. Session-2 findings

1. Turn-1 work adopted and verified narrow-scope: run 41798036351 (car_evidence) + VISUAL_REVIEW 2026-11-04T09:10Z - speed/unit inside frame at all five resolutions, info panel more compact, wheel closeups + steered FL (corners=32) real, genuine cockpit ("COCKPIT_VIEW mode=2 fov=75 seatLocal=(0,0.88,0.05) seatOffsetFromAuthored=0.000 wheelsHidden=True"). Wheel pivot index mapping adopted as proposed (0=FR, 1=FL, 2=RR, 3=RL).
2. Coordinator added `Hud.EmitRects()` + per-resolution `HUD_RECTS` bounds assertions at 076363b (in-flight run 41820839687). The emitter and the element-bounds checks are sound; the assertion set is preserved by this turn's redesign (section 3).
3. DEFECT FOUND in the new HUD_RECTS assertion (coordinator-owned `tools/cloud_game_audit.py`; predicted to fail the in-flight run at 4 of 5 resolutions):
   the block asserts `viewport == raw window resolution`, but Main pins content scale (CanvasItems, 1280x720, aspect Expand) at Main.cs:24 and viewport resolution normally equals the design size, not the raw window pixels. The in-flight run will most likely stop at `ref-hud-640x360: viewport (1280.0, 720.0) != requested 640x360` (or at 1920x1080 first depending on loop order). NOT a HUD layout fault and NOT fixed by any HUD change; needs the audit-side correction below.
   - Evidence (godotengine/godot source, read this session): `window.cpp _update_viewport_size` - for CONTENT_SCALE_MODE_CANVAS_ITEMS: `final_size = screen_size; final_size_override = viewport_size / content_scale_factor;` with `viewport_size` = design window (EXPAND: design base extended; same-aspect: base itself), and `_set_size(final_size, 1, final_size_override, allocate)`; `viewport.cpp get_visible_rect` - "if (size_2d_override != Size2()) { r.size = size_2d_override; }" - the design size WINS. Version note: verified against godotengine/godot master read today; the size_2d_override-wins rule in get_visible_rect is long-standing 4.x behavior.
   - Expected design sizes (base 1280x720, factor 1, all supported captures): 640x360 -> (1280, 720); 1280x720 -> (1280, 720); 1920x1080 -> (1280, 720); 1920x1200 -> (1280, 800); 2560x1080 -> (1706, 720) (Godot floors; 1706.67 -> 1706).
   - PROPOSED audit-side correction (coordinator-owned; not edited by me), replacing ONLY the viewport-equality assert:

```diff
--- a/tools/cloud_game_audit.py
+++ b/tools/cloud_game_audit.py
@@ -371,8 +371,15 @@
   vm=re.match(r'HUD_RECTS viewport=\(([-\d.e]+), ([-\d.e]+)\)',rects[0])
   assert vm,f'ref-hud-{resolution}: unparsable HUD_RECTS line'
   vw,vh=float(vm.group(1)),float(vm.group(2))
-  assert (vw,vh)==(float(resolution.split('x')[0]),float(resolution.split('x')[1])),f'ref-hud-{resolution}: viewport {(vw,vh)} != requested {resolution}'
+  # Content scale (Main: CanvasItems 1280x720 aspect Expand) renders 2D in design units;
+  # Viewport.get_visible_rect() returns the design size (viewport.cpp: size_2d_override wins). Check that:
+  w,hh=[float(x) for x in resolution.split('x')]
+  if abs(w*720-1280*hh)<1e-3:expect=(1280.0,720.0)          # same 16:9 aspect as the design base
+  elif w*720>1280*hh:expect=(float(int(720*w/hh)),720.0)    # wider: design width grows
+  else:expect=(1280.0,float(int(1280*hh/w)))                # taller: design height grows
+  assert (vw,vh)==expect,f'ref-hud-{resolution}: design viewport {(vw,vh)} != expected {expect} for window {resolution}'
```

   The element-bounds check right after it (`px>=0 and py>=0 and px+rw<=vw and py+rh<=vh`) already uses the reported design viewport and is correct as written - keep unchanged (never weakened).

## 2. HUD hierarchy redesign (committed bf5b598)

Reviewer target (VISUAL_REVIEW 2026-11-03: "disconnected classification/speedgear/minimap/ERS panels, weak coherent hierarchy" + turn-2 brief): cohesive top strip, standings grouping, speed/gear bottom-right grouping, minimap bottom-left, hybrid ERS placement. Original MySim identity: the existing midnight-chip language (dark translucent panels, cool 1px edge, 6px corners, "MYSIM /" typography elsewhere) is reused and unified; no Monoposto-derived layout or branding.

Layout (design coordinates; canvas is 1280x720 for 16:9 at every resolution from 640x360 to 1920x1080; 16:10 -> 1280x800 and 21:9 -> 1706x720 add space only, anchors are edge-relative):

| Element | Group | Placement | Grow rule |
|---|---|---|---|
| Top strip (new PanelContainer) | session + car state | x12..canvas-12 (touch: stops at canvas-340, clear of the speed column), y8..72 fixed 64 px band | width: anchored both edges; height fixed; content HBox |
| `info` = SESSION state (POS/LAP/TIME/BEST, font 17) | top strip left | inside strip, vertically centred | hugs text; left edge fixed at strip margin |
| `carState` = CAR/strategy state (TYRES/PIT/FUEL/DAMAGE, font 15, muted) | top strip right | inside strip, right-aligned, vertically centred | hugs text, right edge fixed by HBox fill spacer |
| Start lights chip (RaceStartLights) | top strip centre | x canvas/2 +/-130, y11..69 (fits the 64 px band; drawn after the strip = on top) | fixed; unaffected by resolution |
| RaceStandingsPanel | grouped under strip | Position (12,80): contiguous left rail, 8 px below the strip | PanelContainer hugs rows (CustomMinimumSize 255) |
| Speed/gear chip (desktop) | bottom-right group | ONE PanelContainer holding speed 46 + gear 24 (VBox, right-aligned) anchored at the safe-margin corner (right-20/bottom-20) | zero-size at corner; GrowHorizontal=Begin, GrowVertical=Begin -> expands leftward/upward only; content-hugging |
| Speed/gear (touch) | top-right pair | offsets unchanged from turn-1 (x -320..-24, y 24..94 / 94..164) | GrowHorizontal=Begin (unchanged, policy-verified) |
| MiniMap | bottom-left | x12..202, y canvas-170..canvas-20 (touch top-right unchanged) | fixed size, anchors |
| ERS (HybridControlsPanel instance, file untouched) | bottom-centre group member | bottom baseline unified: offsets -66/-20 set from Hud.cs after AddChild | fixed, centred |

Resulting geometry sentences: top strip + standings + minimap form one coherent left rail (all share the 12 px left margin); the bottom row (minimap | ERS | speed/gear) shares one 20 px baseline; the start lights live inside the top band instead of floating 85 px below it. Every chip uses the same style box (Chip(): midnight 0.82 alpha + translucent 1 px edge + 6 px corners + 12/6 content margins), so the five previously disconnected surfaces read as one HUD.

Behavioral notes:

- All HUD data strings are split, not altered: former `info` line 1 -> `info` (strip left), former line 2 -> `carState` (strip right). PAUSED keeps overwriting `info` only (carState retains its last value, same freeze behaviour the old two-line panel had).
- EmitRects (the coordinator's assertion emitter) is untouched and still names all six elements; it now also emits `carState`, so the new label is bounds-checked like the others (added coverage, nothing removed).
- Draw order: strip -> start lights -> panels -> speed chip -> minimap; the strategy panel still draws above the strip when opened (unchanged relations: standings hide nothing; minimap still `!strategy.Open`).
- GridLightsScenario compatibility: it asserts `lights.GetGlobalRect().Size.X >= 250` (chip is 260 wide, unchanged) plus visibility/timing; only the Y position moved.

## 3. Existing reference_evidence assertions that must still hold (verification table)

| # | Assertion (source: tools/cloud_game_audit.py, REFERENCE_EVIDENCE branch) | Expected outcome at my branch's merge ref |
|---|---|---|
| 1 | ref-car16-{front,rear,left,right,top}.png exist (CAMPOS+FITVIEW static views) | unchanged by HUD work; keep passing (corners=376 gate in car_evidence variant) |
| 2 | 4 wheel closeups (MYSIM_WHEEL_CLOSEUP 1/0/3/2 = fl/fr/rl/rr per verified mapping) + steered FL (WHEEL_FIT_STEER=-20): shot exists AND `PASS complete visible wheel framing` in output (corners=32) | unchanged by HUD work; keep passing |
| 3 | Cockpit: shot exists AND `COCKPIT_VIEW mode=2` AND `wheelsHidden=True` AND `seatOffsetFromAuthored=0.000` (DriveController CamMode 2 boot; emitter unchanged this turn) | keep passing |
| 4 | ref-hud-{res}.png exists for 640x360, 1280x720, 1920x1080, 1920x1200, 2560x1080 (4 s drive, MYSIM_HUD_RECTS=1) | keep passing (design-size captures; my redesign keeps all elements inside) |
| 5 | Exactly ONE `HUD_RECTS viewport=` line per HUD capture (one EmitRects call per CaptureShot) | keep passing (EmitRects called once per capture; emitter untouched) |
| 6 | Parsed viewport (vw,vh) equals the requested resolution | DEFECTIVE as written - expected design size is (1280,720)/(1280,720)/(1280,720)/(1280,800)/(1706,720) for the five captures (engine-source verified, section 1). Replace with the design-size equality (diff in section 1); this STRENGTHENS the assertion (it becomes a true invariant) rather than weakening it |
| 7 | Every parsed element rect (speed/gear/info/carState after my change, minimap/standings/hybrid) inside the reported viewport: px>=0, py>=0, px+rw<=vw, py+rh<=vh | keep passing - all my placements use 12/20/8 px safe margins with inward grow rules; desktop bottom-right chip and top strip cannot exceed the reported design viewport at any supported resolution/aspect |
| 8 | Drive frames ref-lap-near (CamMode 0, 9 s) + ref-lap-wide (CamMode 1, 14 s) exist | unchanged |

## 4. Proposed dispatch (coordinator runs; I dispatch nothing)

1. Fix the viewport assertion per section 1 diff (coordinator-owned file), then dispatch at a ref containing bf5b598 (my redesign) + the corrected audit:
   `gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref <ref> -f depth=reference_evidence`
   PASS criteria: all table-1..8 rows green; HUD_RECTS design-viewport equalities exact per the section-1 table; every element rect inside; ref-hud PNGs then reviewed manually for the hierarchy (strip/centred lights/left rail/bottom baseline).
2. If run 41820839687 (in flight at 076363b) fails on `viewport != requested`, that is the section-1 audit defect, NOT a HUD regression - do not weaken or remove the bounds checks; apply the correction and re-dispatch (preserving the failure record).

## 5. Remaining risks / open items

- The C# redesign is not compiled locally (cloud-only contract; first displaced run compiles it). API surface used is already proven in-repo (PanelContainer/Chip stylebox/ContentMargin*, Control.SizeFlags.ExpandFill, Label Vertical/HorizontalAlignment, GrowDirection).
- The viewport-equality defect is a prediction from engine source (master; the exact behaviour of godotengine/godot 4.7.2-private assumed identical - the size_2d_override-wins rule is old and stable, but the in-flight run's log is the ground truth; check before trusting my prediction either way).
- Overlap checks are geometric (hand-computed) for the surveyed string widths; the Strips/labels cannot exceed the design viewport per the grow rules, but the actual aesthetic verdict (cohesion, hierarchy readability) needs the human reviewer on the ref-hud PNGs.
- Touch layout beyond the strip addition is intentionally untouched (touch policy verified earlier at 1280; the strip addition only adds a band above existing touch elements, stop-at-940 keeps the speed column clear).
- carState/EmitRects addition changes the HUD_RECTS line content (extra pair) - the audit parses all found pairs, so no audit change is required for that; flagged here so the coordinator knows the line grew.
