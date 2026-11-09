# [Performance] Game stutters / freezes and runs slowly in play mode

**Type:** Performance  **Priority:** P1  **Milestone:** Demo II (W6-9)
**Area:** Mine tunnels / corridor swap, Sanity / post-processing, Build / editor tooling
**Suggested owner(s):** Hasan Can Zorlu, Fikret Karakuzu
**Labels:** performance, P1

## Problem / context
While playing `Ward0_Map` the game sometimes freezes, movement becomes impossible, or it runs very slowly. Not yet measured, so the causes below are suspected, not confirmed. Unity's Console also shows: "...resolution by 4 to make 24 shadow maps fit in the 2048x2048 shadow atlas".

Suspected causes (most likely first):
1. `MapBuilder` bakes the whole level (both floors, every chamber) into one mesh per material, so nothing can be culled and every shadow-casting light redraws all of it.
2. Too many shadow-casting realtime lights (24 shadow maps reported); only the 4 nearest lamps are meant to cast shadows, but upstairs panel lights and the torch add more.
3. About 22 tunnel lamps + torches + station lights all lighting the same objects.
4. Large soft transparent particles (gas, steam, fire) cause heavy overdraw.
5. Editor overhead (Game view scale, Gizmos, Scene tab open).
6. Post-processing stack (SMAA, bloom, grain, chromatic aberration, vignette, lens distortion).
7. First-use generation of procedural audio clips causes short hitches.

## Steps to reproduce
1. Open `Assets/_Project/Scenes/Ward0_Map.unity`, press Play.
2. Walk around the checkpoint, then descend to the mine and move between chambers (the gas chamber and the hub are expected to be the worst).

Expected: stable 60 FPS on a mid-range PC.
Actual: freezes / low FPS (record numbers below).

## Proposed change
- [ ] **Measure first:** open Game view -> Stats and record FPS, Batches, SetPass calls, Tris per area (booth, hub, gas chamber), in the editor and in a Windows build.
- [ ] **Split the baked map into per-room meshes** (chambers, corridors, checkpoint rooms) so frustum culling works. (Recommended first step.)
- [ ] **Cap shadow-casting lights to 1-2** and raise the shadow atlas size in the URP asset to remove the warning.
- [ ] **Distance-cull lights and fade particles** beyond ~25 m.
- [ ] **Add a Quality preset** that scales shadows, particles and post effects.
- [ ] **Test in a Windows build**, not only the editor.
- [ ] Pre-generate procedural audio clips at load to remove first-use hitches.

## Acceptance criteria
- [ ] Baseline and after numbers recorded in this issue.
- [ ] 60 FPS sustained in the booth, hub and gas chamber on the target PC (build and editor Game view).
- [ ] No shadow-atlas warning in the Console.
- [ ] No hitch > 50 ms during a full shift.

## Open questions
- What is the target minimum spec (GPU / CPU)?
- Is a lower-quality fallback preset acceptable for the Steam release?

## Notes / links
`Game/Assets/_Project/Editor/Desk/MapBuilder.cs` (`BakeMap`), `Game/Assets/_Project/Scripts/Gameplay/LampBudget.cs`, `docs/GAME_DESIGN.md` section 11.
