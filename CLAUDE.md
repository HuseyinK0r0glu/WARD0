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

## Unity project
- Unity project root is **`Game/`** (repo root holds `docs/`, `CLAUDE.md`, `.gitignore`). Editor **6000.3.9f1**, URP template. Run Unity commands with `Game/` as the project path.
- Layout target:
```
Game/Assets/
  _Project/
    Scripts/   (Core, Interaction, Scanners, Sanity, Miners, Tunnels, Audio, UI)
    Scenes/  Prefabs/  Materials/  Shaders/  Art/  Audio/  Settings/
  ThirdParty/   (imported asset packs, untouched)
```
Keep third-party packs under `ThirdParty/` and never modify them in place; copy/variant instead.

## Unity CLI (`unity`, installed at `~/AppData/Local/Unity/bin`)
Package `com.unity.pipeline` connects the CLI to the open editor. Prefer the CLI over guessing.
- `unity status` — is the editor connected (port, project, state)?
- `unity list` / `unity command <cmd>` — list/run editor commands (recompile, play/pause, capture game_view/scene_view, component/script/scene ops). Run `unity list` to see what exists before using.
- `unity test Game` — run EditMode/PlayMode tests; `unity build Game` — batch build.
- `unity logs`, `unity doctor` — diagnostics. Add `--json` for machine-readable output.
- After editing C# files, trigger a recompile and read console errors before calling a task done.
- If the editor isn't open (`unity status` empty), say so; use `unity open Game` only if the user agrees. Batch-mode commands fail if the same project is open in the editor.
- MCP server `unity-editor-mcp` (`unity mcp`) exposes the same editor to Claude Code if configured.

## Procedural art pipeline
Props are built from code, not hand-modelled. Editor scripts live in `Game/Assets/_Project/Editor/Desk/` (`MeshKit`, `TexKit`, `DeskBuilder`, `ShowcaseBuilder`). Menu `Ward0 → Build Quarantine Desk` regenerates meshes, textures, materials and the prefab; `Ward0 → Build Desk Showcase` rebuilds the review scene. Change the builder, not the generated assets. Designs are authored front-toward +Z and mirrored in Z at build time (Unity is left-handed), so the finished prop faces -Z. Review with `unity command capture game_view --source camera --output-path <file>` and look at the image before calling a prop done. Reference art lives outside the repo (user's Downloads).

## Gameplay layer (`Game/Assets/_Project/Scripts/Gameplay`, namespace `Ward0.Gameplay`)
- `ShiftManager` is the single state machine (Triage > Descent > Mine > Return > Complete/Failed). `MineMap` / `MineNode` / `MineCorridor` is the chamber graph used by patrols and corridor swaps. `PatrolEnemy` + `EnemyDirector` are deterministic graph walkers (no NavMesh). Interaction goes through `Interactable` + `Interactor`. Sanity lives in `SanitySystem`. Input uses the new Input System directly (`Keyboard.current`).
- The level scene is **generated**: run `Ward0 > Build Quarantine Desk`, then `Build Map Props`, then `Build Game Map`. Do not hand-edit `Ward0_Map.unity`; change `MapBuilder` / `PropKit` and rebuild.
- Testing: enter play mode through the CLI (`unity command editor.playmode start`), turn on `editor.playmode set_run_in_background true` so the editor ticks while unfocused, and drive the game with throwaway scripts kept OUTSIDE `Assets/` (e.g. `Game/DevScripts/`, git-ignored) via `unity command script run --file ... --entry Class.Method`. Editing anything under `Assets/` while in play mode triggers a domain reload that nulls statics and breaks the run.
- Player is on the Ignore Raycast layer so interaction rays pass through it; enemy line-of-sight uses all layers.

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
