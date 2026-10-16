# Cellblock Holdings

A Unity reboot of a prison ownership and management simulation for iPhone, iPad, Mac, and potentially Windows/Steam. The player chooses a contract, takes over an editable starter facility, and connects building decisions to people, construction, and business results.

**Current milestone: construction foundation.** The C# simulation compiles and has automated executable checks. Unity rendering, editor setup, controls, native builds, and device performance have not been run in this cloud task. The presentation uses procedural prototype geometry and people; the [visual study](docs/concepts/visual-direction-study.png) is an art direction reference.

## Open on a Mac

For a local development session continuing this milestone, read [the Mac handoff](docs/local-mac-handoff.md).

Use an installed **Unity 6 editor (6000.x)** with a valid local Unity entitlement. The project uses URP 17.0.4, verified against Unity's official 6000.0 package source.

From the repository root:

```bash
bash Tools/open-unity-mac.sh
```

The launcher discovers Unity 6 in Unity Hub's normal installation directory. It records the actual installed editor version in `ProjectSettings/ProjectVersion.txt` when that file does not exist. If Unity is installed elsewhere:

```bash
UNITY_EDITOR='/full/path/to/Unity.app/Contents/MacOS/Unity' bash Tools/open-unity-mac.sh
```

Let Unity resolve the packages and compile. The first-import editor script prepares the URP assets, retained shader material, and main scene. If preparation does not run, select **Cellblock → Prepare project**. Open `Assets/Cellblock/Scenes/Main.unity` and press **Play**.

Generated scene/render assets are currently recreated by the setup script and ignored by Git. Unity remains responsible for generating its package lockfile and complete project settings; those should be reviewed and committed after the first actual editor validation. This cloud task does not fabricate editor-generated lockfiles or claim an installed Mac editor version.

For a real editor compile and asset check on the Mac:

```bash
bash Tools/open-unity-mac.sh --batch-check
```

That command is an editor preparation check, not a gameplay or device test. The installed editor needs access to Unity's package registry and its package-download destinations. This project does not configure signing credentials or publish a build.

## First construction session

1. Choose a contract. The initial offers differ in starting capital, acquisition cost, population, operating costs, payment timing, and a verified meal-service bonus. They currently share the starter-building kit.
2. Inspect the facility. Tap a wall or furnishing to expose its editing controls.
3. Hire a construction crew. Mobilization costs $500; each hired crew costs $800 per game day, including idle time. These are illustrative balancing values.
4. Select **Prefab wing** and tap the starter building's lower-right outer corner. That attachment is at world position `(2, -8)` and shares its existing boundary and doorway. Pinch/zoom closer for placement. Review the quote and approve.
5. Add another crew to compare parallel progress. Preparation, limited work fronts, prior projects, and handover prevent unlimited acceleration. Approved construction is shown in ochre; its elements become operational at project handover.
6. After handover, inspect the wing's curved wall. Use **Bend**, **Move start**, or **Move end** to change the actual component. Doors, bed/table installation, furnishing moves/rotation, and demolition use the same quote/approval flow.
7. Use **Meal service** on an installed table to change the destination. Routes and service attendance affect verified meal-service earnings. **Company** shows cash, unpaid earnings, costs, and the cash ledger.
8. Save and resume from the contract menu. Separate company saves remain selectable. Approved jobs and unapproved complete plans persist; an unfinished first/second drawing tap is not a complete plan and is not saved.

Touch: tap to select/place; two fingers pan and pinch. In Inspect mode, a one-finger drag pans. Desktop: click to select/place, wheel to zoom, right/middle drag to pan, Q/E to rotate, Home to recenter, Space to pause/resume, Escape to discard the current unapproved plan.

The game clock is player controlled. Closing, suspending, or unfocusing the game stops progression. Resume does not apply elapsed wall-clock time. Resumed saves start paused.

## Verify the engine-independent core

With .NET 8 installed:

```bash
bash Tools/check-core.sh
```

Or set `CELLBLOCK_DOTNET` to the SDK's `dotnet` executable. The check project has no external test-package dependencies. It exercises geometry, routing around furnishings, editable prefab joins, crew progress, handover, resident relocation, service-dependent earnings, contract payment timing, accounting, numeric save validation, and state round trips.

An additional Roslyn syntax check can run without Unity:

```bash
dotnet run --project Tools/UnitySourceChecks/UnitySourceChecks.csproj --configuration Release -- "$PWD"
```

It checks C# syntax only. It does not typecheck Unity APIs, validate asset imports, or establish that the Unity project runs.

## Current boundaries

- The supplied starter building and prefabs have one floor. Multistory building, terrain editing, and walk-through controls remain design decisions.
- Curves are editable quadratic Béziers. Rooms, generated floor surfaces, wall rendering, and navigation share sampled geometry. Navigation uses a lattice and clearance checks, so small passages can be rejected; it is not a complete production navigation mesh.
- Furniture has footprints and blocks routes. Bed replacements preserve assignments. This is a basic movement and meal-service model, not the planned complete prisoner, employee, health, security, or rehabilitation simulation. The perimeter and landscape are presentation, not a validated security system.
- Crews are currently a labor/work-front simulation. Worker travel, supplier deliveries, equipment, temporary facilities, and full phased relocation are future work. Existing geometry stays operational until atomic project handover; this abstracts real renovation stages.
- Materials/disposal are paid on approval and crew labor accrues over game time. Approved-job cancellation, refunds, financing, insolvency recovery, stock markets, and IPOs are not implemented. An overdraft holds construction while operating obligations continue.
- Construction quotes are estimates of work, not guaranteed calendar completion. All three opening offers use basic meal-service verification; specialist program/security obligations and distinct facility models still need implementation.
- The realistic asset library and production animation are not implemented. No supported minimum hardware, agent count, sustained frame rate, native player build, or App Store/Steam readiness is claimed.

See [the design brief](docs/reboot-vision.md) for the game direction and [implementation status](docs/implementation-status.md) for the current milestone and validation limits.
