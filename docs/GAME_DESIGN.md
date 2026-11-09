# WARD 0 — Game Design Overview

Derived from *WARD 0 – Written Game Pitch & Project Proposal* (Null Sector Studios, October 2026). The PDF in this folder is the source of truth; if this file and the PDF disagree, flag it and ask.

| | |
|---|---|
| **Studio** | Null Sector Studios (4 senior CE students, GameTech course) |
| **Genre** | 3D first-person deep-mine quarantine & diagnostic psychological horror |
| **Engine** | Unity 3D, Universal Render Pipeline (URP) |
| **Platform** | PC (Steam) |
| **Audience** | Mature PC gamers 18–34, horror streamers (Outlast / Mortuary Assistant fans) |
| **Scope rule** | All detail goes into one armored checkpoint + compact, task-specific modular mine tunnels. **No open world.** |

## 1. Premise

A neurotoxic gas leak in a deep underground mine has turned miners into violent, paranoid hosts. You are the **shift technician** at the armored quarantine checkpoint, evaluating evacuated workers through reinforced glass. Decide who is healthy and who is infected — and live with the consequences.

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
| Toxic mine gas in the tunnels | Temporary decay | Yes, on returning upstairs |

Crossing sanity thresholds triggers escalating URP post-processing and gameplay penalties: vignette, silhouettes, camera sway, heavy movement penalties, and 3D spatial audio hallucinations (whispers, screams, panic breathing).

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
| Hasan Can Zorlu | Core Gameplay, 3D Raycast & Camera Lead | Diagnostic instrument integration, Render-to-Texture CCTV, X-Ray layer culling, sanity-driven camera sway & occlusion |
| Hüseyin Köroğlu | Procedural Systems & State Machine Architect | Symptom matrix, dual-source sanity state machine, scripted FOV enemy sequences, UI/graphic design (logos, protocol binders) |
| Görkem Eren | 3D Environment, Lighting & Level Designer | Checkpoint + modular tunnels, landmarks, steam-occluded prefab swapping, Mixamo rigging |
| Fikret Karakuzu | Technical Artist, Shaders & Audio Designer | Thermal/UV Shader Graphs, URP sanity post-processing, 3D spatial audio |

## 7. 12-Week Plan (WBS)

| Milestone | Owners | Weeks |
|---|---|---|
| 1.0 Proposal, concept, asset selection, greybox | All | W1–2 (done) |
| 2.1 Demo I: checkpoint, task-specific tunnels, raycast | Hüseyin & Can | W3–5 |
| 2.2 Demo I: Render-to-Texture CCTV, 3D bio-scanners, cell blood draw | Görkem & Fikret | W3–5 |
| 3.1 Demo II: steam-occluded corridor swap, landmarks, FOV stealth | Can & Görkem | W6–9 |
| 3.2 Demo II: dual-source sanity, URP post-processing, 3D audio | Hüseyin & Fikret | W6–9 |
| 4.0 Final release: shift progression, sanity balancing, QA, trailer | All | W10–12 |

## 8. Market Context

Comparable titles: *The Mortuary Assistant*, *Papers, Please*, *Contraband Police*, *The Exit 8*. WARD 0's edge: live 3D bio-scanners, dual-source sanity, and steam-occluded corridor swapping on top of the triage-decision formula.

## 9. Open Questions (to resolve as we build)

- Exact number of shifts and miners per shift; difficulty curve.
- Sanity thresholds and the numeric values for each penalty tier.
- Full symptom matrix (which scanner reveals which infection sign; false-positive rules).
- Win/lose conditions and ending(s).
- Final asset-pack list.
