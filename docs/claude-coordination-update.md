# Current parallel work update — 2026-10-16

Read COORDINATION.md and fetch `codex/live-operations-handoff` before continuing. Preserve your current branch and unfinished work; do not reset it.

## User's latest direction

The user reports Claude is working on prisoner lives and has also given Claude the opening game modes / HUD design request. Codex continues connected doors, physical security, utilities, upgrades and their Unity review. Codex is the sole Unity runner.

Recommended opening structure: jurisdiction (Federal / State / City / County) followed by a facility profile (for example federal camp vs federal supermax). Challenge follows the actual profile: contract terms, legal population, temperament ranges, staffing, initial physical security and budget. Jurisdiction alone is not a reliable linear difficulty ladder. Keep an accessible recommended starting profile; let players pick a city or request a seeded random eligible city. Preview the physical layout, population, staffing obligations and funds before starting.

This is design guidance for Claude's assigned task, not a claim that the mode system is already implemented. Preserve the old three contract IDs and saves. New-game choices must reach the actual core setup, legal population and building configuration, not only the menu labels.

## Scope boundaries

- Codex: all existing Core/Simulation.cs, Operations.cs, Security.cs, Connections.cs, FixtureCatalog.cs, PrefabCatalog.cs; runtime workers/security/construction, existing GameController/GameHud, Unity editor. Do not edit these in your branch without an explicit documented file handoff.
- Claude: reserved resident-lives new files and hooks in docs/claude-lives-handoff.md. Opening-mode design may be developed in new separate files. Request narrow hooks or a concrete file handoff for integration of menu and scenario setup; avoid silently replacing the existing input/HUD/controller.
- An additional `codex/claude-personnel` branch exists (latest observed core commit 89834a8) with an active personnel claim in Claude's separate clone. Codex has preserved it and has not merged unfinished personnel changes. Report the actual current branch and SHA. If personnel is still active, keep it isolated and report its hook requests before merging; it currently edits Justice.cs, which Codex has updated since the old justice base.
- Codex's Justice changes now dispatch mobile officers to a detected attempt and allow interception only by actual proximity and LOS. Do not restore the abstract response timer. Integration must retain presence filtering and the Unity construction payload fixes.
- Never start a second Unity instance. Request combined Play verification in your log.

Please acknowledge the current task/branch, claim the matching scope, and report finished commits, checks, boundaries and a repeatable demonstration. New Lives code must preserve meal/court/confinement/absence priorities and staff security assignments.

## Published file handoff

`docs/claude-modes-and-building-handoff.md` supersedes the earlier existing-file restriction for the opening-and-build-ui scope once that scope appears in the published ownership table and Claude claims it. It transfers Simulation.cs, GameHud.cs, GameController.cs, ConstructionGuides.cs and Cellblock.uss for the user's assigned modes/HUD/placement work. Preserve current security/justice APIs and request further hooks instead of overlapping the remaining Codex files.
