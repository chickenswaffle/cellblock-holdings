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
| justice | Only new `Core/Justice.cs`, `Runtime/GameJusticeHud.cs`, `Runtime/FacilityJusticeView.cs`, `Resources/Justice.uss`, `Tools/CoreChecks/JusticeChecks.cs`, their metas, and `docs/justice-notes.md` | Codex integration (Claude released) | Integrated and reviewed in Unity: legal profiles, court transport, escape-attempt gameplay (see `docs/justice-notes.md`) |
| resident-lives | Only new `Core/Lives.cs`, `Runtime/FacilityLivesView.cs`, `Runtime/GameLivesHud.cs`, `Resources/Lives.uss`, `Tools/CoreChecks/LivesChecks.cs`, their metas and `docs/lives-notes.md` | Reserved for Claude (next handoff, unclaimed) | Prisoner animation, daily activities, memories and stories; see `docs/claude-lives-handoff.md` |
| documentation | README, controls/status/handoff, coordination tooling | Codex | Active milestone updates; each agent may edit only its own log below |

Claude: your completed justice branch is under Codex integration. Your next reserved scope is `resident-lives`; the new handoff is published from integration base `9624674`. Read `docs/claude-lives-handoff.md`, claim it and acknowledge in your log. Create only the listed new Lives files and edit your own log. Codex owns all existing files, security/doors/power/upgrades and Unity runs.

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
- Claude justice `49aff6e` is merged. Its code passed combined69 core checks and eight actual combined Unity frames, including phone legal UI, court/return, absent visibility, Unity JSON mid-trip and fixture saves, detected escape and alarm response. Integration fixes preserve operations v2 and both agents’ logs. A resident-lives handback is prepared with additive migration, clock, routine-target, animation and UI hooks.
- Runtime checkpoint `950455e` pushed to `codex/live-operations-handoff` (user authorized new-branch push). Actual focused operations Play review passed all nine captures with no runtime errors. Native magnify initialized; gate-held hires, staged builds, capacity handover, live CCTV, night alarm, reports and phone Control checked.
- Core workflow checkpoint `69e8ca8` committed locally.
- Core workflow milestone: 46/46 checks pass, including the exact enhanced-housing/two-crews/shared-staff arrival scenario and completion with material trips; mid-carry save/load; slow bay docking; simultaneous gate queues; staff shuttles freeing bays; additive operations v2 migration preserving purchases/progress; persistent geometry generations preserving later-project carry after handover. The first articulated runtime review passed all twelve captures with no runtime errors. Final operations Play review passed all twelve captures, no runtime errors; general 16-frame regression is running.
- Active next milestone (latest user steering): sequential material collection/carrying, articulated tool-specific builder actions, smooth marked-bay parking and disembark, plus critic fixes for pinch queues, alarm audio listener, luminaire emission and CCTV overlays. Codex retains existing scopes. Claude justice reservation and hooks remain available; justice integration is next.

### 2026-10-16 — connected security and services implementation

- Active Codex files: new Core/Security.cs and Connections.cs, FixtureCatalog/PrefabCatalog/Simulation/Operations/Justice; new FacilitySecurityView/GameSecurityHud; existing actor/worker/controller/HUD integration; new SecurityChecks and SecurityReview. Pending and installed assets remain separate; upgrades preserve identity. Existing company saves and unknown Unity recovery assets are preserved.
- Core milestone: 84/84 checks pass (69 prior + 15 security/connections), including 72 prefab/package/rotation quotes. Actual Unity security review passed nine captures, no runtime errors, including real powered outdoor lighting, operational tower, JSON upgrades and repeat-placement purchase persistence. A final general 16-frame regression is running.
- Critic findings: unavailable-post assignments now reject before fees; draft connection must border the new unit across the full doorway. Existing doors survive as separately editable wall pieces. Interception now requires physical officer arrival/LOS; ETA describes a route, not an automatic timer.
- Latest user steering: Claude owns richer resident lives and has also received opening jurisdiction/facility-mode and HUD direction. Codex continues security/services and construction flow. See docs/claude-coordination-update.md for current boundaries and integration advice.
- Discovered a separately claimed personnel branch at origin/codex/claude-personnel (latest core 89834a8); preserved it. It is not integrated or claimed complete. Its older Justice changes will need deliberate integration. Do not overwrite Claude's unfinished clone or log.

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

## Requested handoffs / check requests

Claude justice branch `49aff6e` is merged locally. Codex integration fixes: presence-aware social/misconduct/workload/service accounting, real blocked-release waiting, and Unity inline-serialization payload discrimination. Combined69 core checks and eight actual Unity justice frames passed (no runtime errors). The empty-draft regression passes; next `resident-lives` handoff is prepared in `docs/claude-lives-handoff.md`. Codex remains the sole Unity runner and owns security/door/services implementation; no overlapping edits.

Next Claude handoff is ready at integration base `9624674`; `docs/claude-start.txt` is the short copy/paste prompt and `docs/claude-lives-handoff.md` is the exact scope/hook contract. Codex continues doors/security/services; no new overlapping file ownership.

Codex integration update: fetched `origin/codex/claude-justice-custody` at `49aff6e` after the user reported completion. Preserve and merge Claude’s log and scoped files. User also requests richer prisoner animation/lives/stories for Claude’s next slice; Codex retains the new connected-door/security/power/upgrade mechanics.
