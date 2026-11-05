# Track rendering — turn 8: invisible-wall evidence tooling (collision overlay + named contacts + per-name trim)

Author: track-render specialist. Branch `mistral/track-render`. NO dispatches (fleet/fence/night runs in flight); nothing executed beyond read-only source reading and local patch authoring; no compile exists yet.

## 1. Collision-overlay evidence mode (ONE flag: `MYSIM_COLLISION_OVERLAY=1`)

Implemented in `src/WallBuilder.cs` (my file): a `CollisionOverlay` node (partial, Node3D, deferred build on first physics tick — the `TrackNightLighting` pattern — so the drive surface added later in the same frame is included; build scans the layer's parent world, not just the layer). Hooked from `TrackMaterialBinder.Apply` after all binding/passes: `WallBuilder.AttachCollisionOverlay(track)` — attaches NOTHING when the flag is unset (default rendering bit-identical).

Behaviour when the flag is set:

1. **Every static collider renders as a transparent colour-coded shape over the visible scene**: `BackfaceCollision`-carrying `ConcavePolygonShape3D` (walls, drive surface — the shapes the track actually uses) render as their exact triangle mesh (`shape.Data` → `ArrayMesh`); box/cylinder/sphere/capsule/world-boundary render as primitives; convex-hull point clouds are audited but not drawn (v1 keeps the overlay cheap; the track's colliders are concave). Materials: unshaded, alpha 0.32, `NoDepthTest=true`, double-sided — buried or offset colliders are directly visible against their visible counterpart, which is the point: a wall the car hits but cannot see becomes a red volume exactly where it sits.
2. Colour by collision layer: layer1-only (drive/support floors) green; layer2-only (walls) red; both yellow; other purple. `CastShadow=Off` on all overlays.
3. **Audit JSON** (`MYSIM_COLLISION_OVERLAY_AUDIT=<path>`): every collider gets a row `{body, path, shapeType, layer, mask, worldAabb(position+size), counterpart, counterpartSize, centreOffset}` — `WallBuilder` names bodies `<meshName>_col`/`_support`, so the body name IS the visible-source mapping; `centreOffset` (collider AABB centre minus counterpart mesh AABB centre) directly detects displaced/double-transformed colliders — the suspected invisible-wall class (the default-on `MYSIM_METRE_WALL_COLLIDERS` bakes `GlobalBasis` into already-world-space vertices in `WallBuilder.AddFaces`; if any node basis is non-identity that re-transforms the world verts — the overlay's offset column will show it as a number and as a red cloud). `unmatched` lists colliders with NO visible mesh in their ancestry (invisible walls by definition). Drive surface deltas: `{colliderXZ, visualXZ, centreDeltaXZ, distance}` vs the nearest visible `gnd_*`/`road*` mesh — the drivable-width-vs-visual-geometry check (item 4).
4. **Per-name trim (item 3's tool): `MYSIM_COLLIDER_TRIM=<json-path>`** — a JSON array of `{name, reason, evidence}`; for each entry, EXACT-name `StaticBody3D` matches get `CollisionLayer=0, CollisionMask=0` (inert, retained, reversible — no deletion, never blanket); each entry's applied count + the supplied `reason`/`evidence` string are audited; entries matching nothing are audited with `disabledBodies=0` so a stale name is visible, never silent. Initial trim content: NONE — the overlay evidence run produces the named list first; trimming without that evidence is exactly what the policy forbids.
5. Log: `COLLISION_OVERLAY shapes=N drawn=M unmatched=K driveDeltas=D trims=T`.

## 2. Contact logging — collider name mapping (item 2)

`src/NativeSuspensionVehicle.cs` `RoadContactEvidence` (the contact-evidence function; rows already carried position/normal/face/triangles/sourceNormal/sourceCrossY): extended minimally and additively — each row now also carries `colliderName` (the StaticBody3D name — e.g. `fence_brands_hatch_..._col` — the mesh ID by the WallBuilder naming convention), `colliderPath` (full scene path) and `shapeType`. Name capture happens whenever a collider is present; the concave-triangle geometry capture is unchanged (same condition chain, same fields); no assertion touched, no row removed, no behaviour change beyond three added JSON fields. The chassis-side contacts (`ContactParts`/`ContactSurfaces`, `NativeSuspensionVehicle.cs:91`) already carry the shape-node name + collider path — unchanged.

**File-ownership note for the coordinator:** `NativeSuspensionVehicle.cs` is not in my standing exclusive list; this turn's brief explicitly directs extending the native controller's contact logging, so the edit rides this commit as a minimal additive change for the coordinator to integrate, reassign or veto. My four standing files are unchanged except `WallBuilder.cs`.

## 3. Wall/scenery audit against source (items 3-4)

The overlay's audit IS the audit mechanism (evidence-first, per-name removal only):

- **WallBuilder-generated walls**: rows pair each `_col`/`_support` body with its parent visible mesh; `centreOffset` flags any displaced/rotated collision (the double-transform suspicion); unmatched rows flag structure-colliders without visible geometry.
- **Road seams / drive surface**: `DriveSurface` (layer 1, `DriveSurfaceLoader.cs:16`, `BackfaceCollision=true`) vs the nearest visible `gnd_*/road*` mesh — `driveWidthDeltas` quantifies drivable width vs visual width (item 4) per road piece; pit entry/exit and run-off read from the same rows (the entry/box widths are road pieces in the same class).
- **Scenery collision/layers**: every static body's layer/mask is audited (visual = colour-coded in the overlay render).
- **Oversized convex hulls**: hull point-clouds are counted and audited (extent vs the source's visible bounds is the reviewer's call from the overlay); the native car-collider side already has its own coverage checks (`NativeCarColliders.CoversSource` — source hull coverage asserted there; untouched).
- **Removal**: only via `MYSIM_COLLIDER_TRIM` named entries with evidence — never a policy, never blanket; legitimate barriers/kerbs/bridge supports are untouched by default (they keep layer 2 layers... layer 2/1 as built).

## 4. Evidence protocol (item 5)

- **Regression routes at the reported areas:** the reported symptom is generic ("invisible barrier walls block driving"); the named evidence runs pair the overlay with the drives over the SAME cameras as the fence/night runs: track 0 (Hatch — the reference), track 25/28/30 (the night/stall evidence tracks) + the pit lane (entry/box/exit read from the drive-surface rows).
- **Representative all-tracks set:** the existing startup-render-probe sweep class (tracks 0,6,12,...,30 — the 41830995246 sweep) with the overlay flag on; one run covers the sweep WITH evidence instead of blind renders.
- **Readout:** for each run: `COLLISION_OVERLAY shapes/drawn/unmatched/driveDeltas/trims` log line + the audit JSON; the reviewer sees every collider over the scene in the 960x540 PNG + the JSON numbers; the invisible-wall verdict = human visual comparison (red wall visible where the car stops, overlay vs naked-eye branch pairs).
- NO dispatches this turn (fleet + fence + night runs in flight); the overlay's first flight = the coordinator's next fence/fleet-cycle run with the env flag set + audit path.

## 5. Commit contents and verification status

- `src/WallBuilder.cs` (+105: `AttachCollisionOverlay` + the `CollisionOverlay` class) — the ONE flag.
- `src/TrackMaterialBinder.cs` (+1: the hook line).
- `src/NativeSuspensionVehicle.cs` (+3/-3: the name-mapping extension; flagged above for ownership).
- `outputs/mistral-orchestration/agents/track-render-turn8.md` (this file).
- Verification: full-file re-reads of all four; brace/paren balance (WallBuilder 56/56 + 217/217; NSV 67/67 + 378/378; TrackMaterialBinder 53/53 + 243/243... binder measured with the turn-3 state); API patterns matched to CI-proven usage (`ConcavePolygonShape3D.Data` already read in `NativeSuspensionVehicle`; `ArrayMesh.AddSurfaceFromArrays` standard; `ShapeOwnerGetOwner`/`ShapeFindOwner` already used in the same function pre-patch). NO compile, NO dispatch — coordinator owns execution; the overlay's first flight = the next fence/fleet-cycle run with `MYSIM_COLLISION_OVERLAY=1` + audit paths set.
