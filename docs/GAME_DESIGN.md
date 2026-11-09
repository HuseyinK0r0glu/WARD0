# WARD 0 — Game Design Overview

Derived from *WARD 0 – Written Game Pitch & Project Proposal* (PDF) and the *GameTech Project Proposal & Pitch* slide deck (PPTX), both in this folder (Null Sector Studios, October 2026). These are the source of truth; if this file and them disagree, flag it and ask.

> *One reinforced glass window separates you from a neurotoxic mining nightmare.*

| | |
|---|---|
| **Studio** | Null Sector Studios (4 senior CE students, GameTech course) |
| **Genre** | 3D first-person deep-mine quarantine & diagnostic psychological horror |
| **Engine** | Unity 3D, Universal Render Pipeline (URP) |
| **Platform** | PC (Steam) |
| **Audience** | Mature PC gamers 18–34, horror streamers (Outlast / Mortuary Assistant fans) |
| **Scope rule** | All detail goes into one armored checkpoint + compact, task-specific modular mine tunnels. **No open world.** |

## 1. Premise

A neurotoxic gas leak in a deep underground mine has turned miners into violent, paranoid hosts. You are the **shift technician** at the armored quarantine checkpoint. Evacuated miners arrive **one by one** in the quarantine cell across from your desk, behind reinforced glass. Decide who is healthy and who is infected — and live with the consequences.

## 2. Core Loop (two tiers)

### Upper tier — Quarantine triage (safe booth)
- Miners are presented behind glass. Inspect them with **100% diegetic 3D instruments** (no 2D menus):
  - **X-Ray** — internal skeletal anomalies (secondary camera + layer culling mask → Render-to-Texture on CRT monitors)
  - **Thermal** — abnormal organ heat signatures (Shader Graph)
  - **UV** — sub-dermal neurotoxic veins (Shader Graph)
  - **ECG** — heart-rhythm readout
- Optionally **enter the cell via the airlock to draw a blood sample** (close-quarters, high tension, micro-jumpscares).
- Pull a verdict lever: **Clear** or **Incinerate**.

### Verdict consequences
| Verdict | Miner is infected | Miner is healthy |
|---|---|---|
| **Clear** | **Instant death** (fatal ambush, game over) | Correct, miner passes |
| **Incinerate** | Correct | **Permanent sanity loss** |

Miner symptoms come from a **deterministic C# symptom matrix** (not random noise) so a careful player can always deduce the truth.

### Lower tier — Mine maintenance
- During facility breakdowns, descend by elevator into **task-specific modular mine tunnels** to repair valves and breakers.
- **Steam/spark occlusion:** a dense burst blocks the player's view while scripts silently **swap the rear corridor prefab** (non-Euclidean). The player must return using **landmarks** (minecarts, chalk marks, torches, stalactites).
- **Scripted FOV encounters:** delusional miners spawn outside the player's field of view, walk deterministic paths, and despawn out of sight. Player hides in alcoves / behind minecarts. **No NavMesh AI.**
- Return upstairs to resume triage.

## 3. Sanity System (dual-source)

| Source | Effect | Recoverable? |
|---|---|---|
| Incinerating an innocent miner | Lowers sanity **baseline** | **No (permanent)** |
| Toxic mine gas in the tunnels | Temporary decay | Yes, cleared at the booth on returning upstairs |

The deck calls these two sources *guilt* (permanent) and *gas* (temporary). Crossing sanity thresholds triggers escalating URP post-processing and gameplay penalties: vignette, silhouettes, camera sway, heavy movement penalties, and 3D spatial audio hallucinations (whispers, screams, panic breathing).

## 4. Design Pillars

1. **100% diegetic** — every tool is a physical 3D object in the world; zero 2D menus during play.
2. **Deterministic horror** — scripted, reproducible encounters; no fragile pathfinding; zero pathfinding bugs.
3. **Moral consequence** — mistakes are fatal or permanently scarring.
4. **Landmark memory** — spatial disorientation is fought with observation, not minimaps.
5. **Tight scope** — depth over breadth; finish a shippable, bug-free game.

## 5. Technical Plan (Unity URP)

- **Multi-spectral rendering:** secondary cameras + Layer Masks → Render-to-Texture for X-Ray and CCTV feeds; custom UV and Thermal Shader Graphs.
- **State machines:** miner symptom matrix, dual-source sanity, shift progression, scripted encounter sequencing.
- **Raycast interaction:** levers, scanners, valves, breakers, cell door/airlock.
- **Corridor swapping:** C# prefab swap hidden behind steam occlusion.
- **Post-processing:** URP Volume (Vignette, etc.) driven by sanity value; camera sway script.
- **Audio:** 3D spatial audio for hallucinations, breathing, steam bursts.
- **Assets:** curated 3D packs + Mixamo miner animations; AI-assisted coding workflow.

## 6. Team & Ownership

| Member | Role | Focus |
|---|---|---|
| Hasan Can Zorlu | Core Gameplay, 3D Raycast & Camera Lead (deck: *3D Cameras*) | Diagnostic instrument integration, Render-to-Texture CCTV, X-Ray layer culling, sanity-driven camera sway & occlusion |
| Hüseyin Köroğlu | Procedural Systems & State Machine Architect (deck: *Core Logic*) | Symptom matrix, dual-source sanity state machine, scripted FOV enemy sequences, UI/graphic design (logos, protocol binders) |
| Görkem Eren | 3D Environment, Lighting & Level Designer (deck: *3D Scene*) | Checkpoint + modular tunnels, landmarks, steam-occluded prefab swapping, Mixamo rigging |
| Fikret Karakuzu | Technical Artist, Shaders & Audio Designer (deck: *Tech Art*) | Thermal/UV Shader Graphs, URP sanity post-processing, 3D spatial audio |

## 7. 12-Week Plan (WBS)

| Milestone | Owners | Weeks |
|---|---|---|
| 1.0 Proposal, concept, asset selection, greybox | All | W1–2 (done) |
| 2.1 Demo I: armored booth, modular mine tunnels, raycast | Hüseyin & Can | W3–5 (active) |
| 2.2 Demo I: Render-to-Texture CCTV, 3D bio-scanners, cell blood draw | Görkem & Fikret | W3–5 (active) |
| 3.1 Demo II: steam-occluded corridor swap, landmarks, FOV stealth | Can & Görkem | W6–9 |
| 3.2 Demo II: dual-source sanity, URP post-processing, 3D audio | Hüseyin & Fikret | W6–9 |
| 4.0 Final release: shift progression, sanity balancing, QA, trailer | All | W10–12 |

## 8. Visual Identity & Asset Strategy (from the deck)

- **Logo:** industrial mine-stencil typography with a red biohazard icon.
- **Environment:** modular mine pack for the armored booth and task-specific tunnels, with navigation landmarks.
- **Miners:** Outlast-inspired Mixamo rigs for blood draws and FOV patrols.
- **Diegetic feedback:** physical CRT scanners plus URP vignette, sway and 3D whispers — no flat HUD.
- **Smart asset pipeline:** combine existing modular mine and bio-scanner packs with custom shaders.

## 9. Market Context

Comparable titles: *The Mortuary Assistant* ($9.3M+ gross), *Papers, Please*, *Contraband Police*, *The Exit 8* (908K+ copies, $2.9M+). WARD 0's edge: live 3D bio-scanners, dual-source sanity, and steam-occluded corridor swapping on top of the triage-decision formula. The deck also claims the core demographic is 18–34 and that horror is a top-3 streamed indie genre on Twitch.

*Note: the revenue figures in the PDF and PPTX comparison tables are attributed to different rows (e.g. $33.7M / $44.1M). Verify against Steam data before using them publicly.*

## 10. Open Questions (to resolve as we build)

- Exact number of shifts and miners per shift; difficulty curve.
- Sanity thresholds and the numeric values for each penalty tier.
- Full symptom matrix (which scanner reveals which infection sign; false-positive rules).
- Win/lose conditions and ending(s).
- Final asset-pack list.
- Reconcile the competitor revenue figures between the PDF and the deck.

## 11. Playable map (`Game/Assets/_Project/Scenes/Ward0_Map.unity`)

Built entirely from the procedural props via **Ward0 > Build Game Map** (order: Build Quarantine Desk, Build Map Props, Build Game Map).

### Layout
```
 UPPER CHECKPOINT (y = +30)                         MINE (y = 0), 6 chambers + 18 tunnel segments
 +--------+------+----------+                       (N1)=====(N0)=====(N4)
 | ELEV.  | BOOTH|  CELL    |                        ||       || \    ||
 | shaft  | desk |  (miner) |                       (N2)=====(N3)=====(N5)  <- gas chamber
 +--------+  AIRLOCK                                  spur A    N3 hub     spur C (gas)
                                                      spurs B and D: dead ends with batteries
```
- **Upper:** booth with the triage desk, observation glass onto the quarantine cell (incinerator grate, miner spawn), airlock with two blast doors, elevator.
- **Mine:** N0 landing (elevator), N1 breaker hall A, N2 pump house (coolant valve), N3 central hub (pillars, carts), N4 east hall, N5 gas chamber (breaker B). Every chamber has its own landmarks (chalk marks, torches, minecarts, stalactites) for memory navigation. Four dead-end spurs hold spare batteries.

### Shift flow
1. **Triage (3 miners):** power the desk, run X-ray / bioscan / analysis (infected miners show anomalous scans and swing the gauge needles into the red), then CLEAR (toggle) or INCINERATE (lever). Clear an infected miner = fatal ambush. Incinerate a healthy one = permanent sanity loss.
2. **Breakdown:** alarm; the elevator unlocks.
3. **Mine:** repair 3 systems (hold E, loud) inside the reactor timer. Each repair vents a steam burst that blinds you while the corridor behind you collapses (graph-checked so you are never trapped).
4. **Return** to the lower elevator to finish the shift.

### Difficulty levers
- Reactor timer (default 600 s), 3 holds-to-repair that make noise, patrols that scale 1 -> 3 with each repair, corridor collapses that force new routes.
- Patrols: authored graph walk (no NavMesh); detection by FOV + line of sight; faster in light / sprinting, slower crouched or behind cover; noise (sprint, repairs, doors) draws them. A full detection meter is fatal. They spawn and despawn out of your view.
- Darkness: roughly a quarter of tunnel lamps are off and the spurs are unlit; the torch has a limited battery.
- Dual sanity: permanent baseline + temporary gas decay (gas chamber and spur C). Sanity tiers add vignette, grain, chromatic aberration, sway, whispers, slower movement.
- Stamina limits sprinting; crouch + hide zones behind minecarts.

### Controls
WASD move, Shift sprint, Ctrl / C crouch, E interact (hold to repair), F torch, R restart after a result screen, F1 debug overlay, Esc releases the mouse.

### Where to tune
`ShiftManager` (miners, repairs, timer, infected pattern), `SanitySystem` (loss and drain rates), `PatrolEnemy` (speed, view range, detect time), `EnemyDirector` (spawn interval and distance), `PlayerController` (speeds, stamina), `FlashlightController` (battery).
