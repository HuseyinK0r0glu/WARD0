# [Art] Create high-quality human 3D character models (miners and infected)

**Type:** Art / assets  **Priority:** P1  **Milestone:** Demo II (W6-9)
**Area:** Quarantine cell / airlock / doors, Patrols / stealth
**Suggested owner(s):** Görkem Eren (3D / Mixamo rigging), Fikret Karakuzu (shaders / tech art)
**Labels:** art, P1

## Problem / context
Every human in the game is currently a placeholder built from primitives (capsules, spheres, boxes): the miners shown in the quarantine cell (`MinerFigure`) and the delusional patrols in the mine (`InfectedPatrol`). They have no faces, no real clothing, no animation clips and do not sell the horror. The triage loop depends on reading a person through glass (twitching, eyes, skin), and the stealth loop depends on a threatening, readable silhouette in the dark.

## Steps to reproduce
1. Press Play on `Ward0_Map`, finish nothing yet and look through the observation glass at the miner in the cell.
2. Descend to the mine and look at a patrol.

Expected: believable, unsettling human characters with faces, clothing and animation.
Actual: blocky primitive dummies.

## Proposed change
Characters needed:
- [ ] **Miner subjects (healthy):** 3-5 distinct people (age, build, skin tone, clothing wear) for the triage cell.
- [ ] **Infected variants:** the same bodies with neurotoxic signs (sub-dermal veins, discolouration, bloodshot / glowing eyes, torn clothing). Prefer one shared body with a blendable infection material over duplicate meshes.
- [ ] **Patrol enemy:** 1-2 menacing delusional miners readable as a silhouette in near-dark.
- [ ] Optional: technician hands / arms visible on the desk.

Technical requirements:
- [ ] Humanoid rig compatible with Mixamo / Unity Humanoid; clean A or T pose.
- [ ] Poly budget: about 12-20k tris for close-up (cell) characters, 6-10k for patrols, plus 1-2 LODs.
- [ ] PBR textures (albedo, normal, metallic-smoothness, emission for eyes / veins) working with URP Lit; 2K max for hero, 1K for patrols.
- [ ] Animation set: idle, nervous idle, head twitch, walk, limp walk, look-around, lunge / ambush, burn / collapse. Reuse Mixamo clips where possible.
- [ ] Keep gameplay hooks: `MinerSubject` needs head / arms / body transforms and eye renderers; `PatrolEnemy` needs arm / body references and eye-glow light. Update `PropKit` (or replace the prefabs) without breaking the scene wiring.
- [ ] Heads must look acceptable through the observation glass at 3-6 m and lit by cold panel light; patrols must work lit only by torch / lamp.

Source options (decide in comments):
1. Licensed asset pack plus our own retexture (fastest).
2. AI-assisted generation (e.g. Meshy) followed by retopology and rigging.
3. Hand-modelled in Blender (highest effort).

## Acceptance criteria
- [ ] At least 3 healthy miner variants and their infected versions are in the cell rotation.
- [ ] Patrol enemy uses the new model with animations, in both the corridor and chamber lighting.
- [ ] Characters pass a close-up check in the cell (no stretched textures, no clipping hard hats).
- [ ] No frame-rate regression (see the performance issue); LODs in place.
- [ ] All assets are licensed for commercial Steam release (record the source / licence).
- [ ] `docs/GAME_DESIGN.md` visual identity section updated with the final character direction.

## Open questions
- Realistic or slightly stylised proportions? Reference images to follow.
- How graphic should the infection look (rating target)?
- Do we want gendered / ethnic variety among the miners?
- Hand-made vs purchased vs AI-generated base meshes (budget and time)?

## Notes / links
Placeholders built in `Game/Assets/_Project/Editor/Desk/PropKit.cs` (`Miner`). Gameplay scripts: `MinerSubject.cs`, `PatrolEnemy.cs`. Related: performance issue (LODs), team roles in `docs/GAME_DESIGN.md`.
