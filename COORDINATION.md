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
| game-core | `Assets/Cellblock/Core/`, `Tools/CoreChecks/`, excluding Simulation.cs and the exact new scopes below | Codex | Source `8f38ce2`; 305 combined checks passed, saved physical incidents/radio timing; security and routes preserved |
| game-runtime | Existing runtime renderers/operations/native plugin, excluding GameController.cs, GameHud.cs and ConstructionGuides.cs | Codex | Source `8f38ce2` adds visible incident poses and close follow camera; safe saves/preferences retained |
| game-ui | GameOperationsHud.cs / GameSecurityHud.cs integration; new metrics, Home/settings/command shell and incident partials/resources | Codex | Source `8f38ce2`; live incident card/controls plus retained Home, dock, numbered meters and navigation |
| unity-editor | Installed editor, `Assets/Cellblock/Editor/`, generated settings, all builds | Codex | Source `8f38ce2`: all 73 final incident/frontend/metrics/polish captures passed; ordinary Play/Home restored for handoff |
| justice | Existing Justice.cs, GameJusticeHud.cs, FacilityJusticeView.cs, Justice.uss, JusticeChecks.cs and justice notes | Codex integration | Reviewed; preserve presence/save/release fixes and physical interception |
| opening-and-build-ui | Existing Core/Simulation.cs, Runtime/GameHud.cs, Runtime/GameController.cs, Runtime/ConstructionGuides.cs, Resources/Cellblock.uss; new mode/configuration/HUD files, matching scoped checks/metas/notes | Claude (acknowledged 2026-10-17 on branch `codex/claude-overload`, merged with Codex `9b6f2e4`) | Merge preserves StartMode, explicit terms, direct doors and movement memory; Claude's duplicate StartCharter removed. Meals: Claude's nearest-place plan plus Codex's MealDue/MealTravelDue split |
| resident-lives | Only new Core/Lives.cs, Runtime/FacilityLivesView.cs, Runtime/GameLivesHud.cs, Resources/Lives.uss, Tools/CoreChecks/LivesChecks.cs, matching metas and docs/lives-notes.md | Claude (acknowledged 2026-10-17, branch `codex/claude-overload`) | Appearance and poses integrated; activities and stories still to come |
| personnel | New Personnel.cs, GamePersonnelHud.cs, PortraitElement.cs, FacilityPersonnelView.cs, Personnel.uss, PersonnelChecks.cs, matching metas and personnel notes | Claude (branch `codex/claude-overload`) | Published fixes through `1a9aa5b` integrated. Preserve `8f38ce2` incident start/resolve and destination-ownership hooks under the exact Codex exception below |
| population-pressure | Core/Population.cs, Intake.cs, Contraband.cs, Upgrades.cs, pressure/lives views and HUD, ride-along, matching checks/metas/notes | Claude (branch `codex/claude-overload`); Codex integrated 650e3ce | Nearest dining places, trays, stuck-resident load repair and balance fixes integrated; preserve active follow-on |
| economy | New Core/Economy.cs, Informants.cs, Kitchen.cs, Counsel.cs, Roles.cs; Runtime/GameEconomyHud.cs, GameIntelHud.cs, GameKitchenHud.cs, GameCounselHud.cs; Resources/Economy.uss; Tools/CoreChecks/EconomyChecks.cs, IntelChecks.cs, KitchenChecks.cs, CounselChecks.cs, RoleChecks.cs; matching metas; docs/economy-notes.md, intel-notes.md, kitchen-counsel-roles-notes.md | Claude (claimed 2026-10-17, branch `codex/claude-overload`) | Published snapshot through `1a9aa5b` integrated as `9f55fd9`; 305 combined checks passed. Active follow-on remains Claude’s |
| home-save-shell | Home/Continue/save/settings and main HUD shell; exact existing-file exceptions and new files listed in current log | Codex (latest user request) | Home source `3fc7417` preserved in `8f38ce2`; final 25-frame source regression passed. Claude retains charter, construction/catalog logic, lives and economy |
| documentation | Shared README/status/coordination and validation evidence; Claude may edit its own log and scoped new notes | Codex | Checkpoint and handoff publication |

The explicit file rows above override the broad folder rows. Claude: the user's latest modes/HUD/placement request now has a concrete existing-file handoff at `a6d2afc`. Fetch the latest shared branch, preserve your own work, read `docs/claude-modes-and-building-handoff.md` and `docs/claude-coordination-update.md`, then claim opening-and-build-ui and/or resident-lives and acknowledge your current branch/SHA. The latest direct Home/save/settings/command-shell exception below overrides only those named parts of GameHud/GameController; all remaining handed-off logic stays reserved. Other existing files, security/doors/utilities/operations/justice and Unity remain Codex-owned. Do not restore older Justice or input implementations during integration.

## Codex log

### 2026-10-17 — validated physical incident scenes / handoff

- Direct user request: repair invisible fight/escape demos, show pursuit and a guard radio action before automatic lockdown. Codex claims new Core/IncidentScenes.cs, Runtime/GameIncidentHud.cs, GameIncidentControl.cs, FacilityIncidentView.cs, Resources/Incidents.uss, Editor/IncidentReview.cs, Tools/CoreChecks/IncidentChecks.cs and matching metas. Existing owned Justice/Operations/render files get narrow integration hooks. Exact temporary exception to Claude's personnel scope: StageFightDemo/Confront begin-vs-resolve split and DirectStaff incident ownership guards in Personnel.cs, plus the one demo button in GamePersonnelHud.cs. Existing Personnel/Justice/Pressure/GameModes/Intel/Role check fixtures receive narrow phase-aware expectation updates; unrelated intake/court scenarios suppress random escapes so the new automatic lockdown cannot invalidate their setup. No other careers, economy, lives or catalog edits.
- Design contract: saved bounded state and explicit game clock; actors physically converge without teleporting, staged tension/scuffle/intervention, once-only consequences. Existing assigned security/capital/secure escorts retain destination ownership. Detection selects a real available observer/operator; radioing takes game time, then SetAlarm closes the gate and starts lockdown. Detection alone cannot do so. Manual alarm stays immediate; no free guard means no phantom interception. Actual range/sight/routes remain authoritative. Runtime framing and poses explain the real state; demo action says1x, normal incidents offer optional camera focus. Tests cover pause, reload, duplicate actions, report timing and physical outcomes. Critic reviewed root causes before implementation; actual Unity validation remains required.
- First incident Play review passed 16 captures; critic requested closer side-facing framing and stricter report hydration/terminal state. Both corrected. The actual radio actor now remains on station during its call; active security/custody/intake assignments are excluded, and CCTV reporting requires actual desk proximity. Guard fight approach chooses a reachable, clear same-side point with real sight; the former fixed offset could land across a wall. Long travel no longer consumes encounter time. 304 checks passed before the final travel-clock regression; full final suite and corrected-source incident/frontend/metrics/door Play matrix are running. Earlier captures are superseded, never added to final totals.
- Final source passes **305/305 core checks and 16 actual incident Play captures, errors empty**. The critic approved source plus the corrected scuffle/intervention/radio/chase/phone images. The rendered final approach now settles along clear segments before contact poses; existing cached renderer routing continues around corners, and the review waits for distinct actually aligned actors before capturing. Control wording derives current flags and an actual stand-down probe passed. Evidence: docs/reviews/2026-10-17-incidents-{core.txt,unity.json}; full frames Builds/IncidentReview/2026-10-17/final. Source is frozen for the final frontend/metrics/polish regressions; superseded passes are not added to the final matrix total.
- Validated source checkpoint **8f38ce2** committed with scoped sources, tests, notes and focused evidence. No unrelated recovery or duplicate settings asset was staged. Final regression reports will follow in a documentation-only checkpoint; no further feature work is planned for this publication.
- Final unchanged-source matrix passed: **16 incidents + 25 frontend + 20 metrics + 12 construction/presentation = 73 actual Unity captures; every errors array empty**, with **305/305 core checks**. Reports are docs/reviews/2026-10-17-incidents-{unity,frontend-unity,metrics-unity,polish-unity}.json; full regression frames use the incidents-final folders named in implementation-status.md. Source remains `8f38ce2`. Read-only critic approved the corrected actual scuffle/intervention/radio/chase/phone images and final source. No device/performance claim. Unity returned to ordinary Play/Home after disposable reviews; actual player holdings and unrelated local files are preserved.
- Publication handoff: push this source and documentation on the user-authorized codex/live-operations-handoff branch, then release all six local Codex scope locks. No further source edits or reviews remain active. The ownership table continues to reserve Codex's shared/security/Home/incident interfaces; Claude retains its listed lives/personnel/pressure/economy/build scopes and should read docs/claude-start.txt before continuing. Exact incident integration hooks must be preserved; a new shared-file change needs an explicit handoff. Claude has not yet acknowledged this newest checkpoint in this file.

### 2026-10-17 — active home, save continuation and command HUD

- Latest direct user request assigns Codex a visible home/menu/settings/HUD redesign and easier Continue/autosaves. Exact temporary exception to Claude's broad opening-and-build-ui reservation: Codex owns ShowMenu, save enumeration/Save/Load/autosave hooks, header/navigation/overview shell and additive menu/settings styling in GameHud.cs/GameController.cs for this pass. Core simulation, charter logic, construction tools/catalog logic and Claude's lives/economy/personnel sources stay with Claude. Asked the user asynchronously whether Claude is editing these exact screens; no answer yet. New files isolate most work: GameHomeHud.cs, GameSettingsHud.cs, GameCommandHud.cs, GameSaveFlow.cs, GamePreferences.cs, Frontend.uss, original HomeNight artwork, matching metas and Editor/FrontendReview.cs. No unfinished remote work is accessed.
- Claimed home-save-shell, game-runtime/UI/core/documentation and sole unity-editor locks. Editor safely exited Play. Merged Claude's published economy/informants/kitchen/counsel/role-card snapshot 9c88d9e without conflicts; retain both agents' logs, Codex's 2f54ce4/e6e299d HUD work and all new economy hooks. Combined core/API/Play validation pending; no new passing claim yet.
- Critic confirmed one published role-switch defect: changing the ride-along target retains the former controlled person and can pause on hidden cards. Codex takes a narrow integration correction in GameRideAlong.StartRideAlong: release the old role after validating the new target, reset seen-card tracking, and verify the switch in Play.
- Combined core passed 286/286 after integrating Claude. Actual Unity import/API compilation passed. First focused run passed 21 captures, with real isolated save recovery/failure/retry/settings and economy/role integration. Critic corrections before final run: explicit title TextureImporter metadata (minimal metadata created an unusable resource); concise save errors; capacity uses valid World.Capacity, amber at full/red overcrowded; centered command dock, debounced audio persistence and Back closes one menu level. Final corrected-source review and existing door/HUD regressions remain pending.
- Design: dominant Continue with actual save metadata and a sorted library; stale/missing-pointer fallback; visible autosave status and configurable interval; protect valid backups when recovering damaged primaries; real persisted settings for audio/display/controls/accessibility. Cinematic illustrated night home with one shared ink/brass theme. Larger command navigation, context/indicators and clear settings/save access make the in-game change visible. Preserve original slots, pause-on-load, explicit-clock behavior, draft persistence and preview isolation. Critic reviews flow, data safety and actual phone/desktop images before publication.
- Final critic corrections: disable vSync when applying the selected desktop frame cap; treat every overcrowded ratio and custody with zero valid beds as red, full capacity as amber and an empty facility as green. Shared capacity policy has explicit Play semantic probes. The previous 23-frame run passed; final corrected-source frontend and existing metrics/door regressions are now running. No more feature scope is planned before publication.
- Actual-image review caught the phone New Holding CTA clipped by the ScrollView despite fitting root bounds. Tightened compact title spacing, retained 44px actions and changed the assertion to the actual content viewport. All earlier frontend captures are superseded by the final corrected-source run; they are not added to final totals.
- Final corrected frontend source passed all **23 actual Unity captures, errors empty**, including the strengthened phone viewport checks, first-use/charter, real isolated save recovery/retry, applied preferences and Claude economy/role integration. The independent critic approved the regenerated phone/narrow images and final source. Evidence: Builds/FrontendReview/2026-10-17/final and docs/reviews/2026-10-17-frontend-{unity.json,core.txt}. Existing metrics and polish/door regressions follow the source checkpoint.
- A final fetch found newly published Claude placement/mutation fixes ccd16c4 and 1a9aa5b; merge is clean and preserves all frontend hooks. Scope exception for integration: correct only StepAside’s resident footprint-interior predicate in Simulation.cs and add a focused PlacementChecks regression, as confirmed by the read-only critic. NearbyClear already checks fixture containment for candidates. Extend the owned frontend review with actual projected furniture/light clicks in the secure-cell start, then rerun combined core and the final matrix on merged source. Earlier 23+ 20 frame passes remain prior-source evidence, not final combined totals.
- Combined snapshot through Claude1a9aa5b passed **294/294 core checks** and **25 actual Unity frontend/placement captures**, errors empty. This includes the added fixture-interior handover regression and real projected-pointer path for a secure-cell exterior light and wall-fitted bed quote. Evidence updated in docs/reviews/2026-10-17-frontend-*; full frames Builds/FrontendReview/2026-10-17/combined-final. Home source3fc7417 remains intact. The user now requests a second bounded milestone: visible fight/escape scenes, physical pursuit and guard radio-before-lockdown. That follows the menu/save checkpoint.

### 2026-10-17 — validated readable metrics and inspector navigation

- User prioritizes numbered green/red stat bars and a more thoughtful, premium HUD. Codex holds game-core/runtime/UI/justice/documentation and the sole Unity runner; editor is safely out of Play. No new published Claude commits appeared on fetch. Claude's reserved GameHud.cs, Cellblock.uss, profiles/personnel/lives source remains untouched.
- Exact files for this bounded milestone: new Runtime/StatMeter.cs, Runtime/GameMetricsHud.cs, Resources/Metrics.uss and their metas; owned GameJusticeHud.cs, GameSecurityHud.cs, GameOperationsHud.cs; new Editor/MetricsReview.cs and existing PresentationPreview.cs routing; scoped docs/evidence. New partial HUD accesses existing fields without rewriting Claude's main workspace/build navigation. Local profile/control section navigation and additive styles belong to this pass.
- Meter contract: number stays readable inside the track; overall danger grows red, protective factors extend left in green from a labeled zero, positive danger extends right in red. Factors are risk points, not probabilities. General needs use their actual 0–100 score. Fixed section shortcuts must fit phone width, preserve world-input blocking and keep stable button identities. Actual Unity compile/Play/phone-layout review and independent critic required before publication.
- Claude list follow-on identified: bounded failed-route retries and post-termination justice fees; do after the urgent HUD milestone or record as next work. Physical intake/kitchen fixtures await coordination with their Claude-owned consumers.
- Validated bounded implementation: **215/215 core checks and 20 final actual Unity metric frames passed, no runtime errors.** Fixes after critic/visual review: exact 35/55 escape bands with classification-preserving readout; key protective factors included; deferred content-coordinate jumps + resize anchoring; subject-change and Control-reopen reset; manual-scroll navigation highlight; CCTV count reused. First phone-heading failure was corrected and superseded by the final passing report. Evidence: `Builds/MetricsReview/2026-10-17/final/`, `docs/reviews/2026-10-17-metrics-*`. The existing general HUD regression subsequently passed all 16 frames: **36 final actual Unity captures in total, no runtime errors**. No native-device claim.
- Source milestone **2f54ce4** is committed. The critic approved corrected source and actual phone images. No newer Claude publication appeared on the final fetch. Updated `docs/claude-start.txt` hands the reusable components and hooks back to Claude for its reserved main menu/build/catalog/navigation work. Existing player saves and unrelated recovery/settings assets are preserved. Editor returned to ordinary Play/menu with a fresh CELLBLOCK_PLAY_MODE_READY marker. Publish this checkpoint on the user-authorized shared branch, then release all six Codex local locks immediately after successful publication.

### 2026-10-17 — active searchlight and security presentation pass

- Integrated Claude's published `650e3ce` by fast-forward, preserving the unknown recovery assets and duplicate settings asset. Claude's active Lives/economy/programs/personnel/placement work remains reserved. Critic found no confirmed regression in the published meal, route, tray and solvency changes.
- Codex claims game-core, game-runtime, game-ui, documentation and the sole unity-editor runner. Planned files: new Searchlights core/runtime/checks and editor review; existing Security.cs, FacilitySecurityView.cs, GameSecurityHud.cs, MovementMemory.cs and its checks, FacilityView.cs for retiring permanently departed actors. No edits to Claude's active source files.
- Implementation contract: saved light controls keyed by stable tower identity, independent of footprint rotation; deterministic game-clock sweep shared by rendering/coverage; operational night/power/shift/arrival gates; real wall LOS; bounded sleep stress for present residents actually resting at home; existing lookout detection retained. More detailed security models and useful controls accompany the mechanic. Route endpoint hardening and departed-actor cleanup close existing review items.
- Editor is safely out of Play (`CELLBLOCK_EDIT_MODE_READY`). Actual compile/Play and core validation will follow; no new validation claim yet.
- Core candidate now passes 215/215. Fixed starter-wiring migration, paused same-count actor retirement, shared spot width and adaptive narrow-wall rays after critic review. Actual Unity reached 15 new/existing captures; the runner's 0.6 m diagnostic wall was correctly rejected by the existing 0.65 m minimum. Correcting the test to a legal 0.8 m wall. Visual review also motivates a softer light shaft/pool and brighter cool moonlight in Codex-owned FacilityOperationsView.cs; this changes presentation only. A full passing Play report remains required.
- Validated implementation milestone: **215 core checks passed / 0 failed; actual Unity 6000.3.24f1 project validation, compile and all 17 final security Game-view captures passed, no runtime errors.** The final legal narrow wall and door-opening tests pass; earlier runner failures are superseded, not counted as passes. Captures: `Builds/SecurityReview/2026-10-17/night-watch-final/`; committed report/transcript: `docs/reviews/*night-watch*2026-10-17*`. Final broader door/polish and stories/menu regressions follow before publication. All actual player company slots and unrelated recovery/settings assets remain preserved.
- Milestone `0de5ed9` is committed; published merge `415c1d4` also preserves Claude's `db6ad58` balance note. Broader 12-frame polish regression passed. Final close-view critique confirmed the completed lookout stood through the central lamp mast: adjust only the rendered deck stand to local (+0.65,+0.55), preserving core access/climb/staffing. Also switch lens emission with actual beam availability and tidy the selected-officer label. These scoped runtime polish corrections will receive a fresh final security review.
- **Final corrected-source matrix passed:** security 17 + stories/menu/modes 21 + polish/doors/pressure 12 = **50 actual Unity captures, no runtime errors**, plus the unchanged core's **215/215 checks**. The critic approved final shaft/wall/open-door views and the lookout's clear deck position. Final frames are saved under the October 17 `night-watch-final` and `night-watch-regression` Builds folders; three small reports and core transcript are committed in docs/reviews. No actual-device or native-player performance claim. Publication/handoff update is the remaining step; no further source edits are planned for this pass.
- Editor has returned to normal Play/menu with a fresh `CELLBLOCK_PLAY_MODE_READY` and project-validation marker. `docs/claude-start.txt` is the copy/paste continuation; `docs/night-watch.md` includes mechanics, implementation plan and prioritized next ideas. Publish the final runtime corrections/docs/evidence on the already authorized shared branch, then release Codex's five local locks. Claude's active source remains reserved; no unpublished clone changes were touched.
- **Published final implementation `87aaf4c` to origin/codex/live-operations-handoff; local/remote SHA matched.** Game-core, runtime, UI and Unity runner locks are released. The documentation lock was reclaimed only to record this publication and will be released immediately after this note is committed/pushed. Working-tree inspection shows only the same pre-existing recovery folder/meta and duplicate ShaderGraph asset. Claude can fetch/merge the shared branch and continue its claimed scope using `docs/claude-start.txt`.

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
- Core milestone: 84/84 checks pass (69 prior + 15 security/connections), including 72 prefab/package/rotation quotes. Actual Unity security review passed nine captures, no runtime errors, including real powered outdoor lighting, operational tower, JSON upgrades and repeat-placement purchase persistence. The final general 16-frame layout regression passed with no runtime errors.
- Critic findings: unavailable-post assignments now reject before fees; draft connection must border the new unit across the full doorway. Existing doors survive as separately editable wall pieces. Interception now requires physical officer arrival/LOS; ETA describes a route, not an automatic timer.
- Latest user steering: Claude owns richer resident lives and has also received opening jurisdiction/facility-mode and HUD direction. Codex continues security/services and construction flow. See docs/claude-coordination-update.md for current boundaries and integration advice.
- Discovered a separately claimed personnel branch at origin/codex/claude-personnel (latest core 89834a8); preserved it. It is not integrated or claimed complete. Its older Justice changes will need deliberate integration. Do not overwrite Claude's unfinished clone or log.

### 2026-10-16 — published security checkpoint and next file ownership

- Implementation checkpoint `a6d2afc`: connected draft doors, 23 equipment types, room/outdoor service, upgrades with stable identity, real tower/K9/armed assignments, utilities/comfort/morale, physical interception and faster repeat placement. Critic reviewed construction/security twice; findings fixed and checked.
- 84/84 core checks passed. Nine actual Unity security frames passed without runtime errors, with powered night lighting verified visually. Final general 16-frame review passed with no runtime errors. Evidence lives in ignored Builds/SecurityReview/2026-10-16 and Builds/PresentationReview/2026-10-16/security-regression, plus small committed reports/transcript in docs/reviews.
- Existing-file handoff now reserved for Claude: Simulation.cs, GameHud.cs, GameController.cs, ConstructionGuides.cs, Cellblock.uss. Codex source edits to these are frozen at the checkpoint, except the documented two-line modal-input integration guard below. New modes/lives work follows the explicit table; Claude must preserve security, custody and persistence APIs and report branch/SHA.
- The observed personnel core `89834a8` remains separate; no unfinished personnel or Lives work has been merged. Preserve Claude's branch and request a completed integration handback.

### 2026-10-16 — richer facility and visual pass (active)

- New user request: more detailed people/world, natural movement, facility goals and incidents, named antagonists/gang leaders, state/federal inspections, protestors and death-row/capital cases. Codex claims game-core/runtime/justice/documentation/unity-editor; preserves Claude's handed-off modes/menu/input files and new Lives scope.
- Integrating ready personnel `506633e` first, preserving current security and physical dispatch. Critic found migration, conflicting personnel/security duties and off-site escort bypass risks; fixes and combined checks are in progress. No completed validation claim yet.
- New facility-events scope will use new files plus hooks in Codex-owned Justice/operations partials. Personal life stories remain Claude's slice. Facility objectives, named external pressures and fictional capital-case proceedings are Codex's current event work.

- Personnel integration exception: Codex applies only `InteractionModalOpen` to GameHud.BlocksWorldInput and `CloseInteractionSheets()` to CloseOverlays. This fixes typing/modal camera input without changing Claude's menu/placement design. The new helper partials own those guards. No ConstructionGuides/theme edit is made. New-world integration adds one explicit InitializeNewFacilityStories call at World.Start completion; legacy hydration never creates authored convictions. Claude published mode catalog 029e5d9 requesting Codex wiring in Simulation/GameHud/GameController; Codex will perform that narrow integration after this checkpoint, leaving placement design reserved.

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

### 2026-10-16 — personnel slice ready; user decisions on scope

- The user decided: **Claude keeps staff careers** (hiring, files, ranks, discipline, warden and nurse posts, write-ups, watch and special duties); **Codex keeps security equipment** (towers, dogs, armed posts, power, comfort, dispatch). Order: personnel UI, then **difficulty modes** (Federal/State/City/County; Claude designs the catalog and every system it drives, Codex wires the contracts, starter buildings and new-game menu), then **resident-lives** (your handoff, read and queued).
- Merged `codex/live-operations-handoff` at `d5f89ef` into `codex/claude-personnel`. Combined checks: 88 passed, 0 failed (your 69 + 19 personnel). The social-contact regression now passes.
- Personnel files and the justice-file changes on this branch are listed in `docs/personnel-notes.md` (Justice changes are for your review, since you now integrate justice). Includes a real justice fix: court call-out at dispatch with a 09:05 deadline; far-cell residents were missing every regular hearing.
- Evidence: mutation testing caught 26/26 deliberate bugs; offline Roslyn typecheck against the Unity 6000.3.24f1 references passes for editor/macOS and iOS. Unity check request and demo script: `docs/personnel-notes.md`.
- Requests: text-field keyboard focus (Space, WASD and Esc while typing); security-query alignment for duties; placement handoff; opening-staff wages are 0 (personnel uses the standard wage for raises and severance).

### 2026-10-16 — game modes engine (in progress, paused by usage limit)

- New files: `Core/GameModes.cs` (4 jurisdiction tiers as difficulty, 12 facility types, 8 locations; contract terms, population, charges, courts, staff, rules, starter-room specs), `Runtime/GameModesHud.cs` (charter screen, `GameHud.ShowModePicker(callback)`), `Resources/GameModes.uss`, `Tools/CoreChecks/GameModesChecks.cs`. Wired through Claude's justice/personnel hooks; no Codex file edited.
- The three original contracts map to `county-intake`, `county-reentry` and `state-modernization` with identical generation and terms (checked). Explicit `World.ApplyMode(typeId, locationId)` reshapes a first-day company; the default mapping is additive only.
- **Codex wiring request:** host `ShowModePicker` as the new-game screen; build `World.Offers` from `GameModes.Offer(type, location)`; build starter layouts from `FacilityType.StarterRooms`/`Housing`/`Beds`; call `ApplyMode` right after `World.Start`.
- **Queued (user requests):** money-making prisoner programs (farm, kitchen, manufacturing, clerical, education) through the Lives activity hook; resident intake requests after building beds (per-diem); special response team (staffing); the resident-lives handoff. **Spotlight/searchlight is security equipment for Codex:** a powered night beam that detects residents in its cone, can be staffed or motorized, and causes sleep stress if aimed at housing.
- Checks: 97 passed, 0 failed; Unity typecheck passes for editor/macOS and iOS. Not yet: a critic pass on game modes, mutation tests for modes, `docs/game-modes.md`.

### 2026-10-16 — personnel critic findings (fixed 1, 4; rest open for Claude's next session)

Fixed: (1) a departed nurse's file failed validation and made saves unloadable; (4) the 81st personnel file broke loading because trimming only ran on removal. Still open, from the critic run (24 seeds × 15,000 min): (2) a default game unravels with no player action: pressure saturates, both opening officers quit by day 8, and fights and welfare penalties run away; needs pressure relief/headcount tuning or a softer burnout; (3) duty movement re-plans every minute, so staff on moving targets barely walk, which also causes false neglect write-ups; (5) commendations are free and unlimited (28 clicks takes integrity 44→100); (6) a paid background check is lost when the listing refreshes; (7) demoting an opening staff member leaves part of the raise; (8) a referral at the 12-charge cap charges $200 and does nothing; (9) without a warden, write-ups flood and the oldest open ones are evicted. Weak checks: gate post passes without the duty, and the watch check hides finding 3.

### 2026-10-16 — acknowledgement: overload, intake, contraband, modes/HUD, lives (branch `codex/claude-overload`)

- Read `CLAUDE.md`, `AGENTS.md`, this file, `docs/claude-modes-and-building-handoff.md`, `docs/claude-coordination-update.md` and `docs/claude-lives-handoff.md` at `21a0783`.
- Branch `codex/claude-overload` = `codex/claude-personnel` (`029e5d9`) merged with `origin/codex/live-operations-handoff` (`21a0783`) as `fa4bdde`. Justice conflicts resolved toward your physical interception and local lighting; kept personnel's CCTV-monitoring filter and officer detection credit. No abstract response timer restored. 112/112 core checks; offline Unity typecheck passes for editor/macOS and iOS.
- Claimed `opening-and-build-ui`, `resident-lives`, and a new `population-pressure` scope (new files only, listed in the table). Claude still does not run Unity; Play checks are requested below as milestones land.
- User request for this pass: (1) finish the open personnel critic findings and game-modes critic/mutation/docs; (2) continuous transfer arrivals from an overloaded system, overcrowding, riots, failed audits and violence as failure pressure that makes the player keep building; (3) contraband of many kinds with drone drops and other smuggling routes; (4) a thorough staged intake program, mandatory and complete for federal facilities and upgradeable elsewhere; (5) an upgrade catalog plus a roadmap of everything upgradeable; (6) graphics and character appeal; (7) a switchable first/third-person view of any character (critic run first).
- Boundaries: security equipment (towers, dogs, CCTV, lighting, perimeter, power) stays Codex's; contraband detection only queries it (`CameraSees`, `SecurityDetectionAt`, `IsIlluminated`, handlers). New physical fixtures for intake, scanning and drone defense will be requested from Codex rather than added to `FixtureCatalog.cs`; until then they are program upgrades. Character appearance goes through `PrepareResidentPose`/`AnimateResidentLife`; staff actors and lighting stay Codex's.

### 2026-10-16 — overload, intake, contraband, upgrades, appearance and ride-along (branch `codex/claude-overload`)

- **Personnel critic findings fixed** (`e884dcc`):
  - (2) off-shift rest recovery plus a daily burnout warning; a default county company keeps its staff for 14 days;
  - (3) duty-target hysteresis, so watchers keep pace;
  - (5) commendations once per 3 days;
  - (6) paid checks survive a refresh;
  - (7) exact raise reversal;
  - (8) refused referral at the charge cap;
  - (9) minor write-ups lapse without a warden and overflow drops the least serious.
  - Security posts and escape responses keep sole ownership of an officer's destination. There is a no-duty control check for the gate post. 19/19 mutants killed.
- **Game modes finished:** 24/24 mode mutants killed with new checks (`038f3bb`, see `docs/game-modes.md`). The tier `CorruptShare` now scales staff smuggling.
- **New scope `population-pressure`** (`f7c1f9e` core, `2e7d6f4` runtime, `5dd2301` checks, `0313d08` docs). New files: `Core/Population.cs`, `Core/Intake.cs`, `Core/Contraband.cs`, `Core/Upgrades.cs`, `Tools/CoreChecks/PressureChecks.cs`, `Tools/CoreChecks/LivesChecks.cs` (implements your `RunLivesChecks` hook), `Runtime/FacilityPressureView.cs`, `Runtime/GamePressureHud.cs`, `Resources/Pressure.uss`, `docs/pressure-notes.md`, `docs/upgrade-roadmap.md`. Resident-lives scope: `Runtime/FacilityLivesView.cs`. Opening-and-build-ui scope: `Runtime/GameRideAlong.cs`; GameController is now `partial`. Metas are minimal with fresh GUIDs.
- **Handed-off files edited:**
  - `Simulation.cs`: pressure hooks (`HydratePressure`, `AdvancePressure` after `AdvanceJustice`, `PressureMovement`, `PressureTarget`).
  - Meals in shifts at real table and chair places, with a 45-minute head start.
  - Plan-aware meal places in `CheckRoutes`; floor mattresses and intake cases are exempt from route blocking.
  - Revenue stops on termination.
  - `GameHud.cs`: a Pressure workspace and tab, plus a `LayoutPressure` hook.
  - `GameController.cs`: the pressure view, the ride-along camera and body cam, Esc/Tab, and `StartCharter`.
  - New-game menu: a "Charter a facility" card opening `ShowModePicker`, plus `World.StartCharter`; `World.Contract` follows the chartered mode. This closes my earlier wiring request, except distinct starter buildings per type.
  - `Personnel.cs` is Claude's.
- **Codex check files edited** (behavior they test is now Claude's `Simulation.cs`), in `Program.cs`:
  - Distinct meal destinations are now distinct (place, shift).
  - The "leave initial positions" wait is 180 minutes, because a passed breakfast shift waits for the yard.
  - The circulation-earnings window now runs through lunch.
- **Evidence:**
  - 175/175 core checks (SDK 8.0.426).
  - Pressure mutation testing: 72 deliberate bugs, 71 caught, 1 was a redundant line (removed).
  - Offline Unity typecheck passes for editor/macOS and iOS.
  - Balance probes are in `docs/pressure-notes.md`.
  - Independent critic (read-only, fuzz 1.8M game minutes with System.Text.Json and JsonUtility-style round trips) confirmed 7 bugs, all fixed (`b470a10`, `d28d8a0`):
    - contraband held by a released resident made saves unloadable;
    - yard places for a larger roster were never checked against obstacles;
    - removing an intake desk stranded a case;
    - audits fined after termination;
    - non-officers were day-only (now a night shift option);
    - drop assignments never released;
    - the drone package spun.
  - Its balance finding (building never saved the facility; refusing everything dominated) was fixed with meal grace, night staff, violence findings per ten residents, gate turn-back, revenue advances and net-position solvency. The charter flow (`08df2a1`) now gives each type its own capital and land.
  - Later: meals across every dining room with a serving counter and nearest-place assignment (`76707da`), meal trays instead of starvation, and load repair for residents in route-planner dead spots (`1831418`, found by fuzz).
  - A sensible builder now survives 9/9 runs at 21 and at 35 days across county, state-medium and federal-camp (before: 0/24).
  - Final fuzz: 705,600 minutes and 5,238 reloads, 0 failures.
- **Unity check request (Codex as runner):** the 12-item script in `docs/pressure-notes.md` § "Unity check request", covering the Pressure tab fit at 280–360 px, bus/gate/unloading, desk lanes, drone, riot, audit inspector, mattresses and sleeping, seated meals, faces and jumpsuits, ride-along and body cam, mid-event JsonUtility saves and the console.
- **Codex check edits:** `Program.cs` "leave initial positions" now excludes residents separated after an early fight. `PersonnelChecks` watch threshold is 75%: with hysteresis 82–96%, without it 41–69%.
- **Handoff:** local locks for `opening-and-build-ui`, `resident-lives`, `population-pressure` and `personnel` are released in Claude's clone. The rows stay "Claude" until you integrate `codex/claude-overload` (head at the end of this entry); then hand them back or keep them reserved.
- **Interface requests (your files):** listed in `docs/pressure-notes.md` § "Interface requests":
  - `Walk` caches empty routes forever;
  - an `ExtraEscapeFactors` hook;
  - fixtures for body scanner, booking desk, drone detector, yard netting and walk-through detector;
  - starter layouts with a booking desk near the sally port for state and federal types;
  - Justice penalties after termination;
  - a staff-appearance hook;
  - physical visitation.
  - The ride-along control phase needs movement hooks listed in `docs/possession-plan.md`.

### 2026-10-17 — acknowledgement and merge of Codex `9b6f2e4`; economy and roles design

- **Acknowledged.** Codex's `e3349b8`/`9b6f2e4` (integration of Claude `b470a10`) is merged into `codex/claude-overload` with Claude's six later commits (`d28d8a0..5ba7e88`). Scopes held: opening-and-build-ui, resident-lives, personnel, population-pressure.
- **Conflicts resolved:**
  - `Simulation.cs`: Codex's `Contract` (explicit terms) kept. Claude's `StartCharter` removed in favor of Codex's `StartMode`/`ModeLayout`; the charter test now runs `StartMode` and requires at least one intake lane.
  - Load repair runs after `RestoreMovementMemory` and drops the repaired resident's saved route.
  - Meals: Claude's nearest-place meal plan, plus Codex's `MealDue` (eating) / `MealTravelDue` (45-minute approach) split and shared serving clock. The 60-minute hungry grace now applies only to the last shift, so no seat is shared across blocks.
  - `GameController.cs`/`GameHud.cs`: Codex's opening flow (`StartModeCompany`); Claude's `StartCharter`/`AcquireCharter` removed. `docs/game-modes.md`: Codex's version.
- **Codex check edited:** `MealFlowChecks.cs` finds shared seats and shifts from the meal plan (`ServiceDestination`, `MealShift`) instead of `index % places`. Same assertions.
- **Evidence:**
  - 208/208 core checks (union of both parents' 175 and 202 test names; none lost).
  - Offline Unity typecheck passes for editor/macOS and iOS.
  - Builder balance rerun on the merged code: 21 days: 9/9 survive across county-intake, state-medium and federal-camp, with 0 riots, 0 strikes and 0 starving (trays cover anyone past four shifts). 35 days: 9/9 survive with no termination and nobody starving.
    - Federal camp and state medium: 0 strikes.
    - County intake is the tightest: 0/2/2 strikes, 0–2 riots, unrest 75–91 against 58–74 before the merge. Watch county balance when Lives and Economy land.
- **Design for review:** `docs/economy-and-roles-plan.md` (user request). It covers:
  - prisoner trust fund, jobs and wages;
  - commissary with a weekly price index;
  - kitchen staff, food quality, menus and diets;
  - informants, snitch jackets and protective custody;
  - public defenders and paid counsel;
  - decision-card role play.
  - Proposed Codex hooks: `CounselOdds` in `Justice.Resolve`, kitchen fixtures, storage racks, an attorney room, and possibly `ValidRole` for kitchen staff. Nothing is implemented yet; the build waits on the user's go.

### 2026-10-17 — economy, informants, kitchen, counsel and role cards (user request: "start with the items in order")

- **Base.** Fast-forwarded to Codex `415c1d4` (searchlights; it includes Claude `650e3ce`). Claimed opening-and-build-ui, population-pressure, personnel, resident-lives and a new scope, `economy` (row above).
- **Built**, in the plan's order (`docs/economy-and-roles-plan.md`, now marked implemented):
  - A. Prison economy: trust fund with its own reconciled book, jobs from fixtures, payroll, family deposits and fees, a commissary with a seeded 3-day price index and markup, soups as currency, the store man, debts, black-market prices in soups. `docs/economy-notes.md`.
  - C. Informants and snitches: intel and who knows it, notes, a tip line, recruited informants with hidden reliability and double agents, tip actions and exposure, snitch jackets, rumors, leaks, paperwork checks, protective custody, trust in staff. `docs/intel-notes.md`.
  - B. Kitchen: catering stays the baseline; a kitchen upgrade plus a Cook post (role Technician) cooks from Budget–Premium menus, with diets, loaf, holiday meals, theft, knives, poisoning and fires.
  - D. Counsel: public defender caseload from the transfer backlog, paid tiers in instalments, motions, bond, discovery, sentencing argument, a law library.
  - E. Taking a role: decision cards on the ride-along for prisoners, officers and the warden, each running the same core methods as the AI, with a 60-minute default. B, D and E are in `docs/kitchen-counsel-roles-notes.md`.
- **Edited Claude-owned existing files:** Simulation.cs (MealGain hook, service accounting), Population.cs, Contraband.cs, Personnel.cs (Intelligence duty, Cook post, fight hooks), Intake.cs, Upgrades.cs (24 → 29 upgrades: 23 available, 6 planned), GameHud.cs (3 hooks), GamePressureHud.cs, GameRideAlong.cs, Pressure.uss, GameModesChecks/PressureChecks/LivesChecks. No Codex files edited.
- **Justice boundary (please review).** Counsel reads legal state and acts only before a hearing (motions, bond via Justice's own `Release`) or after it:
  - `NextCourtMinute` moves earlier for private counsel, or later for an overloaded defender;
  - `SentenceDays`/`ReleaseMinute` are cut at sentencing.
  - If you prefer this inside `Resolve`, a `CounselOdds(resident, ref release, ref sentence, ref termDays)` hook would replace the after-the-fact adjustments.
- **Evidence:**
  - 286/286 core checks (78 new across five suites, with pinned literals).
  - Offline Unity typecheck passes for editor/macOS and iOS.
  - Fuzz, with every new action plus Unity-style round trips: 345,600 calm and 362,880 chaos minutes, 5,267 reloads, 0 failures after one fix (the intelligence demo could exceed the 12-knower cap).
  - Builder balance on the final code: 9/9 survive 21 days (0 riots, 0 strikes); county and state 6/6 survive 35 days (0–1 strikes).
  - An earlier run lost 2 county contracts: soups-only payment with no store turned every black-market deal into debt. Fixed with trust-transfer payment and proportional debt consequences, and documented.
  - Mutation testing: 50 deliberate bugs across the five systems are running; results follow in the next entry.
- **Unity check request (Codex as runner):** the lists in `docs/economy-notes.md`, `docs/intel-notes.md` and `docs/kitchen-counsel-roles-notes.md`: panel fit at 280–360 px and phone landscape, the demo buttons, the cook at the counter, protective custody staying home, and role cards pausing play.
- **Interface requests (Codex's files):**
  - fixtures: storage rack, washer/dryer, workbench, kitchen range/prep/cooler;
  - `StaffEffect` text for the Cook post (it currently shows the technician description);
  - an escape-plan hook for escape intel;
  - optional note and meeting props.

### 2026-10-17 — placement fix: furniture and exterior lights refused ("plan needs attention")

- **User report:** exterior lights refused everywhere outside, and a bed moved inside its room refused as if it hit a wall. Reproduced in core with a probe across all 12 start layouts at four times of day. Three causes, all fixed in Claude's files:
  1. **`Quote` re-checked every existing furnishing against every wall.** In the secure-cells layout (county-maximum, state-modernization) the second bed of each cell sits flush on the cell wall (0.00 m, under the 0.11 m clearance), so every plan failed with "A wall intersects furnishing": 0 of 80 light spots, every bed move. Now only conflicts the plan creates are checked: its furnishings against all walls, its walls against all furnishings.
  2. **`CheckRoutes` treated anyone standing in a planned furnishing's clearance as trapped.** This caught the bed owner asleep beside the bed being moved, and staff walking past a light; about 25% of small bed moves failed. Now only walls trap people. Fixed staff posts (`Station`) still block, and Codex's direct-overlap rule is kept: no furnishing on top of a staff member. After handover, anyone a new item lands on steps aside (`StepAside`).
  3. **Furniture placement used the wall-drawing snap.** `Geometry.Snap` pulls points onto walls within 0.65 m, so a bed or light dropped near a wall landed on it. Furniture tools now snap to the 0.5 m grid, then `World.FitFurnishing` settles the item at the nearest spot the quote accepts: flush against the wall with 0.13 m clearance, walkable access in front, inside a room unless outdoor, no overlap. Walls and wings keep the wall snap.
- **Probe after the fix:** ordinary layouts 24/24 bed moves; secure-cells layouts 78/81 light spots. The rest are lights genuinely on a wall or an item.
- **Checks:** 293/293 (5 new placement checks: unrelated construction in secure-cell starts, still refusing furniture through walls and walls through furniture, moving an occupied bed through handover with nobody left inside furniture, flush fitting against interior and exterior walls, walking past versus standing on). Unity typecheck passes for editor/macOS and iOS.
- **For Codex (`ModeStarts.cs` `ModeLayout`, secure cells):** `Item("Bed",-9.5f,y+.9f,0)` puts the bed's edge exactly on the cell wall at `y`. y+1.05 would clear it. Construction no longer depends on it, but the bed visually touches the wall.
- **Unity check request:**
  - In county-maximum, place an exterior light anywhere in the yard: the quote is valid.
  - Move a bed by dragging it against a wall: the preview settles flush, green, beside the wall.
  - Move an occupied bed at night: the plan is accepted, and after handover the owner sleeps at the new bed.

### 2026-10-17 — mutation results for the economy scope; handoff

- **Mutation testing:** 50 deliberate bugs across Economy, Informants, Kitchen, Counsel and Roles.
  - The first pass caught 45; the 5 survivors got new or pinned checks and are now caught: 50/50.
  - The survivors were the √ in exposure, protective custody out of fights, the holiday interval, the bribe write-up target and the defender delay.
  - The exposure check exposed a real bug: a prevented attack removed its intel before exposure ran, so nobody who knew was ever exposed. Fixed.
- **Final:** 293/293 core checks. Unity typecheck passes for editor/macOS and iOS. Builder balance on final code: 9/9 at 21 days, 9/9 at 35 days (federal rerun included).
- **Handoff:** local locks released in Claude's clone. Rows stay Claude until you integrate `codex/claude-overload`.

### 2026-10-17 — resident lives: activities, memories, story chapters, poses (acknowledging Codex `e6e299d`)

- **Acknowledged.** Codex's HUD meters and night-watch publication `e6e299d` is merged into `codex/claude-overload` (`9a20549`; clean, 293/293). Scopes claimed: resident-lives, opening-and-build-ui, population-pressure, personnel, economy. Codex's `StatMeter`/metrics hooks are untouched.
- **Built**, per `docs/claude-lives-handoff.md` (details in `docs/lives-notes.md`):
  - **Activities at real furnishings**, in free time only:
    - reading at shelves, exercise at benches, television (seats on nearby benches and chairs), study at table places, talking with friends at a common-room spot, quiet time on the bunk;
    - capacity per furnishing, reachability, staggered personal replanning;
    - `ChooseResidentActivity` stays a pure read of the stored plan, and replaces only a trip home.
  - **Progress:** books, fitness, certificates (+5% sentencing credit through counsel), one-way mentoring.
  - **Memories:** up to 12, with same-day dedupe. They come from relationships, fights, activities and every Claude system.
  - **Story chapters:** derived from state.
  - **Poses:** Read, Exercise, Watch, Study (bench-aligned table seat) and Rest (the verified sleep placement). The head pose runs after `MoveActor`'s idle glance, which takes the head back afterwards.
  - **Life block** on the resident card.
- **Files:** new `Core/Lives.cs`, `Runtime/GameLivesHud.cs`, `Resources/Lives.uss`, `docs/lives-notes.md`. Edited, all Claude-owned: `FacilityLivesView.cs`, `LivesChecks.cs`, `Simulation.cs` (`Relationship` partial), one-line memory calls in the Economy/Informants/Counsel/Personnel/Contraband/Population/Kitchen files, the `GameEconomyHud` card order, and `GameModesChecks` (the fight-scale comparison also holds activities off).
- **Evidence:**
  - 303/303 core checks (10 new lives checks, including the real hook's purity and priority, capacity, and a reload mid-activity).
  - Unity typecheck passes for editor/macOS and iOS.
  - Builder balance: 9/9 at 21 days (0 riots, 0 strikes) and 9/9 at 35 days across county, state and federal (at most 1 strike). County unrest 65–78, previously 65–84.
  - Fuzz with the lives demo plus Unity-style reloads: 529,920 minutes, 3,931 round trips, 0 failures.
- **Unity check request:** `docs/lives-notes.md` § Unity check request (five items: activity poses at real furnishings, meal and night priority, Life block fit, head release after reading, reload mid-activity). The disposable demo is the "Demo · free time" button on a resident card (09:00–10:00 or 20:00–21:40).

### 2026-10-17 — build flow: drag a selected furnishing to move it; lives mutation results

- **Drag-to-move** (`GameController.cs`, Claude's): in Inspect, pressing on the already-selected furnishing and dragging moves it instead of panning, for both mouse and touch.
  - It previews through the new `FitFurnishing` (it settles flush, red or green).
  - Letting go over the floor opens the move plan with its quote; letting go over the interface cancels.
  - Dragging anywhere else still pans. Alt-drag and right-drag still orbit. The two-finger gesture is unchanged.
  - The Inspect hints now mention it.
- **Lives mutation:** 15 bugs, all 13 valid ones caught. 2 were redundant capacity code, now removed: places are the only limit. Checks now pin literal minutes, the memory bound, one-way mentoring between near-equals, and a conversation's exact per-minute effect.
- **Checks:** 303/303. Unity typecheck passes for editor/macOS and iOS.
- **Unity check request:**
  - Select a bed, then drag it across the room: the preview follows and settles flush against walls.
  - Let go: the move quote appears.
  - Drag a bed onto the side panel: the move is cancelled and the selection kept.
  - On touch, a one-finger drag that starts on the selected item moves it; one that starts elsewhere pans.

### 2026-10-17 — acknowledgement of Codex `358b028` (Home/save shell, physical incidents)

- **Acknowledged.** Merged `origin/codex/live-operations-handoff` at `358b028` into `codex/claude-overload` (clean textual merge). This brings Claude's unintegrated `9a20549`, `ce9a739` (resident lives) and `06df24e` (drag-to-move) together with Codex's Home/save/settings/command shell and `IncidentScenes`.
- **Scope:** Claude keeps resident-lives, opening-and-build-ui (minus the Home/save/settings/command-shell exception), personnel (minus the incident start/resolve and DirectStaff ownership guards), population-pressure and economy. GameHud/GameController keep Codex's frontend partials and hooks. IncidentScenes, the incident HUD/view/control and the Unity editor stay Codex's.
- **Checks on the merge:** 315/315 core. Unity typecheck passes for editor/macOS and iOS.
- **Next (Claude):** make lives activities and poses defer to incident ownership. Then continue lives, stories and the build flow inside Claude's files.

### 2026-10-17 — user direction: land every verified milestone on `main`

- **User request (2026-10-17):** asked why the work wasn't on GitHub. Both agents had been pushing only to side branches, so `main` had stayed at `f4266ca` while those branches were 50+ commits ahead. The user then chose to "update main now + always".
- **Done:** fast-forwarded `main` to `7d15617`. That commit contains all of Codex's work through `358b028` and all of Claude's through `06df24e`, with no rewrite.
- **For both agents from now on:** after each validated milestone, merge the latest `origin/main` into your branch, rerun the checks, then push the branch and fast-forward `main`. If `main` has moved, merge it first; never force-push `main`. Feature branches stay as they are for in-progress work. This overrides "no remote pushes without user instruction" (Rules 5) and the matching line in CLAUDE.md for `main` publication.

### 2026-10-17 — lives, role cards and life poses defer to incident ownership

- **Why:** this follows the `docs/claude-start.txt` contract. Before the change, a resident walking to a fight could keep reading, keep earning progress and keep a seated pose. A card could also pause play on a controlled prisoner mid-fight.
- **Core changes, all in Claude's files:**
  - `Lives.FreeTime` is false while `IncidentOwnsResident`, so the activity ends and nothing accrues or replans until the scene resolves. Escapees were already excluded by custody "Escaping".
  - Ending a Talk now ends it for the partner too.
  - `Roles`: the new `Asking(r)` / `Asking(s)` checks skip cards for a person whose incident owns them, and the ordinary rules decide instead. This covers store, job, counsel, card game, bribe, tip search and prisoner moments.
- **Runtime change:** `FacilityLivesView.PrepareResidentPose` drops any everyday pose immediately for fight participants and the escapee. Codex's `RefreshIncidentVisuals` still runs last and owns the body.
- **Checks:** 317/317 core, including 2 new checks. All 3 targeted mutants were caught. Unity typecheck passes for editor/macOS and iOS.
- **Unity check request:** stage the fight demo while one participant is reading at a shelf or sitting at the TV. They should stand and walk without a seated or reading carry-over, and the Life card should show no activity until the scene resolves.

### 2026-10-17 — user assignment: iOS / TestFlight builds go to Claude, from Claude's own clone

- **User request (2026-10-17):** "a playable version I can put on TestFlight so I can play it a bit on my phone". When asked who builds, the user chose Claude, from Claude's own clone.
- **What this means:** native iOS player builds and TestFlight uploads are Claude's, run against `ClaudeWorkspace/cellblock-holdings` only. Codex's checkout and its open editor are never touched. Codex keeps the unity-editor scope for its checkout: Play reviews, captures and `ProjectSetup`. Claude does not edit `ProjectSetup.cs` or any other existing Editor file.
- **New file (Claude):** `Assets/Cellblock/Editor/IosBuild.cs` (+ meta). `IosBuild.Simulator` / `IosBuild.Device` call `ProjectSetup.Validate()`, build the Xcode project into the ignored `Builds/iOS/<Simulator|Device>`, and restore the iOS player settings afterwards.
- **Open decision (user):** the bundle ID is undecided ("later"). Codex's `ProjectSetup` still writes `com.cellblockholdings.game`. No App Store Connect record will be created until the user picks; if the choice differs, Claude will ask Codex for that one-line change.
- **First step:** an iOS Simulator build, which needs no signing, to find native-player problems (stripped shaders, Resources, safe areas) before a device build.

### 2026-10-17 — build flow: drag to place, and drag from a catalog card (Codex handoff request item 1)

- **Files (Claude's opening-and-build-ui):** `GameController.cs` input and `GameHud.cs` catalog cards only. No frontend, shell, incident or settings code was touched.
- **Drag to place:** with a furnishing, bed, table or prefab tool active, a one-finger or left-mouse drag that starts on the map now shows the fitted ghost and opens the quote when you let go. Before, letting go did nothing and a separate exact tap was needed.
  - On touch the ghost sits about 0.42 in above the finger, so the finger doesn't hide it. A plain tap still places at the exact point.
  - Moving an already-selected furnishing keeps its exact, unlifted position.
- **Drag from the catalog:** a furnishing card arms on press (`PressedCatalogKind`, via a TrickleDown `PointerDownEvent`). If the pointer leaves the panel before letting go, the controller selects that furnishing, closes the compact inspector and lets the ghost follow the pointer. Letting go over the map opens the quote. Letting go over the interface cancels the placement and keeps the tool selected.
  - A pinch, a blocked-input overlay, a touch cancel, a ride-along and an open quote all cancel or refuse the drag.
- **Checks:** no core change (317/317). Unity typecheck passes for editor/macOS and iOS.
- **Unity check request:**
  - (1) Choose a bunk card and drag across the floor: the ghost follows, and letting go opens the quote.
  - (2) Press a bunk card in the catalog, drag onto the map and let go: the quote opens.
  - (3) Drag a card off the panel, then back over the panel, and let go: no quote, and the bunk tool stays selected.
  - (4) On a phone layout, the compact inspector closes when the drag leaves it.
  - (5) A two-finger pinch during a placement drag places nothing.
  - (6) Inspect-mode pan, drag-to-move of a selected item, and two-finger orbit and zoom are unchanged.
  - Suggested `docs/controls.md` line for Codex: "Furnishing tools: drag on the map or drag a catalog card onto it; let go to review the quote."

### 2026-10-17 — first native iOS player: Simulator boot passes; TestFlight script ready

- **Simulator result:** Unity 6000.3.24f1 `IosBuild.Simulator` and then Xcode 26.5 (`iphonesimulator`, x86_64, Release) both succeeded. The app boots on an iPhone 17 Pro simulator (iOS 26.5) to the Home screen in landscape: night artwork, Begin your story, Save library and Settings. The text clears the Dynamic Island, and there are no exceptions or missing-shader logs.
  - Unity ships the Simulator player only as a Development variant (`Variations/Development_simx64_managed`), so the debug console and "Build type Development" are simulator-only. The device build uses `Releasearm64`.
  - The simulator GPU lacks float MSAA, which accounts for the "Attachment 0 ... 1 samples but 2 requested" and RenderPass messages. A real iPhone has float MSAA.
- **Shaders in the player:** URP Lit (normal-map and emission variants kept by the Resources materials), Cellblock/SearchlightBeam, Skybox/Procedural, UI/Sprites and URP internals. All three runtime `Resources.Load` lookups are present.
- **Not verified:** the in-world facility render and touch on the player. The Simulator can't be driven without accessibility permission, so those wait for the first TestFlight run on the user's phone.
- **New `Tools/testflight.sh`** (Claude). It runs the Unity `IosBuild.Device` export, sets `ITSAppUsesNonExemptEncryption=false`, then archives and exports-and-uploads with xcodebuild, automatic signing and team FX4R73M22M (`--no-upload` exports only). Afterwards it restores the shared settings Unity rewrites. `IosBuild` now honours `CELLBLOCK_BUNDLE_ID` and restores it.
  - Blocked only on the user's bundle-ID choice and the App Store Connect record, which Claude will create in Chrome when the user is ready.
- **Hygiene:** a fresh Unity import rewrites `Assets/DefaultVolumeProfile.asset`, `Assets/UniversalRenderPipelineGlobalSettings.asset`, `ProjectSettings/{Graphics,Project,Quality}Settings.asset` (iPhone batching). These are discarded in Claude's clone and never committed.

## Codex handoff request (personnel + placement)

Please include in your next handoff to Claude:
1. **Drag-and-drop placement scope (the user's request: "drag and dropping of elements like lights, tools, tables etc to flow better").** Either hand Claude the placement input path in `GameController` (`InputUpdate`/`PreviewAt`/`Tap` for `BuildTool.Furnishing`, `RotatePlacement`, `SetDraft`), `ConstructionGuides`, and the catalog cards in `GameHud`, or add hooks for: drag from a catalog card into the world, drag an existing furnishing to move it, a live valid/invalid ghost with cost while dragging, a rotate gesture or handle, and drop-to-quote. Claude's proposed flow: press and hold a catalog card, drag into the world, see the ghost snap with green/red validity and price, release to open the quote, one-tap approve; drag a selected furnishing to move it the same way; twist or tap a rotate handle; and optionally batch several placements into one quote.
2. **Optional staff hooks** (the posts work without them): `partial void StaffDutyTarget(StaffMember staff, ref Point goal, ref string duty, ref bool handled)` in `AdvanceStaff`; `static partial void ExtraStaffRole(string role, ref bool valid)` in `ValidRole`, if you want Warden or Nurse as real roles later; and the earlier `IsPresent` filter for `AdvanceRelationships`.
3. When you integrate, please note whether you want the personnel posts migrated to real roles.

## Requested handoffs / check requests

Claude justice branch `49aff6e` is merged locally. Codex integration fixes: presence-aware social/misconduct/workload/service accounting, real blocked-release waiting, and Unity inline-serialization payload discrimination. Combined69 core checks and eight actual Unity justice frames passed (no runtime errors). The empty-draft regression passes; next `resident-lives` handoff is prepared in `docs/claude-lives-handoff.md`. Codex remains the sole Unity runner and owns security/door/services implementation; no overlapping edits.

Next Claude handoff is ready at integration base `9624674`; `docs/claude-start.txt` is the short copy/paste prompt and `docs/claude-lives-handoff.md` is the exact scope/hook contract. Codex continues doors/security/services; no new overlapping file ownership.

Codex integration update: fetched `origin/codex/claude-justice-custody` at `49aff6e` after the user reported completion. Preserve and merge Claude’s log and scoped files. User also requests richer prisoner animation/lives/stories for Claude’s next slice; Codex retains the new connected-door/security/power/upgrade mechanics.

Codex handback: implementation `a6d2afc` is committed. Both final Unity reports passed (nine focused security frames and sixteen general frames), no runtime errors. Core transcript records 84 passed / 0 failed. Publishing this exact handoff and releasing local Codex scope locks. No source edit to the Claude-reserved files will follow without a new ownership update.

### 2026-10-16 — Codex people and facility milestone

- Integrated ready personnel `506633e`, retaining current security/presence/save behavior. Fixed personnel/security duty exclusivity, physically present escorts/CCTV monitors, legal v1 migration and modal input guard.
- Added scoped FacilityEvents/CapitalCases/ResidentTraffic core, events HUD/view, detailed people and public frontage. Critic found and fixed premature inspector findings and legacy-save authored-intake inference. Authored intake now uses an explicit new-world hook; story schema 2 handles the nested transport migration.
- Actual focused Unity Play review: eleven captures, no runtime errors, in Builds/StoriesReview/2026-10-16. 117/117 core checks pass, including legacy empty-company migration and once-only grants/fees. Existing recovery assets and duplicate ShaderGraph settings are preserved.
- Fetched subsequent Claude handback `029e5d9` (mode catalog/UI plus two personnel save fixes). Not yet integrated at this checkpoint. Codex will merge it next, wire opening contracts/layout/menu and check it in Play, as requested by Claude's mode handback. Existing drag-placement/Lives work stays reserved for Claude. Claude's acknowledged remaining personnel issues must remain explicit in the handoff.

- Codex checkpoint `eb02c7d` saved personnel/events/character detail; 117 core checks and 11 focused Unity frames passed. Integrating ready mode catalog `029e5d9` now. Narrow mode wiring in Simulation/GameController/GameHud and GameModes files is assigned to Codex for this handback. Claude retains broader placement/HUD redesign and Lives.

### 2026-10-16 — Codex opening-mode integration complete

- Merged Claude's `029e5d9` mode catalog and two personnel archive fixes; preserved both logs and current security/story behavior. Added StartMode atomic acquisition/layout/ApplyMode, explicit financial terms, actual dorm/secure/single-cell layouts, a three-step new-holding wizard and saved-mode labels. Legacy companies keep their terms/buildings.
- Critic's free-recharter pricing bug is fixed: an acquired explicit charter cannot swap terms. The operating-facility charter row is hidden. Only a new holding acquires a new charter.
- Added physical one-officer-per-resident movement authorization for supermax routines and court/release movement; escape attempts remain breaches. Fixed the shared moving-target route's zero-distance first waypoint, so watch officers actually walk instead of replanning in place. Added dedicated checks for both.
- Final core: 133 passed/0 failed. Twenty-one actual Play frames passed after phone footer bounds, opening card layout and resolved-visitor cleanup fixes. Nine final security/construction/night/phone frames also passed, with no runtime errors in either report. No phone performance or full long-run balance claim.
- Claude's remaining personnel critic items remain queued for its next pass: workload/burnout tuning, commendation rate limits, pending background checks surviving pool refresh, demotion wage accounting, charge-cap referral fees and write-up queue policy. The moving-target defect is addressed here. The nurse/archive save defects were integrated from your completed branch.

### 2026-10-16 — Codex final handback

- Combined evidence: 133 core checks, 21 actual people/events/mode Play frames, and 9 final security/construction regression frames; no runtime errors. Reports and core transcript are committed in docs/reviews; full frames in ignored Builds/StoriesReview/2026-10-16 and Builds/SecurityReview/2026-10-16/personnel-modes-regression.
- Resolved visitors and generated meshes are retired; the Play runner checks this. Phone action bounds and opening-card size are asserted. Catalog room wording now matches actual double/single/dorm/exercise layout capability.
- docs/claude-start.txt is the copy/paste handback. Claude keeps staff careers and the reserved Lives/drag-HUD scopes. Preserve the pure ChooseResidentActivity query contract, physical escorts and separate generated heads/held props. Existing company slots and unrelated Unity recovery/settings files remain untouched.
- Publishing the reviewed branch with the user's standing new-branch push authorization, then releasing local Codex locks. Warden/Nurse remain posts. Long-run staff balance/exploit items above and native device touch/performance are still open, not claimed complete.

### 2026-10-17 — Codex direct doors, Claude review and graphics pass

- User explicitly requests click-at-position connecting doors, continued feature work, premium people/items/shadows and a critic review of Claude work. Published completed a393197 to the shared branch before new work.
- Fetched Claude overload snapshot b470a10; merging locally for review, preserving newer modes, events, custody, prices and both agents logs. Claude retains its separate clone/branch; no unfinished remote working tree is touched.
- Narrow user-requested exception to existing-file reservation: Codex owns door hit testing/draft controls in GameController/GameHud plus new door helper partials for this pass. No unrelated placement redesign. Codex graphics work remains in presentation/lighting/item files. Both scopes are logged before editing.
- Critic identified proposed-wall hit-test failure, invisible placement instructions and failed taps exiting the door tool. Codex will make pending geometry directly clickable with a live opening preview, preserve quote/approval/handover and retain valid plans on rejection.

- Combined engine-independent checks: 197 passed / 0 failed after adding direct-door and continuous shared-boundary checks and retaining security-post ownership against personnel duties. Critic reviewed doors, curve splitting, world-switch undo, material retention, bevel topology/cleanup, bench poses and visitor lifetimes. Confirmed findings were corrected.
- Presentation scope includes selective cached furniture/coach bevels, retained Lit normal/emission templates, procedural micro-relief, stronger shadows/contact shading, clear clothing contrast/shoe details, corrected bench-normal seating and the pressure auditor using the shared articulated Person. Pressure meshes/materials dispose explicitly; world changes clear resident look caches.
- The first 12-frame actual Play integration passed without runtime errors. Visual inspection prompted stronger meal-pose assertions and bench alignment, persistent phone door instructions, shared-door frames, a detailed audit visitor and coach refinement. Final focused Play and existing security/story regressions are in progress; do not treat the first capture as final visual evidence.

- Additional narrow integration correction authorized by the user's Claude review/natural-flow request: Simulation meal-shift timing. Critic confirmed the 45-minute early window and individual routine offsets allow adjacent shifts to eat at identical seats simultaneously. Codex will use a shared non-overlapping serving schedule when tables are reused; walking/personality offsets remain elsewhere. No unrelated placement/HUD redesign.

- Save-continuation correction: the revised schedule exposed a real loss of route queues across saves (first resumed tick changes routes, later contraband outcomes diverge). Codex owns a bounded additive movement-memory record and route helpers across the current core route writers, including intake/personnel/justice/security/workers. Critic recommends persisting both residents and workers; only the position/decision timer is insufficient. Preserve older saves by hydrating an empty optional record.

- Final core candidate: 202 passed / 0 failed. Shared meal service/travel are separate; early arrival waits outside the current seat, four-shift occupancy and food rules are checked. Close-watch pace is physical and bounded; the regression records 272/300 close minutes. Null/legacy movement memory, malformed cursor/point data, old geometry invalidation, a 30-minute exact resident/staff mid-walk continuation and the existing week-long population/contraband reload check pass. Critic found no movement-memory blocker; endpoint-to-target validation is a future defensive hardening opportunity, with current writers preserving that invariant.

- First final security run completed its nine scenarios but caught a real Unity-only deferred-Destroy bug in Claude's clothing cache. Dress queried inactive source renderers in the same frame as mesh combining, then a later intake classification recolor dereferenced a destroyed source. Fixed by filtering inactive source objects before caching, defensive Unity-null checks and a new after-frame binding assertion. Critic confirmed the cause/fix. Re-running all final Play reports on the corrected source; no passing claim for the failed report.


### 2026-10-17 — validated direct-door/pressure/presentation handback

- Final source: 202 core checks passed / 0 failed. Actual Unity 6000.3.24f1 import/API compilation and generated project validation passed. Final corrected-source Play matrix: 12 focused door/polish/pressure captures, 9 construction/security captures and 21 people/story/menu captures, all with no runtime errors. Phone approval bounds and after-frame clothing bindings are asserted; the prior failed report is superseded by the re-run, not counted as a pass.
- Committed evidence: docs/reviews/2026-10-17 core transcript and three JSON reports. Full frames remain local in ignored Builds/{PolishReview,SecurityReview,StoriesReview}/2026-10-17/final. Existing company slots, Unity recovery assets and duplicate ShaderGraph settings are preserved. Preview companies never write a company slot.
- Completing the merge of published Claude b470a10 with current charters, events, security and justice; publish on the already authorized codex/live-operations-handoff branch. docs/claude-start.txt is the new copy/paste handoff. Return the editor to ordinary Play/menu, then release Codex's local scope locks. No second Unity runner was launched.
- Claude's next priority remains richer named lives/memories/programs and broader catalog/HUD work after acknowledging the latest branch and claiming scope. Preserve direct doors/undo, shared serving and staged approach, movement-memory helper ownership, current acquisition terms, custody, pure routine queries and active-renderer-only clothing caches. Production character assets, actual phone gestures/performance, native stripped-player rendering, and long-run balance remain open.


- Published implementation merge **e3349b8** to origin/codex/live-operations-handoff (parents a393197 and Claude b470a10). Unity reports CELLBLOCK_PLAY_MODE_READY in the ordinary game/menu. Codex released game-core, game-runtime, justice and unity-editor locks; documentation lock will be released after this publication note. Only the unrelated pre-existing recovery folder/meta and duplicate ShaderGraph asset remain untracked. The copy/paste handoff is ready; Claude should fetch/merge the latest shared branch and acknowledge its new branch/SHA before resuming scoped work.
