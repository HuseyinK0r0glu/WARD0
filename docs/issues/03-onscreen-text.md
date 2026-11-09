# [UI / text] Rewrite the on-screen messages and add step-by-step guidance

**Type:** UI / text  **Priority:** P2  **Milestone:** Demo II (W6-9)
**Area:** HUD / on-screen text, Triage desk / scanners
**Suggested owner(s):** Hüseyin Köroğlu (UI/UX), Hasan Can Zorlu
**Labels:** ui-text, P2

## Problem / context
The on-screen wording is unclear in places and new players do not know what to do. Examples seen in testing: the elevator message ("The elevator is locked until triage is done.") was misread by a player who then looked for something that does not exist; the breakdown message ("FACILITY BREAKDOWN. Take the elevator down and restore 3 systems.") and the other prompts need to explain the objective and the next step more clearly. Nothing currently teaches how to use the scanners.

Current messages live in `ShiftManager`, `HudOverlay` and the interactable scripts (prompts, objective line, verdict feedback, controls hint, result screens).

## Steps to reproduce
1. Start `Ward0_Map`, try the elevator before finishing triage, then finish three miners and read the breakdown message.
2. Try the scanner toggles without being told the order.

Expected: every message says what happened, why, and what to do next, in the game's tone (cold, clinical).
Actual: terse messages, no scanner guidance, ambiguity about the elevator.

## Proposed change
- [ ] Audit every player-facing string (objective line, prompts, verdict feedback, breakdown / alarm, repair / collapse messages, result screens, controls hint).
- [ ] Rewrite them for clarity and tone, including the "FACILITY BREAKDOWN" message.
- [ ] Add short contextual hints for the scanner flow: POWER first, then X-RAY / BIOSCAN, then ANALYSIS, then CLEAR or INCINERATE.
- [ ] Add a clear elevator hint and objective marker after the breakdown.
- [ ] Move all strings into one place (a ScriptableObject or one static class) so they are easy to edit and localise.
- [ ] Keep gameplay UI diegetic where possible; use the overlay only for feedback.

## Acceptance criteria
- [ ] A new player can finish the triage without outside help.
- [ ] The elevator and breakdown messages state the next required action.
- [ ] All strings come from one file; no hard-coded messages remain in gameplay scripts.
- [ ] Text is readable at 1080p and at the Game view's smallest size.

## Open questions
- Which exact messages does the team want changed (list them in a comment)?
- English only, or prepare for Turkish too?
- Tutorial hints always on, or an option to turn them off?

## Notes / links
`Game/Assets/_Project/Scripts/Gameplay/ShiftManager.cs`, `HudOverlay.cs`, `DeskToggle.cs`, `DeskLever.cs`, `ElevatorButton.cs`, `RepairStation.cs`.
