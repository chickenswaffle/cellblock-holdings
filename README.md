# Cellblock Holdings

A Unity reboot of a prison ownership and management simulation for iPhone, iPad, Mac, and potentially Windows/Steam. The player chooses a contract, takes over an editable starter facility, and connects building decisions to people, construction, and business results.

**Current milestone: local Unity playable.** The project imports and runs in Unity 6000.3.24f1 on this Mac. The latest staff-encounter milestone passes490 executable core checks, including reviewed sanitation, yard life, staff actions, economy, intelligence, kitchen, counsel and role-card systems. The new-holding flow offers City/County/State/Federal jurisdictions, twelve institution profiles and selected or random eligible locations with matching finances, populations and editable layouts. Play includes detailed people, personnel files/recruitment, transfer coaches and staged intake, contraband and disturbances, live inspections, goals, protests, fictional capital-case proceedings, physical arrivals/building/security and packaged prefab units. See [mode integration](docs/game-modes.md) and [facility stories](docs/facility-stories.md). Earlier [natural movement and human detail evidence](docs/natural-character-motion.md) is recorded separately; the later parked human-detail candidate is not applied to the current playable. Published human models are integrated; Claude owns the rig/action adapter and continuing movement/control refinement. Native builds and actual phone performance remain unverified; the [visual study](docs/concepts/visual-direction-study.png) remains an art direction reference.

The [published Claude integration review](docs/claude-integration-review.md) combines those systems with planted-foot locomotion and detailed people. Staff actions, hygiene services and yard routines preserve incident ownership; keyboard/mouse, touch layouts and virtual-controller look/action checks run in the installed editor with Both input handlers.

The new [staff Actions menu](docs/staff-encounters.md) lets an officer choose a conversation, pat-down, pocket search, hold, cuffs, ground restraint, escort, release or confinement for the person they face. **E / right click** opens choices; **F / left click** strikes; **G** holds/cuffs/escorts; **V** changes perspective. Searches take time; grounded custody now includes a supported get-up before escort walking begins. Existing conduct consequences apply. Phone-sized layouts, desktop/controller look and live actions are reviewed in the installed editor; actual-device touch/performance remain open.

The [staff combat and backup flow](docs/staff-combat.md) adds repeated resistance, directional guard, shoves, stamina/balance and persistent officer injuries. **Actions → Call backup** sends an available line officer by a real route; physical arrival improves compliance/cuff opportunities. **Dismiss backup** releases them. Posts and higher-priority incidents keep their staff. See [backup rules](docs/staff-backup.md) and [current evidence](docs/implementation-status.md).

The new [Projects & crews desk](docs/construction-project-desk.md) shows every approved project, progress stages, blockers, prerequisites and crew assignments. Open it from Facility or Control; viewing/filtering leaves the current game speed running.

The new [Home, Continue, save library, settings and command HUD](docs/home-save-and-settings.md) use a shared night-campus/ink/brass theme. Continue finds the last played usable holding or the newest available fallback. Autosave is visible and configurable, with protected backup recovery. The centered six-workspace dock and live capacity/stress/risk meters make the in-game change visible; [People/Control section navigation and numbered meters](docs/hud-metrics.md) remain integrated. See [current validation](docs/implementation-status.md) for the final review counts and limits.

[Physical incident scenes](docs/incident-scenes.md) make confrontations, guard intervention, escape radio calls and pursuit visible. In the editor/development build, **People → Run fight scene ·1×** or **Run escape scene ·1×** starts and frames a live incident in the current holding. Watch/Pause controls stay on the incident card; drag or use camera keys to take over. A real officer must complete the radio call before automatic gate closure/lockdown, and physically reach an escapee to intercept. These demonstrations affect the current holding's records.

## Open on a Mac

For a local development session continuing this milestone, read [the Mac handoff](docs/local-mac-handoff.md).

Use an installed **Unity 6 editor (6000.x)** with a valid local Unity entitlement. The manifest requests URP 17.0.4; Unity 6000.3.24f1 resolves the compatible bundled URP 17.3 packages, recorded in `Packages/packages-lock.json`.

From the repository root:

```bash
bash Tools/open-unity-mac.sh
```

The launcher discovers Unity 6 in Unity Hub's normal installation directory. It records the actual installed editor version in `ProjectSettings/ProjectVersion.txt` when that file does not exist. If Unity is installed elsewhere:

```bash
UNITY_EDITOR='/full/path/to/Unity.app/Contents/MacOS/Unity' bash Tools/open-unity-mac.sh
```

Let Unity resolve the packages and compile. The first-import editor script prepares the URP assets, retained shader material, and main scene. If preparation does not run, select **Cellblock → Prepare project**. Open `Assets/Cellblock/Scenes/Main.unity` and press **Play**.

Generated main-scene/render assets are recreated by the setup script and ignored by Git. The local editor has generated its package lockfile and project settings. **Cellblock → Open game and Play** prepares the project, opens the main scene, and starts Play mode. Save any edited scene first.

For a real editor compile and asset check on the Mac:

```bash
bash Tools/open-unity-mac.sh --batch-check
```

That command is an editor preparation check, not a gameplay or device test. The installed editor needs access to Unity's package registry and its package-download destinations. This project does not configure signing credentials or publish a build.

## First construction session

1. From Home, choose **Continue your holding**, or **New Holding** to choose jurisdiction, facility type and location. New facilities differ in terms, population, capital and editable layout. The first-use screen offers one clear **Begin your story** action.
2. Inspect the facility. Tap a wall or furnishing to expose its editing controls.
3. Hire a construction crew. Mobilization costs $500; each hired crew costs $800 per game day, including idle time. These are illustrative balancing values.
4. Open **Build → Prefabs**, choose a unit and Shell / Essential / Enhanced package, then tap the starter building's lower-right outer corner. That attachment is at world position `(2, -8)` and shares its existing boundary and doorway. Pinch/zoom closer for placement. Review the quote and approve.
5. Add another crew to compare parallel progress. Preparation, limited work fronts, prior projects, and handover prevent unlimited acceleration. Transport shares available seats, parks in a reserved marked bay, then disembarks. Builders survey, collect/carry supplies, and use task-specific tools at reachable work fronts. Approved construction is shown in ochre; its elements become operational at project handover.
6. After handover, inspect the wing's curved wall. Use **Bend**, **Move start**, or **Move end** to change the actual component. Doors, new bed/table installation and demolition use the quote/approval flow. Existing furnishings move and rotate immediately, with no crew or charge; invalid destinations leave the original in place.
7. Use **Build → Dining → Meal service** on an installed table to change the destination. Routes and service attendance affect verified meal-service earnings. **Company** shows cash, unpaid earnings, costs, and the cash ledger.
8. Use **Menu → Continue playing** to return to this session, or **Save Library** to select another holding. Autosave defaults to 30 seconds; **Settings → General** changes its interval. The header shows save status and offers Retry if saving fails. Approved jobs and unapproved complete plans persist; an unfinished first/second drawing tap is not a complete plan and is not saved.

Desktop: **left drag in Select / Inspect or while reviewing a plan** or **middle drag** to pan; **arrows / WASD** also pan. **Right drag / Alt + drag** or **Q / E** orbit. **R / F** tilt, Mac trackpad pinch and the wheel zoom toward the pointer, **Home** centers, **Space** pauses, and **Escape** discards an unapproved plan. Click inside Unity's Game view to give it keyboard focus.

Touch: tap to select/place, one-finger drag in Select / Inspect or while reviewing a plan to pan, and two fingers to pan/pinch/twist in any tool. **Camera** includes pan arrows, tilt, orbit, zoom, 3D / plan, and cutaway controls. **Guide** reopens the guide; **Settings → Controls** includes the camera reference and speed/easing options. See [the controls and first-improvement guide](docs/controls.md).

The game clock is player controlled. Closing, suspending, or unfocusing the game stops progression. Resume does not apply elapsed wall-clock time. Resumed saves start paused.

## Furnishings and people

**Build** groups furnishings into Housing, Hygiene, Dining, Recreation, Administration, Security, and Services. Twenty-four items have distinct 3D models, dimensions, access sides, and material prices. Choose an item, optionally rotate it 90°, place it, and review the construction quote. New items become operational at handover. Beds add capacity and dining tables support meal service; the other items currently provide furnishing and route geometry, without a complete hygiene or recreation service simulation.

**People** lists residents and staff. Select a person in the scene or roster to see a persistent name, temperament, traits, needs, gang/clique affiliation, relationships, and recent events. Residents use individual speeds, staggered decisions, and distinct service destinations. Nearby social contact can change trust/tension and create friendships, beefs, and nemeses. Fictional gangs and cliques affect those interactions.

Staff have integrity, workload pressure, favoritism, and misconduct risk. Incidents affect resident wellbeing and enter the history. A funded **Staff support** action reduces pressure/favoritism and improves integrity. New staff arrive by shared transport and walk to duty. Officers patrol and perform nearby welfare checks, caseworkers provide nearby consultations on shift, and technicians maintain operational equipment. Contraband intake, stashes, drone drops, searches and disturbance pressure are integrated. Full misconduct investigations and gang territorial control remain future work.

## Live operations and construction

**Control** contains lights, alarm, vehicle gate, lockdown, CCTV power, staff hiring, transport status, and shift reports. Raising an alarm closes the gate, starts lockdown, dispatches officers and holds construction; clearing it leaves gate/lockdown decisions with the player. A single selected camera renders a live feed; its coverage respects direction, walls, lighting, condition and network power. Place cameras through **Build → Security** or the Control shortcut.

**Build → Prefabs** offers six editable units and three fitout packages: Shell, Essential, and Enhanced. Walls and furnishings are quoted and built as one project. Foundations, framing, cladding and fitout appear during work; capacity and service geometry change only at handover. Builders carry supplies from their parked truck, use a saw bench, hammer, drill, and inspect; material trips consume game time. Seven marked staff/contractor bays are reserved per trip, with waiting hires sharing seats before departure. Staff shuttles return to their fleet after dropoff; crew pickups stay for supplies. Hiring is held when no bay or carpool seat is available.

### Security and building services

Select installed equipment to inspect condition, power, comfort, staff assignments and **Upgrade**. Improvements use the same crew/quote/handover process and retain the asset's identity. Build → Security adds exterior lighting, perimeter barriers, dog teams, towers and an armory. Build → Services adds room wiring, cooling and control terminals. Staff specializations retain each Officer's identity, shift and actual route. Power loss and maintenance affect availability; comfort changes morale and stress over game time. See [mechanics and boundaries](docs/security-and-services-status.md).

Improved/Enhanced towers now have directed night searchlights. Assign an on-shift lookout and supply power, then select **Sweep / Hold / Off** and **Aim −15° / +15°**. The rotating lamp, binocular watch and clipped ground pool show coverage. Walls block it; door openings admit it. A beam into occupied sleeping housing adds bounded stress, so aim matters. [Night-watch guide and code plan](docs/night-watch.md).

In a prefab quote, **click directly on any proposed or existing wall where you want a door**. The project quote updates; **Undo last door** or **Command/Ctrl+Z** restores the previous plan. Drag or use arrows while reviewing; pinch/scroll zooms. Existing straight and curved openings are retained. See [door controls](docs/direct-doors.md). Essential kits include wiring; Enhanced kits include cooling. For repeated furnishings, use **Approve & place another** and **Z** to rotate.

The native Mac magnify plugin is included for Intel and Apple silicon. Rebuild it with `bash Tools/build-mac-gestures.sh` only if its source changes, while holding the Unity runner lock.

Read [the Claude handoff](docs/claude-handoff.md) and [shared coordination](COORDINATION.md) before parallel work. Claude's legal profiles, court transport and interactive escape gameplay are integrated. Its next reserved slice is resident animation, daily life and stories; see `docs/claude-lives-handoff.md`. Codex owns connected doors, security equipment and building services; see `docs/security-and-services-status.md` and `docs/claude-coordination-update.md`. Claude has also received the new opening-mode/HUD direction.

## Staff field view and readable profiles

People now opens a compact Now / Life / Record / Items dossier with three numbered condition bars and details on demand. Residents and construction crews can be inspected and followed. **Staff only** have **Play as**: WASD/arrows or the touch MOVE pad walk, drag/right LOOK looks, **V** changes first/third person, **P** opens the work phone, **E** opens the duty brief, and **Escape / Management** returns. Phone Messages/Contacts/Tasks read the real role and operation; calls use the game clock. [Controls and boundaries](docs/character-experience.md). A [separate prisoner mode is planned](docs/prisoner-mode-plan.md).

## Population pressure and presentation

**Operations** (formerly Pressure) shows transfers, intake lanes, crowding, contraband, unrest, audits and upgrade programs. Choose a transfer policy, staff actual intake stations, improve beds/meal capacity and respond to disturbances. Contract pressure and cent-based costs persist in saves. **People → Ride along** follows a present resident or staff member with an optional body-camera inset; Escape returns to the build camera. This is a watch camera.

The presentation pass adds beveled furniture and installed hardware, fabric/tile/wood/concrete relief, distinct clothing, shared walking/running gaits and leg IK, complete everyday interactions, staged struggle/cuff/escort poses, detailed visitors/vehicles, pooled contact shadows and restrained alarm atmosphere. Shadow and reduced-motion settings apply to the new effects. See [presentation notes and limits](docs/presentation-polish.md).

## Verify the engine-independent core

With .NET 8 installed:

```bash
bash Tools/check-core.sh
```

Or set `CELLBLOCK_DOTNET` to the SDK's `dotnet` executable. The check project has no external test-package dependencies. It exercises geometry, routing around furnishings, editable prefab joins, crew progress, handover, resident relocation, service-dependent earnings, contract payment timing, accounting, numeric save validation, and state round trips.

If needed, `bash Tools/install-mac-core-sdk.sh` installs a SHA-512-verified official SDK into ignored `.local-tools/dotnet`. Then run `CELLBLOCK_DOTNET="$PWD/.local-tools/dotnet/dotnet" bash Tools/check-core.sh`.

An additional Roslyn syntax check can run without Unity:

```bash
dotnet run --project Tools/UnitySourceChecks/UnitySourceChecks.csproj --configuration Release -- "$PWD"
```

It checks C# syntax only. It does not typecheck Unity APIs, validate asset imports, or establish that the Unity project runs.

## Current boundaries

- The supplied starter building and prefabs have one floor. Multistory building and terrain editing remain future work. Staff first/third-person movement is implemented; separate prisoner play remains planned.
- Curves are editable quadratic Béziers. Rooms, generated floor surfaces, wall rendering, and navigation share sampled geometry. Navigation uses a lattice and clearance checks, so small passages can be rejected; it is not a complete production navigation mesh.
- Furniture has footprints and blocks routes. Bed replacements preserve assignments. Individual routines, basic needs, relationships, fictional affiliations, and staff conduct are implemented; the complete health, security, and rehabilitation simulation remains ahead. Placed perimeter barriers have collision, local sensors and bounded risk effects; the decorative site fence does not have a complete climbing/breach simulation.
- Crew travel, material carrying, tool actions and staged build visuals are implemented. Materials remain paid on approval; supplier inventories/deliveries, equipment scheduling, temporary facilities and full phased relocation remain future work. Existing geometry stays operational until atomic project handover; this abstracts real renovation stages.
- Materials/disposal are paid on approval and crew labor accrues over game time. Approved-job cancellation, refunds, financing, insolvency recovery, stock markets, and IPOs are not implemented. An overdraft holds construction while operating obligations continue.
- Construction quotes are estimates of work, not guaranteed calendar completion. All three opening offers use basic meal-service verification; specialist program/security obligations and distinct facility models still need implementation.
- The realistic asset library and production animation are not implemented. No supported minimum hardware, agent count, sustained frame rate, native player build, or App Store/Steam readiness is claimed.

See [the design brief](docs/reboot-vision.md) for the game direction and [implementation status](docs/implementation-status.md) for the current milestone and validation limits.
