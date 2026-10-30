# Current verified state

The complete game is not finished. Computation, rendering and simulations run on GitHub. Local bulk evidence is on D:, with review ledgers and reports here. Original assets and failure history are preserved.

Verified: all1152 controlled braking cases stop; worst120/240Hz stopping-distance difference0.0361%; slick stopping distance increases with surface water. Native bridge-track laps pass all24cars. Curvature-aware AI mode3 plus stable solver passes all24native Monaco cars with valid laps, zero wall hits and zero damage (41097075244). This is an experimental candidate, not production promotion.

Important correction: inverse-only mode5 workflow41098418532 appeared green because tee masked failures. Raw data shows1435pass/5fail, all layout54, cars15/16/17/18/23. The missing pipefail is fixed. Preserve these failures and do not call this controller complete. Mode3 full1440-case logic regression41099407474 failed:1432pass/8invalid laps. Neither candidate is promoted. Native mode5 Monaco41098900936 is a separate controlled comparison.

Controls: vector steering pads/pedals and three-resolution multi-touch tests passed41094132696. Actual scene/camera cycling passed41095621468. Contextual pit options and separated action bar passed corrected GUI41096996396. Source compile failure41096198914 is preserved; edf123a removed an erroneous method from the nested minimap class.

Camera: translation-follow fix passed actual scene41097459256, but manual review found overly close framing and map overlap. 3aae583 increases geometric chase distance/height, moves map top-right, and adds a complete-car projection gate. Capture41099180035 passed the complete-car projection and camera-cycle checks; manually reviewed: full car visible, map clear of the car, compact controls and contextual pit card. Bloom defaults off for crisp presentation.

Visual failures remain: car panel/material assembly, noisy fences/crowds/roof, near-black night without authored circuit illumination, wet spray, road UV/detail quality and final HUD composition. Ground comparison41094342825 produced four matched captures; original-UV mode also looks flat/stretched, so it was not promoted. Read-only geometry audit41098653061 found zero exact duplicate groups in2864visible track0meshes and six lighting-name candidates (primarily signals), but failed teardown with unsafe managed resource references. This is not an accepted audit. Distinct collection-disposal fix22b4e8c is queued as41100725417.

Reference: all16033original Monoposto frames extracted at original resolution with timestamps/hashes, encrypted in64batches (41095108886). This is not manual completion. Part3frames257?512 were overview-inspected; frames440/474 were individually inspected at original resolution. Exact per-frame ledger distinguishes these levels. All remaining frames and asset reviews remain open.

Also unfinished: current Windows-player acceptance, complete native3D LAN racing, authored pit crew/tool animation and audio, remaining native startup/shutdown failures, weather/physics calibration and exhaustive mesh/texture integration.

New matched alpha-policy comparison41101053265 is queued: the current renderer incorrectly treats any texture alpha as cutout, which may confuse smoothness masks with transparency. Candidate classification remains opt-in until actual captures are reviewed. Fresh Windows candidate build41101404066 is queued/running; export success will not mean the whole game is complete.
