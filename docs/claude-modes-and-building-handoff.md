# Claude continuation: opening modes, prisoner lives and faster building

The user assigned Claude richer prisoner lives and the new opening-mode/HUD direction. Codex's security/services checkpoint `a6d2afc` is the integration base. Fetch the latest `codex/live-operations-handoff`, preserve your own commits and unfinished work, and read COORDINATION.md. Do not reset your branch. This handoff becomes active when the published ownership table reserves `opening-and-build-ui` for Claude and you acknowledge/claim that scope.

## Concrete file handoff

Claude may change these existing files after that claim:

- `Assets/Cellblock/Core/Simulation.cs` — scenario/contract setup, offer lookup and validation, new-game state wiring. Preserve the existing construction, ledger, save discrimination, explicit minute sequence and security advancement.
- `Assets/Cellblock/Runtime/GameHud.cs` and `Assets/Cellblock/Resources/Cellblock.uss` — opening menu, catalog, quote and layout redesign. Preserve the operations/security/justice/lives partial integrations and stable touch controls.
- `Assets/Cellblock/Runtime/GameController.cs` and `ConstructionGuides.cs` — scenario selection and placement input. Preserve physical camera/pinch ownership, UI input blocking, repeat placement, real quote/revalidation and save isolation.
- New mode/configuration/HUD files, new Lives files already listed in `claude-lives-handoff.md`, their metas, new scoped checks and notes.

Codex retains Operations.cs, Security.cs, Connections.cs, geometry/navigation, catalog/prefab data, existing facility/worker/security renderers, justice integration, and every Unity/editor/build run. Request a specific narrow handoff for additional existing files. Codex will not edit the five handed-off files while your claim is active. Keep scope updates and milestones in your own coordination log.

## Opening design

Use Federal / State / City / County as the first mode choice. Follow with a compatible facility/security profile; do not make every federal start maximally difficult. A federal camp and federal supermax should differ in actual building, legal roster, temperament distribution, staffing, service obligations, contract economics and security constraints.

Recommend one approachable profile, while preserving the ability to choose another profile and a city. A seeded random eligible city should be an explicit choice with a visible seed or reproducible identity. The player should see a clear preview of the facility, population, budget/reserve, obligations and challenge before committing to a company.

Keep the old county/rehab/secure contract IDs and existing saves valid. Persist the new jurisdiction/profile/city/seed. Resume must reproduce the chosen configuration without reseeding names, charges, buildings or purchases. Gameplay consequences must use those selected settings; menu labels alone are insufficient. Keep fictional charge/temperament pools authored and transparent rather than mapping personal demographics to risk.

## Building and HUD direction

The user wants a faster, clearer city-builder-style flow with an original identity. Build on the current working camera, catalog and real geometry. Useful next steps: catalog-to-world drag, selected-item move drag, snapped valid/invalid ghost with price, rotate handle/twist, drop-to-quote and optional grouped placements. Retain one-tap selection and visible touch alternatives. Drags starting on UI must not pan/place through the panel; cancelled gestures must not charge. Quotes must revalidate against cash, pending work, access and occupied routes before approval.

Already available: 23 fixture definitions; six prefab types; Shell/Essential/Enhanced packages including services; full-draft shared-wall connection; upgrade quotes with stable installed IDs; **Approve & place another**, **Z** rotation and inspector folding during quotes. Preserve these in the redesign.

## Personnel / justice integration warning

The latest observed personnel core is `89834a8` on `codex/claude-personnel`; it is not integrated. Its older Justice base predates the shared save/presence/release fixes and physical officer dispatch. Preserve it and report readiness; do not overwrite current Justice.cs wholesale. Active escape response and security posts must have a single duty target owner. Do not let a later personnel tick silently replace a tower/K9/armed assignment or a detected-escape response. Request a duty-target hook when needed.

## Required handback

Use a focused read-only critic. Commit scoped milestones, run relevant core checks and offline Unity API checks if available, push your branch, and report SHA, changed files, validation, limitations and a deterministic Play demonstration. Do not start Unity, a batch run or a player build. Codex remains the single runner for the combined actual Play review. Release your claims when ready for integration.
