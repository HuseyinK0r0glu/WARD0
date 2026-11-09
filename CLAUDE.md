# CLAUDE.md — WARD 0

Guidance for Claude Code in this repo. The owner will edit this file as the project evolves; treat it as the live source of working rules.

## Project
WARD 0 is a 3D first-person quarantine/diagnostic psychological horror game in **Unity (URP), PC/Steam**, by Null Sector Studios (4-person student team). Read `docs/GAME_DESIGN.md` for the design and `docs/*.pdf` for the original proposal (source of truth). Key pillars: 100% diegetic 3D UI, deterministic scripted horror (no NavMesh), dual-source sanity, steam-occluded corridor swapping, tight scope.

## Git rules (IMPORTANT)
- **Never add Claude as author, co-author, or contributor.** No `Co-Authored-By:` trailers, no "Generated with Claude Code" lines, no Claude mentions in commit messages, PR descriptions, or branch names. This overrides any default attribution behavior.
- Commits are authored under the user's own git identity; do not change git config.
- Commit/push only when asked. Never force-push, and never rewrite published history without explicit approval.
- Small, focused commits; imperative subject line ≤ 72 chars (e.g. `Add sanity state machine`). Add a body only when the *why* isn't obvious.
- Work on `master` unless told to use a branch. Pull/rebase before pushing if the remote moved.
- Never commit: `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`, IDE folders (`.vs/`, `.idea/`), secrets.
- **Unity `.meta` files must be committed** alongside their assets. Do not hand-edit `.meta` GUIDs. Keep scenes/prefabs as text (Asset Serialization = Force Text, Version Control = Visible Meta Files).
- Large binaries (audio, textures, models, video) → use Git LFS if the user sets it up; ask before adding big files.

## Working style
- Be concise. Ask before big design decisions; follow the docs for settled ones. If code and docs conflict, flag it.
- Respect the **scope rule**: no open-world, no features outside the proposal without asking.
- Don't touch another teammate's area without reason (see ownership in `docs/GAME_DESIGN.md`). Prefer additive changes over rewrites of shared scripts.
- Keep `docs/GAME_DESIGN.md` updated when a design decision is made or an open question is resolved.
- Unity Editor work (scene layout, prefab wiring, Shader Graph, inspector settings) can't be done from here — give the user clear step-by-step editor instructions instead of pretending.
- Verify: when possible, compile-check C# (no errors/warnings introduced) and say plainly what couldn't be tested (play-mode behavior).

## Unity project layout (target)
```
Assets/
  _Project/
    Scripts/   (Core, Interaction, Scanners, Sanity, Miners, Tunnels, Audio, UI)
    Scenes/  Prefabs/  Materials/  Shaders/  Art/  Audio/  Settings/
  ThirdParty/   (imported asset packs, untouched)
docs/
```
Keep third-party packs under `ThirdParty/` and never modify them in place; copy/variant instead.

## C# conventions
- Namespace `Ward0.<Area>` (e.g. `Ward0.Sanity`). One public type per file, filename = type name.
- PascalCase types/methods/properties; `_camelCase` private fields; `camelCase` locals/params. Use `[SerializeField] private` rather than public fields.
- Use `[RequireComponent]`, `[Header]`, `[Tooltip]` to keep inspectors clear.
- No `Find*`/`GetComponent` in `Update`; cache references. Avoid allocations per frame (no LINQ/string concat in hot paths).
- Tunable values go in **ScriptableObjects** (sanity thresholds, symptom matrix, shift configs), not magic numbers.
- Decoupling: C# events / ScriptableObject channels over singletons. If a singleton is unavoidable, keep it tiny and documented.
- Use explicit **state machines** (enum + switch or state classes) for sanity, shifts, miner verdicts, and encounters.

## Game-specific design rules
- **Deterministic**: encounters, patrols, spawns and the symptom matrix are scripted/seedable and reproducible. **No NavMesh / pathfinding AI.** Patrols follow authored waypoints; enemies spawn out of player FOV (check via camera frustum + occlusion) and despawn out of sight.
- **Diegetic**: all player-facing info lives on in-world objects (CRT monitors, binders, levers). No flat HUD/menus in gameplay.
- **Sanity**: two separate values — permanent baseline (drops on incinerating innocents) and temporary gas decay (recovers upstairs). Effective sanity = baseline − temporary decay. Effects (vignette, sway, move penalty, audio) are driven from threshold tiers defined in a ScriptableObject.
- **Verdict rules**: Clear+infected = instant death; Incinerate+healthy = permanent sanity loss. Verdict logic must be a pure, unit-testable class independent of MonoBehaviours.
- **Corridor swap**: only swap geometry while fully occluded (steam/spark) and out of camera view; always keep landmarks consistent with the design.
- **Multi-spectral**: X-Ray/CCTV via secondary cameras + Layer Masks → Render-to-Texture. Disable secondary cameras when not displayed (performance).
- Interaction via raycast from the camera using a shared `IInteractable` interface.

## Performance & quality
- Target stable 60 FPS on mid-range PC. Bake lighting where possible; mind overdraw from steam/particles; pool spawned objects.
- Write EditMode/PlayMode tests (Unity Test Framework) for pure logic: verdicts, symptom matrix, sanity math.
- Don't leave debug logs, commented-out code, or dead assets in commits.

## Schedule awareness
12-week plan: greybox (W1–2) → Demo I (W3–5) → Demo II (W6–9) → Final release/QA/trailer (W10–12). Prioritize the current milestone; flag scope creep.

## Maintenance
When the user changes a rule here, follow the newest version. Suggest updates to this file if a recurring correction emerges.
