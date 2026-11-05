# HUD / camera specialist report — branch mistral/hud-camera

Date: 2026-11-04. Worktree: C:/Users/shash/MySimGodot-wt-hud (base c87ae00). Owned files touched: src/Hud.cs, src/DriveController.cs. Shared files: NOT edited (tools/cloud_game_audit.py patch proposed below only).

## 1. Changes

### src/Hud.cs — 960x540 clipping fix (reviewer defect #2)

Root cause, reproduced from code: commit 32d499d narrowed the right-anchored speed label box from 276px to 110px (desktop) / 210px (touch) while keeping font 46. A Godot Label whose text minimum width exceeds its rect overflows to the RIGHT (default GrowHorizontal=End), so "247 km/h" (about 250px at font 46) spilled from the box right edge (canvas width - 20) past the framebuffer edge. Because Main.cs sets ContentScaleMode=CanvasItems, ContentScaleSize=1280x720, Aspect=Expand (line 24), every 16:9 resolution (640x360, 960x540, 1280x720, 1920x1080) maps to the SAME 1280x720 canvas coordinates; 16:10 (1280x800) and 21:9 (1707x720) canvases only add space. The clip was therefore identical at all supported resolutions; the reviewer measured it at 960x540.

Fix (all in canvas units, no resolution-specific code needed):

- Speed box widened to 300px (desktop, -320..-20) / 296px (touch, -320..-24); gear box to 220px. Right edges unchanged at the safe margins (-20 / -24), right alignment unchanged, so the visual position of the text is the intended pre-32d499d one.
- GrowHorizontal=Begin on both right-anchored labels (plus GrowVertical=Begin for the bottom-anchored desktop labels): any string longer than the box now expands the box LEFTWARD/UPWARD instead of across the right margin. This is the resolution- and text-length-independent guarantee the reviewer asked for.
- Info panel: the fixed 640x102 Panel ("roughly half screen width with large unused space") replaced by PanelContainer > MarginContainer > Label that hugs the label minimum size. Same style box, same text origin (24,20), same label content. Occupied width now tracks the widest current line (about 500-590px) and follows state strings, inside safe bounds at every supported canvas.
- All data preserved verbatim: speed, gear, lap, pos, lap time, best, tyres, pit state, fuel, damage, PAUSED line. No string changed.

### src/DriveController.cs — verifiable cockpit evidence (reviewer defect #5)

CaptureShot now prints, when the genuine in-game cockpit camera (CamMode 2) takes the shot:

    COCKPIT_VIEW mode=2 fov=75 seatLocal=(-0.35, 0.88, 0.05) seatOffsetFromAuthored=0.000 wheelsHidden=True

- fov: authored cockpit FOV 75.
- seatLocal: camera position in car-local space (Transform3D round-trip of the authored seat pose).
- seatOffsetFromAuthored: distance from the exact authored seat pose (class==1 ? (0,0.88,0.05) : (-0.35,0.88,0.05)); float32 round-trip noise only.
- wheelsHidden: all four WheelPivots hidden (cockpit mode behaviour, DriveController _PhysicsProcess).

Print-only emitter; no assertion weakened, no existing flow changed.

## 2. Wheel pivot index mapping — verified (reviewer defect #4)

- CarLoader.AttachWheels builds pivots i=0..3 from assets/car_pivots.json with rearAxle=(float)p[2]<0: front axle is z>0. Nose is +Z local (DriveController basis comment; WheelAnimation.Update steers i<2 = front pair).
- Rendered left/right: car basis columns (carX, n, forward), forward=(sin h, 0, cos h), carX = n x forward. CoreChecks.cs:36-38 asserts that screen-right steering (intent +1) drives position.x negative while heading +Z, so rendered car RIGHT = -X and LEFT = +X.
- car16 (car_pivots.json["16"], i order 0..3):
  - 0 = (-0.763, 0.340, +2.008)  FRONT-RIGHT
  - 1 = (+0.763, 0.340, +2.008)  FRONT-LEFT
  - 2 = (-0.818, 0.340, -1.589)  REAR-RIGHT
  - 3 = (+0.818, 0.340, -1.589)  REAR-LEFT
- Tension flagged for the car-assembly owner (not my file, not changed): RecoveredComponentMounts.cs:45,52 mounts source parts named _LEFT at hub 0/2 (x<0) — source LEFT naming lands on the RENDERED RIGHT side under preserved coordinates. Consistent with the reviewer's mirrored-livery finding (#3). One of the two conventions must be reconciled during the livery/UV mirror investigation; rendered truth (CoreChecks screen-right) is authoritative for reviewer-facing labels.
- Harness defect mechanism confirmed: the reference_evidence 'wheel-fl' view was a FREE camera (MYSIM_CAMPOS 1.9,0.62,2.35) with MYSIM_FITVIEW=1 — MYSIM_WHEEL_CLOSEUP was never set, so FitCarView framed the whole car. The 'cockpit' view was a free camera at (0,1.12,0.05) aiming down at (0,0.6,0) — hence the overhead whole-car image. Neither ran the real closeup/cockpit code paths at all. Both replaced in the proposed audit patch below.

## 3. Genuine cockpit view spec (reviewer defect #5)

- The in-game cockpit is DriveController CamMode==2 (MYSIM_CAM=2 at boot; camera cycle key/button also reaches it). Pose: Cam.Position = Car.GlobalTransform * (class==1 ? (0,0.88,0.05) : (-0.35,0.88,0.05)); camera yaw flipped 180 degrees around car up so it looks along the nose; FOV 75; car body interior visible per CarViewVisibility cockpit mode; wheel pivots hidden.
- Evidence capture must use the real game boot, not a CAMPOS free camera:

      MYSIM_TRACK=0 MYSIM_CAR=16 MYSIM_AI=1 MYSIM_AUTODRIVE=1 MYSIM_CAM=2 MYSIM_SECONDS=9 MYSIM_SHOT=<out>/ref-car16-cockpit.png MYSIM_NOPIT=1 MYSIM_RIVALS=0 MYSIM_NATIVE_RECORD_FPS=30

- Semantic acceptance (proposed audit assertions): screenshot exists AND log contains "COCKPIT_VIEW mode=2" AND "wheelsHidden=True" AND "seatOffsetFromAuthored=0.000". Interior visibility itself (wheel, halo/airbox, mirrors on screen) stays a manual reviewer judgment on the PNG; the printed pose/FOV ties the PNG to the authored cockpit state so filename/existence alone can never satisfy acceptance.

## 4. Proposed tools/cloud_game_audit.py patch (SHARED FILE — NOT edited by me; coordinator applies)

```diff
--- a/tools/cloud_game_audit.py
+++ b/tools/cloud_game_audit.py
@@ -290,19 +290,44 @@
 if os.environ.get('REFERENCE_EVIDENCE')=='1':
- # Reference car16/track0 baseline evidence: nine static views + live drive frames via the EXISTING
- # MYSIM_CARVIEW/MYSIM_CAMPOS/MYSIM_SHOT boot harness used by panel/rim flows. No material modifications.
- views=(('front','0,1.35,5.6'),('rear','0,1.35,-5.6'),('left','-5.6,1.35,0'),('right','5.6,1.35,0'),
-        ('top','0,9.0,0.02'),('wheel-fl','1.9,0.62,2.35'),('cockpit','0.0,1.12,0.05'))
+ # Static body views via MYSIM_CAMPOS+FITVIEW. Wheel closeups via the ACTUAL MYSIM_WHEEL_CLOSEUP
+ # pivot-index branch (verified car_pivots order 0..3: front axle z>0; rendered car left=+x per the
+ # CoreChecks screen-right convention => 0=front-right, 1=front-left, 2=rear-right, 3=rear-left).
+ # Cockpit via the REAL in-game camera (DriveController CamMode 2, MYSIM_CAM=2 autodrive boot):
+ # free MYSIM_CAMPOS cameras cannot produce wheel-closeup or cockpit evidence.
+ views=(('front','0,1.35,5.6'),('rear','0,1.35,-5.6'),('left','-5.6,1.35,0'),('right','5.6,1.35,0'),
+        ('top','0,9.0,0.02'))
 missing=[]
 for view,cam in views:
  shot=out/f'ref-car16-{view}.png'
  label=f'ref-car16-view-{view}'
- text=run(label,{'MYSIM_CARVIEW':'16','MYSIM_FITVIEW':'1','MYSIM_CAMPOS':cam,'MYSIM_TRACK':'0','MYSIM_SHOT':str(shot)},timeout=420)
+ text=run(label,{'MYSIM_CARVIEW':'16','MYSIM_FITVIEW':'1','MYSIM_CAMPOS':cam,'MYSIM_SHOT':str(shot)},timeout=420)
  if not shot.exists():missing.append(view)
+# All four wheel closeups (default authored rims, medium compound) with the framing assertion, plus
+# one steered front-left pose for swept-clearance evidence.
+for closeup,wheel in ((1,'fl'),(0,'fr'),(3,'rl'),(2,'rr')):
+ shot=out/f'ref-car16-wheel-{wheel}.png'
+ text=run(f'ref-car16-wheel-{wheel}',{'MYSIM_CARVIEW':'16','MYSIM_WHEEL_CLOSEUP':str(closeup),'MYSIM_WHEEL_OBLIQUE':'1','MYSIM_SHOT':str(shot)},timeout=420)
+ if not shot.exists() or 'PASS complete visible wheel framing' not in text:missing.append(f'wheel-{wheel}')
+shot=out/'ref-car16-wheel-fl-steered.png'
+text=run('ref-car16-wheel-fl-steered',{'MYSIM_CARVIEW':'16','MYSIM_WHEEL_CLOSEUP':'1','MYSIM_WHEEL_OBLIQUE':'1','MYSIM_WHEEL_FIT_STEER':'-20','MYSIM_SHOT':str(shot)},timeout=420)
+if not shot.exists() or 'PASS complete visible wheel framing' not in text:missing.append('wheel-fl-steered')
+# Genuine cockpit: DriveController CamMode 2 drive boot; engine prints pose/FOV/interior facts.
+shot=out/'ref-car16-cockpit.png'
+text=run('ref-car16-cockpit',{'MYSIM_TRACK':'0','MYSIM_CAR':'16','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_CAM':'2','MYSIM_SECONDS':'9','MYSIM_SHOT':str(shot),'MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_NATIVE_RECORD_FPS':'30'},timeout=600)
+if not shot.exists() or 'COCKPIT_VIEW mode=2' not in text or 'wheelsHidden=True' not in text or 'seatOffsetFromAuthored=0.000' not in text:missing.append('cockpit')
 if missing:raise SystemExit(f'reference evidence views missing: {missing}')
+# HUD safe bounds across supported resolutions/aspect ratios: short near-chase drive frame per
+# resolution; every capture must render with all HUD labels inside the frame (defect #2 regression
+# watch; 960x540 is covered by ref-lap-near below).
+for resolution in ('640x360','1280x720','1920x1080','1920x1200','2560x1080'):
+ shot=out/f'ref-hud-{resolution}.png'
+ run(f'ref-hud-{resolution}',{'MYSIM_TRACK':'0','MYSIM_CAR':'16','MYSIM_AI':'1','MYSIM_AUTODRIVE':'1','MYSIM_SECONDS':'4','MYSIM_SHOT':str(shot),'MYSIM_NOPIT':'1','MYSIM_RIVALS':'0','MYSIM_NATIVE_RECORD_FPS':'30'},['--resolution',resolution],timeout=600)
+ if not shot.exists():missing.append(f'hud-{resolution}')
```

Notes on the patch:

- The five static body views keep their exact CAMPOS values and the dead 'MYSIM_TRACK' env is dropped from them (CarView returns before StartRace; it was inert config).
- Wheel-closeup runs must NOT set MYSIM_FITVIEW (the C# branch is if/else-if; FITVIEW would re-frame the whole car).
- Semantic strings asserted: 'PASS complete visible wheel framing' is printed by Main.FitCarView(subject "wheel") in the closeup branch; COCKPIT strings by the new DriveController print on my branch. The patch therefore requires my commits to be in the dispatched ref.
- Budget: 5 wheel shots (still boot, seconds each) + 1 cockpit 9s drive + 5 short 4s drive frames; per the existing comment 14s sim approx 210s wall at 960x540, the heaviest added item is the 2560x1080 4s frame (about 340-400s) within the 600s timeout.
- The final COMPLETE print should be updated by the coordinator to the new counts, e.g. f'COMPLETE reference evidence: {len(views)} body views, 5 wheel closeups, 1 cockpit, {5+drive_count} live frames captured'.

## 5. Proposed dispatch configs (I dispatch nothing — coordinator runs)

After the coordinator merges/pushes my branch commits and applies the audit patch:

    gh workflow run game-review.yml -R instagramshashank8-bot/mysim-godot --ref <ref containing my commits + audit patch> -f depth=reference_evidence

- The workflow maps reference_evidence to shard [0] (existing mapping); no other inputs; do NOT use native_mounted_laps for named cases (existing preflight guard).
- Artifact: encrypted-review-0-attempt-1 / game-runtime-0-attempt-1 as before.
- Post-decrypt manual review checklist:
  1. ref-lap-near.png plus the five ref-hud-<resolution>.png: every HUD string (speed, gear, info panel, standings, minimap) fully inside frame at 640x360, 960x540, 1280x720, 1920x1080, 1920x1200, 2560x1080.
  2. ref-car16-wheel-{fl,fr,rl,rr}.png: each shows the assembled wheel closeup from outside its corner (not the whole car); wheel-fl-steered.png shows the front-left wheel steered, body anchors fixed.
  3. ref-car16-cockpit.png: interior from the authored seat (halo/airbox, wheel, mirrors), not an overhead view; log line COCKPIT_VIEW present with seatOffsetFromAuthored=0.000 and wheelsHidden=True.
- Optional second run for camera-framing regression at the same head: -f depth=gui_review (existing depth asserts 'PASS full camera cycle restores all four wheels' and 'PASS complete gameplay car framing').

## 6. Camera framing sanity check (deliverable 3 — no numeric change made)

- Current near chase (CamMode 0, commit 26c0f5a): back = len*1.28 (about 6.3m for the roughly 4.9m formula body), up = clamp(hgt*1.45+0.22, 2.0, 2.8) = 2.0m, look target = car + fwd*(3..7m speed-scaled) + 1.0m up, vertical FOV 43..48 speed-scaled, offset smoothing (exp lerp) plus ObstructCam pull-in; MYSIM_CAMERA_FRAME_CHECK asserts the whole car stays inside 2..98 percent of the viewport.
- Geometry vs the Monoposto-like target: from 6.3m back and 2.0m up, a roughly 1.0-1.1m tall car subtends about 8-9 degrees of the 43-48 degree vertical FOV — the car sits in the bottom quarter of frame with a high horizon and forward view bias; car visible and prominent, track ahead dominant. That matches the reference category; nothing in the numbers is off-target on paper.
- The reviewer's "car small in frame" applies to the WIDE mode-1 frame (len*1.85, about 8.7m back) and to the 5.6m static CAMPOS assembly views — deliberately wider evidence, not the chase framing target.
- Conclusion: I propose NO chase-camera number changes now. Decisive path: review ref-lap-near.png (near chase in motion) and the new ref-hud-* frames from the proposed dispatch; adjust back/up/FOV only if the reviewed frames disagree with the target. hub distances are established constants; do not re-tune blind.

## 7. Evidence reviewed

- outputs/repair-20261029/cloud/reference-evidence-41788844369/encrypted-review-0-attempt-1/game-review-41788844369-1-0/review/: all nine PNGs present, all 960x540 (dimension metadata read locally). I cannot render/inspect PNG content locally per the cloud-only contract; the reviewer's defect observations in VISUAL_REVIEW.md are cited as their findings, and each mechanism is independently reproduced from code above (whole-car wc-fl = FITVIEW branch never given a closeup; overhead cockpit = CAMPOS look-down free camera; right-edge clip = 110px label box with End growth under a 1280x720 canvas).
- Read: src/Hud.cs, src/DriveController.cs, src/Main.cs (CarView/FitCarView/closeup/cockpit branches), src/CarLoader.cs (AttachWheels), src/RecoveredComponentMounts.cs, src/WheelAnimation.cs, src/CoreChecks.cs, assets/car_pivots.json, project.godot, tools/cloud_game_audit.py (reference_evidence + run helper), .github/workflows/game-review.yml (depth/shard mapping), git show 32d499d.

## 8. Remaining risks

- C# changes are NOT compiled locally (cloud-only contract). Both files use only APIs already used elsewhere in this repo (Control.GrowHorizontal/GrowDirection, PanelContainer/MarginContainer pattern from ModeMenu.cs/SeriesSetupPanel.cs, Transform3D.AffineInverse() as in CaptureShot). First cloud run must compile green; if the pushed core run fails on my files, revert those two files, keep the report.
- The COCKPIT assert (seatOffsetFromAuthored=0.000) assumes the cockpit camera still sits exactly at the authored seat pose; if a future change legitimately offsets it, the tolerance must be revisited deliberately (additive assertion, replaces nothing).
- Mid-session window resize does not re-flow the HUD (built once in _Ready; cloud captures are fixed-resolution boots). Add a Viewport.SizeChanged hook if that ever matters.
- Handedness tension in Section 2 (RecoveredComponentMounts LEFT naming vs rendered left) is flagged, not resolved — that file belongs to car-assembly.
- The proposed patch requires my DriveController print; dispatching it against a ref without my commits fails the cockpit check (by design, preserves failure records rather than silently passing).
