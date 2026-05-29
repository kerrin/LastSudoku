The full plan can be read in temp_radial_menu_plan.md if you need more context.

Stage 1: Manual Edit Core (no UI)
Goal: Add domain-level actions for set value, add candidate, remove candidate, and smart action resolution scaffold, all with proper changelog grouping and undo/redo compatibility.

Start new chat with this text:
Stage 1 - Manual edit core only.
Implement manual cell edit actions for SetValue, AddCandidate, RemoveCandidate, and SmartAction resolution skeleton.
Requirements:
* Reuse existing board mutation behavior so peer candidate cleanup is preserved.
* Record each manual edit as one atomic change group in the board changelog.
* Ensure undo and redo work for these manual edits exactly like rule-based edits.
* Add editor tests for manual edit changelog records and undo/redo behavior.
Do not add input handling or radial UI yet.


Stage 2: Cell Hold Interaction (no radial UI)
Goal: Detect left-click hold on a cell and resolve pointer position to row/column reliably.

Start new chat with this text:
Stage 2 - Cell hold interaction only.
Add board interaction so left mouse down on a cell starts a hold timer, with a configurable threshold to trigger radial open intent.

Requirements:
* Map pointer screen position to board row and column using existing board geometry.
* Track selected cell and hold lifecycle states cleanly.
* Add safe handling for pointer leaving board and early release.
* Keep behavior disabled for invalid or missing board state.
Do not build radial visuals yet.


Stage 3: Radial Menu Shell
Goal: Show/hide the radial menu with correct segment layout and selection state, but no mutation execution yet.

Start new chat with this text:
Stage 3 - Radial shell only.
Create the radial menu UI that opens on hold.
Requirements:
* 10 outer segments: top is no action, then clockwise digits 1 through 9.
* Center segment reserved for Smart action with dynamic label text.
* Segment hover or highlight state while pointer moves.
* On release, return selected segment identity without applying board edits yet.
Keep this stage focused on visuals, layout, and selection plumbing.


Stage 4: Numeric Segment Actions
Goal: Wire digit segments to three operations: set value, remove candidate, add candidate.

Start new chat with this text:
Stage 4 - Numeric action wiring.
Wire radial digit selection to action choice and execution.
Requirements:
* Each digit supports SetValue, RemoveCandidate, and AddCandidate.
* Execute through the manual edit core so changelog and undo/redo stay consistent.
* Prevent edits on given cells and show a clear no-op outcome.
* Refresh affected runtime UI state after edits.
Do not expand Smart action logic beyond placeholder behavior yet.


Stage 5: Smart Center Action
Goal: Implement deterministic smart behavior based on current cell state.

Start new chat with this text:
Stage 5 - Smart center action.
Implement the center Smart action with deterministic rules.
Requirements:
* Minimum behavior: if unsolved cell has exactly one candidate, set that value.
* If no valid smart action exists, perform safe no-op with clear feedback.
* Ensure smart edits use the same changelog grouping path as manual numeric actions.
* Add tests for smart action success and no-op cases.
Keep smart logic intentionally small and reliable for now.


Stage 6: Validation and Polish
Goal: Confirm no regressions and improve UX consistency.

Start new chat with this text:
Stage 6 - Validation and polish.
Finalize radial editing with tests and UX cleanup.
Requirements:
* Add coverage for segment mapping correctness and hold-open-release flow.
* Verify changelog navigation, undo/redo, and existing solver UI behavior still work.
* Confirm interaction edge cases: release outside menu, cancel path, missing board state, given cells.
* Apply small UX polish only after behavior is stable.
Do not add new major features in this stage.

Scope Boundaries for This Plan
* Included: manual cell edits, candidate edits, hold-to-open radial, smart center action MVP.
* Excluded for now: advanced smart heuristics, animation-heavy polish, solver-strategy automation beyond explicit user action.