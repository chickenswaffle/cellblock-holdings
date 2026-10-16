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
| justice | Only new `Core/Justice.cs`, `Runtime/GameJusticeHud.cs`, `Runtime/FacilityJusticeView.cs`, `Resources/Justice.uss`, `Tools/CoreChecks/JusticeChecks.cs`, their metas, and `docs/justice-notes.md` | Claude (claimed 2026-10-16, branch `codex/claude-justice-custody`) | Complete, awaiting Codex Unity integration check: legal profiles, court transport, escape-attempt gameplay (see `docs/justice-notes.md`) |
| personnel | Only new `Core/Personnel.cs`, `Runtime/GamePersonnelHud.cs`, `Runtime/PortraitElement.cs`, `Runtime/FacilityPersonnelView.cs`, `Resources/Personnel.uss`, `Tools/CoreChecks/PersonnelChecks.cs`, their metas, `docs/personnel-notes.md`; wired through Claude's justice hook implementations | Claude (claimed 2026-10-16, branch `codex/claude-personnel`) | Active: staff files, recruitment, ranks, discipline, watch/special duties, warden and nurse posts, write-ups, fights and health |
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
- Runtime milestone: integrated transport/builders, staged project meshes, staff task animation, Control panel, selected CCTV feed/coverage, prefab fitout selector, and universal macOS native magnify plugin compile in Unity. A fresh general Play review is running; the focused operations/arrival/CCTV review remains pending.
- Claude integration hooks are committed so justice can be developed entirely in new partial-class files. No Claude acknowledgement yet.
- Active next milestone: persistent trips, physically gated work, staff duties, operations state, camera definitions, and prefab packages. All listed scopes remain owned by Codex.

## Claude log

### 2026-10-16 — justice/custody acknowledgement

- Read `AGENTS.md`, `CLAUDE.md`, this file, and `docs/claude-handoff.md` at `950455e`. Claude cannot read `/Users/trading/Documents/ChatGPT/Cellblock Holdings` (macOS privacy denies the terminal), so it works in a separate remote-style clone at `~/ClaudeWorkspace/cellblock-holdings` on branch `codex/claude-justice-custody` from `950455e`. The local lock (`claim claude justice`) exists only in that clone; this row is the cross-clone claim.
- Scope claimed: `justice` only. Files: new `Core/Justice.cs`, `Runtime/GameJusticeHud.cs`, `Runtime/FacilityJusticeView.cs`, `Resources/Justice.uss`, their `.meta` files, `Tools/CoreChecks/JusticeChecks.cs`, `docs/justice-notes.md`, and this log. No existing file is edited except this log and the justice ownership row.
- Not a Unity runner. No editor, batch check, import, or player build will be started against any checkout. Unity-facing files are typechecked offline (C# compile against the installed editor's managed reference assemblies, no editor process) and need a Codex Play check.
- Baseline at `950455e` in this clone: `check-core.sh` with the pinned SDK 8.0.426 → 40 passed, 0 failed.
- Plan: (1) additive legal profiles on `Resident`/`State` partials with `JusticeVersion` migration; (2) court van as a separate `CourtRun` model on road coordinates with a sally-port boarding point, gate waits, mid-trip persistence; (3) bounded escape-attempt state machine using existing `CameraSees`, `SightLines`, officers on shift, gate, alarm and lockdown, with player warnings and close-supervision controls; (4) compact expandable legal card plus a court/custody panel in People; (5) `JusticeChecks.cs` and `docs/justice-notes.md`; (6) one commit per validated milestone, push this branch only.

### 2026-10-16 — justice slice complete, ready for Unity integration

- Branch `codex/claude-justice-custody` (pushed). Commits: `7b15f57` claim, `12c6377` core + checks, `e27bd09` view + HUD + USS, `9be91c7` critic fixes and stronger checks, then a docs/log commit (this entry). No existing file was edited besides this log and the justice ownership row.
- Files: `Assets/Cellblock/Core/Justice.cs`, `Assets/Cellblock/Runtime/FacilityJusticeView.cs`, `Assets/Cellblock/Runtime/GameJusticeHud.cs`, `Assets/Cellblock/Resources/Justice.uss`, their four `.meta` files (minimal, fresh GUIDs), `Tools/CoreChecks/JusticeChecks.cs`, `docs/justice-notes.md`.
- Checks (this clone, SDK 8.0.426): `check-core.sh` → 59 passed, 0 failed (40 yours + 19 justice). Mutation testing: 21/21 deliberate bugs caught. An independent read-only critic subagent found 6 confirmed defects; all are fixed and covered by checks. The critic's fuzz harness was re-run on the fix: 24,012 save/reload round trips, no findings.
- Unity-facing code was typechecked offline with Roslyn against the installed 6000.3.24f1 reference assemblies, for `UNITY_EDITOR;UNITY_STANDALONE_OSX` and `UNITY_IOS`. The only stubs were two URP types and `PresentationLighting`, and no Unity process ran. This confirms the API signatures and all six partial-method hooks. It does **not** cover imports, USS parsing, Play, layout or devices.
- **Unity check request (Codex as runner):** follow "Unity demonstration script" in `docs/justice-notes.md`. Please confirm: no console errors; `Resources/Justice.uss` loads; the legal card and the Court & Custody panel fit at iPhone landscape width; the court van drives, parks and returns; residents hide and reappear; the escape marker and alert actions work; and a mid-run save and reload resumes.
- **Interface requests (your files; details in the notes):** (1) `PeopleDynamics.AdvanceRelationships` should skip pairs where either resident is `!IsPresent`. The checks print `INTERFACE GAP` until it does. Misconduct stress also hits absent residents. (2) The meal-service ratio and (3) the population and officer-workload counts include residents away at court or at large; `World.CustodyCount` is available. (4) An optional `StaffDutyTarget` hook would allow physically dispatching an officer to a detected attempt. (5) No resident intake exists to refill beds freed by releases.
- **Not justice, please verify:** saves use `JsonUtility`, which reloads null `WorkItem.Wall`/`WorkItem.Fixture` as default objects. The `World` constructor then rejects them ("Invalid pending construction work"), including in completed projects. Claude reproduced this at `f4266ca` in a separate scratch Unity project, before the one-runner rule. The validation code is unchanged at `950455e`. Try saving and reloading after moving or removing a furnishing.
- Unresolved in justice: interception is real proximity plus an abstract response timer, not a physical officer dispatch. A reload is not bit-identical (path caches are rebuilt, mostly inherited). Released people leave the roster with no intake to refill it. All of this is fictional game behavior.
- Lock `justice` released in this clone. The row above stays "Claude" until you integrate; after that, hand ownership back or keep it reserved as you prefer.

### 2026-10-16 — personnel scope claimed (user request)

- The user asked Claude for deeper prison-control and staff mechanics while Codex prepares its next handoff, and chose "start now, Codex hands off".
- Claimed `personnel`: only the new files in the ownership row, on branch `codex/claude-personnel` (based on `codex/claude-justice-custody`). Integration goes through Claude's existing justice hook implementations, so no Codex file is edited.
- Planned: recruitment board (candidates with resumes, references, hidden integrity, interviews, background checks); personnel files (procedural face, backstory matched to integrity, original resume, discipline record, supervisor remarks); rename; promote and demote on rank ladders; supervisor influence on integrity; watch assignment to a prisoner; special duties (gate post, court escort, CCTV monitoring, perimeter patrol, cell checks, mentoring, counseling, reentry planning); warden and nurse as posts on existing roles (Warden = officer post, Nurse = caseworker post); write-ups for residents and staff; warden powers (review, sanction, court referral for added time, expedited hearings, good-time recommendations, facility regime, suspensions); resident health with nurse sick call; fights between residents with tension.
- Mechanism limits Claude is working within: `StaffMember` is not partial and `ValidRole` is fixed, so posts ride on existing roles. Duty movement reuses Codex's `AdvanceStaff`: after it runs, Claude's code sets `Destination` and keeps `NextDutyMinute` ahead, and walking stays in Codex's system.

## Codex handoff request (personnel + placement)

Please include in your next handoff to Claude:
1. **Drag-and-drop placement scope (the user's request: "drag and dropping of elements like lights, tools, tables etc to flow better").** Either hand Claude the placement input path in `GameController` (`InputUpdate`/`PreviewAt`/`Tap` for `BuildTool.Furnishing`, `RotatePlacement`, `SetDraft`), `ConstructionGuides`, and the catalog cards in `GameHud`, or add hooks for: drag from a catalog card into the world, drag an existing furnishing to move it, a live valid/invalid ghost with cost while dragging, a rotate gesture or handle, and drop-to-quote. Claude's proposed flow: press and hold a catalog card, drag into the world, see the ghost snap with green/red validity and price, release to open the quote, one-tap approve; drag a selected furnishing to move it the same way; twist or tap a rotate handle; and optionally batch several placements into one quote.
2. **Optional staff hooks** (the posts work without them): `partial void StaffDutyTarget(StaffMember staff, ref Point goal, ref string duty, ref bool handled)` in `AdvanceStaff`; `static partial void ExtraStaffRole(string role, ref bool valid)` in `ValidRole`, if you want Warden or Nurse as real roles later; and the earlier `IsPresent` filter for `AdvanceRelationships`.
3. When you integrate, please note whether you want the personnel posts migrated to real roles.

## Requested handoffs / check requests

Claude justice scope is prepared; read `docs/claude-handoff.md`, claim `justice`, and acknowledge here. Codex remains the sole Unity runner. Request a Unity integration check here when your new files compile and tests pass.
