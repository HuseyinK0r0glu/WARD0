# WARD 0

> *One reinforced glass window separates you from a neurotoxic mining nightmare.*

WARD 0 is a first-person quarantine and diagnostic psychological horror game for PC (Steam), built in Unity 6 with the Universal Render Pipeline. It is a student project by **Null Sector Studios**.

A neurotoxic gas leak has turned the miners of a deep mine into violent, paranoid hosts. You are the shift technician at an armored quarantine checkpoint. Miners arrive one at a time in the cell behind the glass. Scan them at your desk, then **clear** the healthy or **incinerate** the infected. Clear an infected miner and it breaks through; incinerate a healthy one and you lose sanity for good. When the checkpoint fails, you go down into the mine to repair it.

## Features

- Diegetic interface: scanners, gauges and levers on a physical desk, plus a minimal on-screen guide that can be switched off with `H`.
- Two-source sanity system: a permanent baseline that drops with wrong verdicts, and temporary gas decay that recovers upstairs.
- Deterministic scripted horror: authored patrols on a chamber graph, no NavMesh, seedable and reproducible.
- Streamed mine: each chamber is its own additive scene, loaded when the player approaches and unloaded behind them.
- Procedural art: props, characters, textures and audio helpers are generated from code (see below).

## Controls

| Key | Action |
|---|---|
| `W` `A` `S` `D` | Move |
| `Space` | Jump |
| `Shift` | Sprint (loud, limited stamina) |
| `Ctrl` / `C` | Crouch (quiet, lets you hide) |
| `E` | Interact (hold to repair) |
| `F` | Torch |
| `V` | Toggle third-person view of your own technician |
| `H` | Toggle hints |
| `R` | Restart after a result screen |
| `F1` | Debug overlay |
| `Esc` | Release the mouse |

## Getting started

Requirements: **Unity 6000.3.9f1** (URP template) and Git.

1. Clone the repository.
2. Open the **`Game/`** folder as the Unity project (not the repository root).
3. Open `Game/Assets/_Project/Scenes/Ward0_Map.unity` and press Play.

If the scenes or props look out of date, regenerate them from the `Ward0` menu, in this order:

1. `Ward0 > Build Quarantine Desk`
2. `Ward0 > Build Map Props` (also builds the hand tools; `Ward0 > Characters > Build Characters` builds the characters)
3. `Ward0 > Build Game Map`

The level scene and the zone scenes are generated. Change the builders in `Game/Assets/_Project/Editor/Desk/` and rebuild instead of editing the generated assets.

## Repository layout

```
Game/                         Unity project
  Assets/_Project/
    Scripts/Gameplay/         Game logic (namespace Ward0.Gameplay)
    Editor/Desk/              Procedural builders for props, characters and the map
    Scenes/                   Ward0_Map (core) and Zones/ (mine chambers)
    Audio/Footsteps/          Recorded footsteps (CC0, see CREDITS.txt)
docs/                         Game design document, original pitch (PDF, PPTX), issue drafts
CLAUDE.md                     Working rules for AI-assisted development
```

Start with [`docs/GAME_DESIGN.md`](docs/GAME_DESIGN.md) for the design, the shift flow, performance notes and tuning values.

## Team

Null Sector Studios: Hasan Can Zorlu, Hüseyin Köroğlu, Görkem Eren and Fikret Karakuzu.

## Credits and licences

- Footstep recordings are CC0 from OpenGameArt; sources are listed in `Game/Assets/_Project/Audio/Footsteps/CREDITS.txt`.
- All models, textures and other audio are generated procedurally in code and contain no third-party content.
