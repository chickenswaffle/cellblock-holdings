# Cellblock Holdings

A Unity reboot of a prison ownership and management simulation for iPhone, iPad, Mac, and potentially Windows/Steam. The player chooses a contract, takes over an editable starter facility, and connects building decisions to people, construction, and business results.

**Current milestone: local Unity playable.** The project imports and runs in Unity 6000.3.24f1 on this Mac. The engine-independent core passes 46 executable checks. The presentation now includes detailed procedural architecture and people, a categorized furnishing catalog, individual resident/social dynamics, physical crew/staff arrivals, staged construction, prison controls, live CCTV, and packaged prefab units. Native builds and actual phone performance remain unverified; the [visual study](docs/concepts/visual-direction-study.png) remains an art direction reference.

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

1. Choose a contract. The initial offers differ in starting capital, acquisition cost, population, operating costs, payment timing, and a verified meal-service bonus. They currently share the starter-building kit.
2. Inspect the facility. Tap a wall or furnishing to expose its editing controls.
3. Hire a construction crew. Mobilization costs $500; each hired crew costs $800 per game day, including idle time. These are illustrative balancing values.
4. Open **Build → Prefabs**, choose a unit and Shell / Essential / Enhanced package, then tap the starter building's lower-right outer corner. That attachment is at world position `(2, -8)` and shares its existing boundary and doorway. Pinch/zoom closer for placement. Review the quote and approve.
5. Add another crew to compare parallel progress. Preparation, limited work fronts, prior projects, and handover prevent unlimited acceleration. Transport shares available seats, parks in a reserved marked bay, then disembarks. Builders survey, collect/carry supplies, and use task-specific tools at reachable work fronts. Approved construction is shown in ochre; its elements become operational at project handover.
6. After handover, inspect the wing's curved wall. Use **Bend**, **Move start**, or **Move end** to change the actual component. Doors, bed/table installation, furnishing moves/rotation, and demolition use the same quote/approval flow.
7. Use **Build → Dining → Meal service** on an installed table to change the destination. Routes and service attendance affect verified meal-service earnings. **Company** shows cash, unpaid earnings, costs, and the cash ledger.
8. Save and resume from the contract menu. Separate company saves remain selectable. Approved jobs and unapproved complete plans persist; an unfinished first/second drawing tap is not a complete plan and is not saved.

Desktop: **left drag in Select / Inspect** or **middle drag** to pan; **arrows / WASD** also pan. **Right drag / Alt + drag** or **Q / E** orbit. **R / F** tilt, Mac trackpad pinch and the wheel zoom toward the pointer, **Home** centers, **Space** pauses, and **Escape** discards an unapproved plan. Click inside Unity's Game view to give it keyboard focus.

Touch: tap to select/place, one-finger drag in Select / Inspect to pan, and two fingers to pan/pinch/twist in any tool. **Camera** includes pan arrows, tilt, orbit, zoom, 3D / plan, and cutaway controls. **Controls** reopens the guide. See [the controls and first-improvement guide](docs/controls.md).

The game clock is player controlled. Closing, suspending, or unfocusing the game stops progression. Resume does not apply elapsed wall-clock time. Resumed saves start paused.

## Furnishings and people

**Build** groups furnishings into Housing, Hygiene, Dining, Recreation, Administration, and Security. Fifteen items have distinct 3D models, dimensions, access sides, and material prices. Choose an item, optionally rotate it 90°, place it, and review the construction quote. New items become operational at handover. Beds add capacity and dining tables support meal service; the other items currently provide furnishing and route geometry, without a complete hygiene or recreation service simulation.

**People** lists residents and staff. Select a person in the scene or roster to see a persistent name, temperament, traits, needs, gang/clique affiliation, relationships, and recent events. Residents use individual speeds, staggered decisions, and distinct service destinations. Nearby social contact can change trust/tension and create friendships, beefs, and nemeses. Fictional gangs and cliques affect those interactions.

Staff have integrity, workload pressure, favoritism, and misconduct risk. Incidents affect resident wellbeing and enter the history. A funded **Staff support** action reduces pressure/favoritism and improves integrity. New staff arrive by shared transport and walk to duty. Officers patrol and perform nearby welfare checks, caseworkers provide nearby consultations on shift, and technicians maintain operational cameras. Investigations, contraband inventory, and a full gang/security simulation remain future work.

## Live operations and construction

**Control** contains lights, alarm, vehicle gate, lockdown, CCTV power, staff hiring, transport status, and shift reports. Raising an alarm closes the gate, starts lockdown, dispatches officers and holds construction; clearing it leaves gate/lockdown decisions with the player. A single selected camera renders a live feed; its coverage respects direction, walls, lighting, condition and network power. Place cameras through **Build → Security** or the Control shortcut.

**Build → Prefabs** offers six editable units and three fitout packages: Shell, Essential, and Enhanced. Walls and furnishings are quoted and built as one project. Foundations, framing, cladding and fitout appear during work; capacity and service geometry change only at handover. Builders carry supplies from their parked truck, use a saw bench, hammer, drill, and inspect; material trips consume game time. Seven marked staff/contractor bays are reserved per trip, with waiting hires sharing seats before departure. Staff shuttles return to their fleet after dropoff; crew pickups stay for supplies. Hiring is held when no bay or carpool seat is available.

The native Mac magnify plugin is included for Intel and Apple silicon. Rebuild it with `bash Tools/build-mac-gestures.sh` only if its source changes, while holding the Unity runner lock.

Read [the Claude handoff](docs/claude-handoff.md) and [shared coordination](COORDINATION.md) before parallel work. Legal profiles, court transport and escape gameplay are reserved for Claude's separate slice; they are not yet included in this operations milestone.

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

- The supplied starter building and prefabs have one floor. Multistory building, terrain editing, and walk-through controls remain design decisions.
- Curves are editable quadratic Béziers. Rooms, generated floor surfaces, wall rendering, and navigation share sampled geometry. Navigation uses a lattice and clearance checks, so small passages can be rejected; it is not a complete production navigation mesh.
- Furniture has footprints and blocks routes. Bed replacements preserve assignments. Individual routines, basic needs, relationships, fictional affiliations, and staff conduct are implemented; the complete health, security, and rehabilitation simulation remains ahead. The perimeter and landscape are presentation, not a validated security system.
- Crew travel, material carrying, tool actions and staged build visuals are implemented. Materials remain paid on approval; supplier inventories/deliveries, equipment scheduling, temporary facilities and full phased relocation remain future work. Existing geometry stays operational until atomic project handover; this abstracts real renovation stages.
- Materials/disposal are paid on approval and crew labor accrues over game time. Approved-job cancellation, refunds, financing, insolvency recovery, stock markets, and IPOs are not implemented. An overdraft holds construction while operating obligations continue.
- Construction quotes are estimates of work, not guaranteed calendar completion. All three opening offers use basic meal-service verification; specialist program/security obligations and distinct facility models still need implementation.
- The realistic asset library and production animation are not implemented. No supported minimum hardware, agent count, sustained frame rate, native player build, or App Store/Steam readiness is claimed.

See [the design brief](docs/reboot-vision.md) for the game direction and [implementation status](docs/implementation-status.md) for the current milestone and validation limits.
