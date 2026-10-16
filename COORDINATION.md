# Codex + Claude shared work log

Read this file before editing, before every Unity/build run, and before every commit. Update your own section at each milestone. Follow the mandatory protocol in `CLAUDE.md`.

## Rules

1. On this Mac, use this same checkout. In a remote/cloud environment, clone the named handoff branch and work on a separate justice branch; the committed ownership table remains authoritative across clones. Local locks only protect processes sharing the same checkout. Inspect `git status` and recent commits first. Preserve all existing work and saves. Never reset or stash someone else's changes.
2. Claim an unowned scope with `bash Tools/agent-coordination.sh claim <agent> <scope>`. Atomic local locks live in ignored `.collaboration/locks/`. Check actual locks with `bash Tools/agent-coordination.sh status`; the table below is the human-readable intent.
3. One owner per scope. The exact new-file justice exception below overrides the broader folder scopes; Codex must not edit Claude’s listed new files. The owner may subdivide specific files here, but no overlapping edits. Read this file again before touching a new file. If another owner is working there, leave it alone and record a proposed handoff.
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
| justice | Only new `Core/Justice.cs`, `Runtime/GameJusticeHud.cs`, `Runtime/FacilityJusticeView.cs`, `Resources/Justice.uss`, `Tools/CoreChecks/JusticeChecks.cs`, their metas, and `docs/justice-notes.md` | Reserved for Claude (unclaimed) | Legal profiles, court transport, escape-attempt gameplay; use the hooks in `docs/claude-handoff.md` |
| documentation | README, controls/status/handoff, coordination tooling | Codex | Active milestone updates; each agent may edit only its own log below |

Claude: the justice scope is reserved for you. Claim it and add your own acknowledgement below before starting. You may create only the listed new files and edit your own Claude log here. All other existing implementation files remain Codex-owned. Read `docs/claude-handoff.md`.

## Codex log

### 2026-10-16 — current pass

- User requests: pinch zoom; crews arriving in shared trucks, walking and visibly building; functional animated staff and arrivals; live construction phases; lights/alarm/gate/lockdown/report/CCTV controls; placeable cameras; expanded prefab packages; local milestone commits and shared coordination.
- Baseline: Unity 6000.3.24f1 on this Mac; 32/32 core checks; 16 actual Play captures with no runtime errors. Prior improvements are local and will be checkpointed before new implementation.
- Existing local changes belong to the prior Codex playable pass, including Unity-generated settings. Preserve saves and the untracked `ProjectSettings/ShaderGraphSettings 2.asset` until its provenance is clear.
- Current action: save active company, stop Play safely, create a baseline commit, then implement in bounded milestones. Remote push now authorized by the user: publish finished work on a new `codex/` branch for Claude.
- Review constraints: trips use separate road coordinates; occupants remain on-site in saved navigation; work gates on physical arrival; staged meshes do not become operational before handover; old saves hydrate without duplicate fees; CCTV uses shared wall LOS; report boundaries persist.
- Baseline checkpoint `aae6f72` committed locally (see latest `Improve local Unity playable, people dynamics, catalog and agent coordination` commit); 32 core checks and 16 Play captures passed before this next systems pass.
- Core checkpoint `35ce0e3` committed.
- Core milestone: 40/40 checks passed, including 72 prefab/package/rotation quotes. Critic findings fixed: departing-trip bounds, completed-shift report intervals, helper footprint containment, station protection and work-front spacing.
- Runtime milestone: integrated transport/builders, staged project meshes, staff task animation, Control panel, selected CCTV feed/coverage, prefab fitout selector, and universal macOS native magnify plugin compile in Unity. The earlier general 16-capture review passed; focused operations/arrival/CCTV and articulated work reviews also passed. A final regression pass is running.
- Claude integration hooks are committed so justice can be developed entirely in new partial-class files. Claude justice branch has now been fetched for integration; see incoming Claude log.
- Runtime checkpoint `950455e` pushed to `codex/live-operations-handoff` (user authorized new-branch push). Actual focused operations Play review passed all nine captures with no runtime errors. Native magnify initialized; gate-held hires, staged builds, capacity handover, live CCTV, night alarm, reports and phone Control checked.
- Core workflow checkpoint `69e8ca8` committed locally.
- Core workflow milestone: 46/46 checks pass, including the exact enhanced-housing/two-crews/shared-staff arrival scenario and completion with material trips; mid-carry save/load; slow bay docking; simultaneous gate queues; staff shuttles freeing bays; additive operations v2 migration preserving purchases/progress; persistent geometry generations preserving later-project carry after handover. The first articulated runtime review passed all twelve captures with no runtime errors. Final operations Play review passed all twelve captures, no runtime errors; general 16-frame regression is running.
- Active next milestone (latest user steering): sequential material collection/carrying, articulated tool-specific builder actions, smooth marked-bay parking and disembark, plus critic fixes for pinch queues, alarm audio listener, luminaire emission and CCTV overlays. Codex retains existing scopes. Claude justice reservation and hooks remain available; justice integration is next.

## Claude log

_No acknowledgement or active work recorded by Claude yet. Claude must add its own dated entry before starting, list claimed scopes/files, and report results/commits here._

## Requested handoffs / check requests

Claude justice scope is prepared; read `docs/claude-handoff.md`, claim `justice`, and acknowledge here. Codex remains the sole Unity runner. Request a Unity integration check here when your new files compile and tests pass.

Codex integration update: fetched `origin/codex/claude-justice-custody` at `49aff6e` after the user reported completion. Preserve and merge Claude’s log and scoped files. User also requests richer prisoner animation/lives/stories for Claude’s next slice; Codex retains the new connected-door/security/power/upgrade mechanics.
