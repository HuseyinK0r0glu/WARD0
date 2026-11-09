# [Audio] Change the walking (footstep) sound

**Type:** Audio  **Priority:** P2  **Milestone:** Demo II (W6-9)
**Area:** Audio, Player / controls, Patrols / stealth
**Suggested owner(s):** Fikret Karakuzu (audio), Hasan Can Zorlu (player)
**Labels:** audio, P2

## Problem / context
The player's footsteps are a synthesised placeholder (a short filtered-noise thud from `ProceduralAudio.Step`) that is the same on every surface. The team wants the walking sound changed. The exact complaint still needs to be written down (see open questions).

Footsteps also matter for stealth: walking, sprinting and crouching have different noise radii that patrols react to, so the sound must communicate how loud the player is being.

## Steps to reproduce
1. Press Play on `Ward0_Map` and walk, sprint (Shift) and crouch (Ctrl) in the booth and in the mine.
2. Listen to the footsteps.

Expected: footsteps that sound like boots on metal grating upstairs and on gravel / rock in the mine, clearly louder when sprinting and almost silent when crouching.
Actual: one generic thud on every surface.

## Proposed change
- [ ] Decide what is wrong with the current sound (open questions).
- [ ] Replace the generated clip with real sound assets, 3-5 variations per surface (metal floor in the checkpoint, gravel / rock in the mine, optional puddle splash).
- [ ] Pick the clip from the surface the player stands on (tag, material or zone).
- [ ] Randomise pitch and volume slightly; keep sprint / walk / crouch volume tied to `PlayerController.NoiseRadius`.
- [ ] Keep the synthesised sound as a fallback if a clip is missing.

## Acceptance criteria
- [ ] Footsteps differ between the checkpoint floor and the mine floor.
- [ ] Sprint is clearly louder than walk, and crouch is nearly inaudible.
- [ ] No two identical clips play back to back.
- [ ] Audible loudness matches the noise radius that patrols react to.
- [ ] Assets are licensed for commercial Steam release (note the source).

## Open questions
- What exactly should change: tone, loudness, speed (stride rate), variety, or surface feel?
- Record our own samples or use a licensed pack?
- Should sanity (hallucination) tiers alter the footstep sound?

## Notes / links
`Game/Assets/_Project/Scripts/Gameplay/ProceduralAudio.cs` (`Step`), `Game/Assets/_Project/Scripts/Gameplay/PlayerController.cs` (footstep block, `NoiseRadius`).
