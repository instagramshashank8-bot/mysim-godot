# LAN stage — bounded implementation plan (spec only; no code this turn)

Owner: physics-gameplay (planning role for the LAN stage, per the reviewer's staffing requirement).
Status: plan for coordinator sign-off. Nothing here is implemented or validated; the LAN flow remains flagged unstaffed-until-this-plan-is-adopted.

> **Implementation status (updated after the adopted-defaults phase-2 step):** the four decisions were adopted as conservative defaults. Implemented since: the explicit `START|token` handshake (`Wire` v MSR6, `GameSession.StartRace`, client gating on `startReceived`), the logic half of the `RaceSessionBridge` (`src/Logic/RaceSessionSync.cs`: `SnapshotApplier` + `IRaceView`/`ICarView` — one view per car id, updated in place, never recreated per packet), and the join-UI car picker (`LanRoomPanel` → `session.localModel` → HELLO) plus the host START button. NOT yet done (the next small step): the race-scene adapters implementing `IRaceView`/`ICarView` over Godot nodes and the lobby→race-scene transition (touches `Main.cs` — coordinator-owned surface). Unit-verified in CoreRunner (handshake + applier persistence through the real Snapshot→ReadSnapshot→Apply pipeline); no two-instance run yet — the acceptance checklist remains a post-integration battery item.
>
> **Update 2 (host-side integration + test tooling):** the host-side in-race integration is implemented (session adopts the live race; the remote-driven car built at construction from `PeerName`/`PeerModel`; per-tick session + persistent remote visual; the hosting panel lingers until adoption so the client's silence window survives the load). The two-instance test is now SCRIPTABLE: `MYSIM_LAN_HOST=1 MYSIM_LAN_AUTOSTART=1 MYSIM_AUTODRIVE=1 MYSIM_LAN_QUIT_AFTER=45` (host) + `MYSIM_LAN_JOIN=127.0.0.1 MYSIM_LAN_QUIT_AFTER=45` (client) — the executable script, the console expectations, the PASS criteria and the failure fingerprints are in `agents/lan-two-instance-test.md`; the reading rules are card §13. LAN is CODE-COMPLETE for the first two-instance test; everything remains CODE-PRESENT-UNVERIFIED until that run passes.

## 0. What already exists (verified this session — do not rebuild)

| Piece | Where | State |
|---|---|---|
| Transport abstraction | `src/Logic/Network.cs` `IGameChannel` | done — Send/Receive abstraction; anything (UDP, ENet) can implement it |
| LAN transport | `LanChannel` (UDP, port 44880, broadcast enabled) | done, unit-tested (real UDP loopback in CoreChecks) |
| Discovery | `DISCOVER\|<ver>` broadcast → `OFFER\|<ver>\|<host>` | done in logic; `rooms` collected client-side |
| Wire protocol | `Wire` v `MSR5`: `STATE` (host-authoritative snapshot, seq'd), `INPUT` (token-authed, seq'd, replay-rejected), `HELLO/ACCEPT/BUSY/BYE/RESET_ACK` | done, validation-tested (malformed packets rejected, stale input rejected, bounds/enum/quaternion checks) |
| Session logic | `GameSession`: Host/Join/Tick/Process, lobby via `countdown=9999`, disconnect → AI takeover, input-silence → safe defaults (brake) | done, unit-tested |
| Godot lobby UI | `src/LanRoomPanel.cs`: host (track/laps picker → CREATE ROOM), join (address:port → JOIN ROOM), live status | done UP TO "CONNECTED" — deliberately stops there ("LAN race launch is unavailable in this build"; native3D race integration is the separate gate) |
| Race integration | — | ABSENT: nothing transitions a connected lobby into a race scene; no graphical remote car; no results flow |

## 1. Already-decided sync model (the existing protocol IS the decision)

**Host-authoritative state replication + client input replication** — the "inputs vs states" question is already answered in the shipped protocol:

- The HOST simulates everything (its own car natively; the remote car from received `INPUT`) and broadcasts `STATE` at ~15 Hz (66 ms send timer). `STATE` carries per-car: pose (position/velocity/heading + optional body quaternion for externally-simulated bodies), race state (clock/countdown/started), and the full gameplay state (laps, damage regions, pit state, tyres, ERS, finish).
- The CLIENT simulates nothing; it renders from snapshots and sends `INPUT` at the same 15 Hz cadence (throttle/brake/steer/handbrake+reverse flags/pit request/next compound/reset request), token-authed and sequence-guarded.
- Consequences already handled: input-loss safety (host brakes the remote car after 0.5 s of silence), disconnect semantics (AI takeover, `BYE`), no late join (`BUSY` after start), replay/stale rejection.

What replicates: **everything gameplay-relevant is state-replicated from the host; only the local player's controls are input-replicated.** No client-side physics, no determinism requirement across machines.

## 2. Open decisions for the coordinator (the plan's actual asks)

1. **Transport: custom UDP vs Godot ENet.** The working, tested transport is `LanChannel` (raw UDP). Godot high-level multiplayer (ENet) is NOT required by the protocol — `IGameChannel` makes an ENet-backed channel a drop-in later. Recommendation: **ship phase 1-2 on `LanChannel`** (zero protocol risk, already tested), add an ENet `IGameChannel` implementation only as phase-3 hardening if real-LAN testing shows defects (reliability of 1.5-3 KB `STATE` lines on real Wi-Fi, NAT oddities). Rationale: the protocol semantics (tokens, sequencing, validation) are the tested asset; swapping transport under them is low-risk, rewriting them onto RPCs is high-risk and discards test coverage.
2. **Start signal.** There is no explicit start message; a client would have to infer race start from `STATE.countdown` leaving 9999. Recommendation: add an explicit reliable `START|<token>` host→client message (deterministic transition into the race scene), keeping the countdown itself in `STATE` as today. Small protocol addition, versioned (bump `Wire.Version`).
3. **Client rendering model.** MVP = pure snapshot rendering of BOTH cars (client input → host → next snapshot; added latency ≈ 1 RTT + snapshot interval; on LAN ~70-90 ms). No client-side prediction in the MVP; interpolation between the last two snapshots is the phase-3 lever. This must be stated as accepted feel-cost for the minimal race.
4. **Car selection in the lobby.** `HELLO` already carries `localModel`, but the join UI never sets it. MVP: a car picker on the join panel; the host's `ACCEPT` path already applies it (`race.cars[1].carModel=model`).

## 3. Minimal viable LAN race (the bounded scope)

Two cars, one track, sync + results:

- **Host**: lobby (track/laps/car) → CREATE ROOM → (peer joins) → START → race scene with TWO cars: own car through the normal native path, remote car as a native body driven by the received `INPUT` through the same application path the single-player game uses; logic sim fully host-side at the normal rate; `STATE` broadcast at 15 Hz.
- **Client**: join (address or discovered room) → picks car → START received → race scene with the SAME track; BOTH cars rendered from applied snapshots (see §4, the SnapshotApplier); local input through the human chain into `GameSession.localInput`.
- **Finishing**: `finished`/`finishTime` already ride `STATE`; the results UI reads the session's race state on both sides; `BYE`/rematch follows the existing rematch-reset protocol.
- Non-goals for MVP: >2 cars (protocol is 2-slot by construction), spectator mode, WAN/nat traversal, pause, voice/chat, join-in-progress.

## 4. New components required (the actual implementation surface)

1. **`RaceSessionBridge` (Godot side, the core new piece):** owns the `GameSession`, routes the local player's input (post-human-chain) into it in Host/Client roles, and applies received state INTO PERSISTENT SCENE OBJECTS. Critical constraint: `Wire.ReadSnapshot` builds a NEW `Race` per packet — the bridge must never rebuild the scene per packet; it maps car-id → persistent Godot nodes and applies pose/state per snapshot (interpolation later). This is THE central engineering risk of the stage.
2. **Lobby completion (small):** a start button + `START` message (decision 2), a car picker (decision 4), rendering the discovered `rooms` list as tappable entries the client can join directly (the discovery protocol already populates `rooms`; the UI currently wants a typed address).
3. **Remote car rendering:** the remote car is a normal car visual driven by the bridge (pose from snapshot; no local physics on the client, no input application on the host's own car). The host's remote-driven car reuses the existing body/tyre path with the received `DriverInput` — the same application path single-player uses for a human car.
4. **Session lifecycle in the game flow:** menu → LanRoomPanel (exists) → race scene (new) → results → rematch/disconnect handling (mostly protocol-existing; needs the scene-level wiring + UI states).

## 5. Migration risks to the existing single-player flow

| Risk | Mitigation |
|---|---|
| Scene rebuild per snapshot thrashing the rendering/physics scene (new `Race` per packet) | The bridge applies state into persistent nodes; the race SCENE is built once from the first connected snapshot |
| Single-player regression | Everything offline runs exactly as today: the bridge exists only when a session exists; `SessionRole.Offline` code paths untouched; the existing suites (CoreChecks + the DV battery) stay the regression gate |
| Input path divergence | The LAN client/host both feed the SAME human chain (SteeringInput 5.0/s + PedalController) — the LAN input path must not bypass the chain the userpath work validated; the chain state resets at session boundaries like they do at DV profile boundaries |
| Race-start semantics drift | The LAN start must route through the SAME 5 s countdown path (host `countdown=5` at START) so the race-start reading rules (card §9) stay valid for LAN sessions; a LAN variant of the `race_start_sequence` profile is a phase-3 item, not MVP |
| Native body vs logic-vehicle duality on the host's remote car | The snapshot already carries the body-pose fields for externally-simulated cars; the host's remote car is simulated by the SAME native path as single-player (it IS single-player physics with remote inputs) — the risk is only in input application timing, covered by the existing input-silence safety |
| Version skew between builds | `Wire.Version` gate exists and is strict; keep strict (a mismatched build gets `BUSY`/rejection, not a crash) |
| Firewall prompt / port blocked on Windows | Deployment note for the acceptance run: 44880/UDP inbound for the host; document in the run checklist |

## 6. Phase plan

| Phase | Scope | Exit criteria | Owner |
|---|---|---|---|
| 0. Spec (this document) | Decisions 1-4 answered; scope frozen | Coordinator signs the four decisions | me → coordinator |
| 1. Minimal sync | `RaceSessionBridge` (host + client paths), lobby→race transition with 2 cars, both cars moving from real inputs; NO results/departure handling beyond disconnect-safety | Two instances on one LAN: both cars visible and driving; host physics = the native path; CoreChecks green; single-player flow untouched (suite-verified); the session bounded by the existing protocol with NO new wire messages except (maybe) `START` if signed in decision 2 | implementing dev (to staff), me reviewing logic-side |
| 2. Race flow | Start handshake live, results propagation to both UIs, clean teardown (`BYE`), rematch path, mid-race disconnect → AI takeover visible | A complete 2-car race on LAN with a correct result screen on BOTH instances and a clean return/rematch; transcript/log artifact retained | implementing dev, me reviewing |
| 3. Integration hardening | ENet `IGameChannel` IF UDP shows defects; snapshot interpolation if measured latency > 100 ms feels bad; the LAN race-start validation profile; the LAN acceptance checklist becomes a battery item | The acceptance checklist (below) passes end-to-end; charts record it as a verified terminal state only | coordinator dispatches, owner runs |

**Acceptance checklist (phase 3 gate, mechanical):** two fresh instances; host creates (track X, N laps); client discovers/joins; car pickers exercised; start → countdown 5 s → race; both finish; results match on both; client disconnects mid-race in a second run → AI takeover; host quits → clean client message; wire version mismatch → clean rejection; zero single-player regressions (suites green).

## 7. What this plan does NOT claim

No LAN behaviour exists today beyond the lobby handshake (the panel's own label says launch is unavailable). All race-integration behaviour above is to-build. The LAN flow stays flagged unstaffed until phase 1 has a named implementing dev and this plan is signed.
