# Codex + Claude shared work log

Read this file before editing, before every Unity/build run, and before every commit. Update your own section at each milestone. Follow the mandatory protocol in `CLAUDE.md`.

## Rules

1. Use this same checkout. Inspect `git status` and recent commits first. Preserve all existing work and saves. Never reset or stash someone else's changes.
2. Claim an unowned scope with `bash Tools/agent-coordination.sh claim <agent> <scope>`. Atomic local locks live in ignored `.collaboration/locks/`. Check actual locks with `bash Tools/agent-coordination.sh status`; the table below is the human-readable intent.
3. One owner per scope. The owner may subdivide specific files here, but no overlapping edits. Read this file again before touching a new file. If another owner is working there, leave it alone and record a proposed handoff.
4. **One Unity runner.** The `unity-editor` owner controls Play, imports, screenshots, batch validation, and native builds. Other agents request checks in their log; they do not launch another Unity/build process.
5. Complete and validate a bounded milestone, review its diff, and commit only owned files. Record the commit and evidence here. Re-read the other agent's log before continuing. No remote pushes without user instruction.
6. Release locks and update the ownership table when stopping or handing off. A lock has no automatic expiry. If its owner appears gone, ask the user before reclaiming it. Do not claim that the other AI read a message without an entry from it.

## Current ownership

| Scope | Files/resources | Owner | State |
| --- | --- | --- | --- |
| game-core | `Assets/Cellblock/Core/`, `Tools/CoreChecks/` | Codex | Active: arrivals, work travel, operations, prefabs |
| game-runtime | `Assets/Cellblock/Runtime/`, native trackpad plugin | Codex | Active: workers, vehicles, progress, staff animation, CCTV, pinch |
| game-ui | `GameHud*`, `Cellblock.uss` | Codex | Active: prison controls, hiring, prefab packages |
| unity-editor | Installed editor, `Assets/Cellblock/Editor/`, generated settings, all builds | Codex | Active: sole runner |
| documentation | README, controls/status/handoff, coordination tooling | Codex | Active milestone updates; each agent may edit only its own log below |

Claude: choose work outside these scopes or record a requested handoff below. No parallel implementation of the systems listed above.

## Codex log

### 2026-10-16 — current pass

- User requests: pinch zoom; crews arriving in shared trucks, walking and visibly building; functional animated staff and arrivals; live construction phases; lights/alarm/gate/lockdown/report/CCTV controls; placeable cameras; expanded prefab packages; local milestone commits and shared coordination.
- Baseline: Unity 6000.3.24f1 on this Mac; 32/32 core checks; 16 actual Play captures with no runtime errors. Prior improvements are local and will be checkpointed before new implementation.
- Existing local changes belong to the prior Codex playable pass, including Unity-generated settings. Preserve saves and the untracked `ProjectSettings/ShaderGraphSettings 2.asset` until its provenance is clear.
- Current action: save active company, stop Play safely, create a baseline commit, then implement in bounded milestones. No remote push requested.
- Review constraints: trips use separate road coordinates; occupants remain on-site in saved navigation; work gates on physical arrival; staged meshes do not become operational before handover; old saves hydrate without duplicate fees; CCTV uses shared wall LOS; report boundaries persist.
- Next update: record baseline commit and first core milestone/check results here.

## Claude log

_No acknowledgement or active work recorded by Claude yet. Claude must add its own dated entry before starting, list claimed scopes/files, and report results/commits here._

## Requested handoffs / check requests

None. Codex is the current Unity runner.
