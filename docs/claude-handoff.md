# Claude parallel handoff — legal profiles, court transport, custody

## Start here

Repository: `https://github.com/chickenswaffle/cellblock-holdings`

Shared base branch: `codex/live-operations-handoff`. This branch contains Codex's ongoing operations milestone. Read `AGENTS.md`, `CLAUDE.md`, and `COORDINATION.md`, then inspect status and recent commits. Preserve the user's saves and every existing dirty file. Never reset this checkout.

Local checkout, if available: `/Users/trading/Documents/ChatGPT/Cellblock Holdings`. On this Mac, claim `justice` with `bash Tools/agent-coordination.sh claim claude justice`. In a remote clone, create `codex/claude-justice-custody` from the shared base; scope ownership still applies even though local lock directories are not shared between machines.

**Codex continues crews, arrivals, construction, staff, controls, CCTV, prefabs and pinch input. Do not rebuild or edit those systems. Codex is the only Unity runner. Do not launch another editor or batch/player build.**

Add an acknowledgement and your plan to your Claude section in `COORDINATION.md`. Update that section at milestones and before commits; read Codex's log at the same points. Commit only your scoped work. In a remote clone, push your justice branch and report its SHA; do not push to main or the shared Codex branch. Locally, coordinate commits on the current shared branch without switching it underneath Codex.

## Your requested feature slice

1. Persist fictional legal/custody profiles: charge/reason for detention, pretrial versus sentenced status, sentence/release timing, next court appointment, and a readable event history. Label charges as allegations where appropriate. Use explicit game time, never wall-clock catch-up. Do not imply these fictional schedules model real legal procedure.
2. Show the information in the People profile, with an expandable compact legal card so needs/traits remain usable on phones. Include actionable court status and custody/escape risk.
3. Add a court van with a persisted schedule and a visible boarding, road-travel, court, and return cycle. Keep off-site coordinates in a separate court-trip model; do not move `Resident.Position` outside the site's validated bounds. Hide occupants while aboard/away and prevent normal meal/yard movement/social contact from pulling them back into the facility.
4. Add a bounded, interactive escape-attempt state machine for high-risk residents. Risk should reflect individual traits and conditions. Guards, closed gates, alarms/lockdown, and **actual** operational CCTV sightlines must affect detection/interception. Give the player readable warnings/events and controls to influence outcomes. Keep this fictional game behavior abstract; do not add real escape instructions.
5. Preserve existing names, IDs, homes, finances, and all saved construction. Hydrate old saves without silently changing existing state or replaying transport/fees. Add focused executable core checks and write exact evidence/limits.

## Exclusive files you may create

- `Assets/Cellblock/Core/Justice.cs`
- `Assets/Cellblock/Runtime/GameJusticeHud.cs`
- `Assets/Cellblock/Runtime/FacilityJusticeView.cs`
- `Assets/Cellblock/Resources/Justice.uss`
- Matching Unity `.meta` files
- `Tools/CoreChecks/JusticeChecks.cs`
- `docs/justice-notes.md`
- Your own Claude log section in `COORDINATION.md`

Do not edit existing core/runtime/USS/editor/test-driver files. Ask for an interface change in the shared log if needed. Do not introduce duplicate movement, transport, lighting, or CCTV systems for existing crew/staff.

## Integration hooks already provided by Codex

`Resident`, `State`, and `World` are partial classes in namespace `Cellblock.Core`. Put new serialized fields in your new partial declarations. `State.Version` remains 1; use your own additive justice-version marker and migration.

Optional World partial methods (implement the matching signatures in your file):

```csharp
static partial void HydrateJustice(State state); // called during construction, before validation
partial void AdvanceJustice(); // once per explicit game minute, after operations
partial void UpdateResidentCustody(Resident resident, ref bool skip); // set skip=true to hold normal routines
partial void ResidentPresence(Resident resident, ref bool present); // false for aboard/away/released/escaped
```

`World.IsPresent(resident)` is consumed by social contact, casework/patrol targets, meal verification, and CCTV coverage. World partial code can reuse private `Walk(key, ref position, target, speed)`, `Routes`, `SightLines`, `Book`, and existing fields. Persist any legal route/phase needed for resume; cached paths are reconstructed.

`FacilityView` is partial in namespace `Cellblock.Unity`:

```csharp
partial void ResidentVisibility(World world, Resident resident, ref bool visible);
partial void RefreshJustice(World world, bool force);
```

Your partial renderer can reuse private `Root`, `Truck(parent, kind)`, `TruckView`, geometry helpers, `V`, and actor facilities. Keep justice vehicle dictionaries separate from crew/staff trucks. Optional court props must not enter authoritative routing unless modeled in core. Normal render cleanup owns generated meshes; explicitly clean any extra owned textures/materials/audio.

`GameHud` is partial with optional `BuildJustice()` and `RefreshJustice(World world, Resident resident)` hooks. You can access the existing private `personCard`, `root`, `game`, `Text`, `Action`, and UI helpers. Load your own `Resources/Justice.uss`; do not edit `Cellblock.uss`. The selected resident is null for staff profiles. `GameController.FocusPoint(Point)` is available.

Core checks use `static partial class Program`. Implement `static partial void RunJusticeChecks()` in `JusticeChecks.cs`; it is called by the existing driver. You may use its `Test`, `Check`, `County`, and `RoundTrip` helpers. The project automatically includes `*Checks.cs`. Run:

```bash
CELLBLOCK_DOTNET="$PWD/.local-tools/dotnet/dotnet" DOTNET_CLI_HOME="$PWD/.local-tools/dotnet-home" bash Tools/check-core.sh
```

If the SDK is absent in your environment, use an installed .NET 8 SDK or the appropriate existing install script. Request Unity checks from Codex in the shared log. Do not claim actual Play/device results from a syntax/core check.

## Acceptance / report back

Check old-save hydration, stable legal identities/timelines, court departure/return and mid-trip save/load, no routine/social activity while absent, pause/no catch-up, gates/guard/LOS-dependent interception, bounded event history, and UI responsiveness. Include a deterministic way for the Unity runner to demonstrate your slice.

Report files, commit SHA/branch, exact checks/results, unresolved issues, and any interface request in your Claude log. Release the justice lock when finished. Codex will integrate and run the combined Unity review.
