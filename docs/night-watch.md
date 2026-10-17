# Night watch: construction that changes security

## Play

Build a Guard tower in **Build → Security**, then use its **Upgrade** action. The Improved tier adds a searchlight; Enhanced adds a wider, longer beam plus the existing console/cooling. Finish construction, supply electricity and assign an officer. The lookout must arrive, climb and be on shift. Turn facility lights on in **Control**.

Select the tower and scroll to **Searchlight / Night watch**:

- **Sweep** moves through a 110° arc around your chosen bearing.
- **Hold** stops at the current beam direction.
- **Off** switches off the beam while preserving the ordinary lookout's sight.
- **Aim −15° / +15°** changes the centre bearing. 0° is north, 90° east. It does not rotate the tower, ladder or route access.

The light operates from 19:00 to 06:00. Improved reaches 18 m with a 36° cone; Enhanced reaches 24 m with a 44° cone. Opaque walls and the site boundary stop coverage. Actual door openings admit it. Tower vision uses the existing conservative ground-projected wall model; it cannot see over opaque walls.

The visible ground pool shows the beam footprint. Detection and camera illumination query the same cone, bearing and sightlines. A low-cost local Spot Light adds surface highlights; it is conservatively shortened by the nearest wall and does not cast additional dynamic shadows. The pool remains the coverage indicator when that local highlight is unavailable. Production volumetric scattering and phone rendering performance are not established by this implementation.

## A real tradeoff

Light can detect a resident outside the ordinary lookout range, while illuminating useful camera positions. A beam pointed through an open doorway into occupied housing can disturb sleep. Between 22:00 and 06:00, only present residents actually at home, outside intake and in ordinary facility custody receive the effect. Overlapping towers do not stack it. The maximum added stress is 2.4 over a whole eight-hour night; this is a fictional balancing value. Control reports current exposed sleepers; each tower records resident-minutes of exposure. Aim away, switch to Hold, or turn that beam off.

## Code plan and ownership

1. `Core/Searchlights.cs` owns saved operating controls, phase, coverage, ray reach and exposure. Security schema 2 adds `SecurityState.TowerLights`; v1 saves hydrate to Sweep using each tower's rotation as its initial bearing. `World.Start` must finish installing/wiring its starter rooms before the version is advanced. Queries never create controls or spend money.
2. Controls follow the installed fixture's stable ID. Upgrades preserve that ID and its aim/mode. Drafts and incomplete towers do not grant capability. Reconciliation removes demolished posts. Existing lookout detection remains available when the lamp is off.
3. `Security.cs` routes illumination/detection through the shared query and advances the bounded exposure effect on explicit game minutes.
4. `FacilitySearchlightView.cs` owns the separate rotating head, yoke, lens, hinges, fins, binocular watch pose and reusable coverage mesh. Uniform rays plus wall/opening endpoints and clearance tangents prevent broad sampling from missing narrow barriers. Ray endpoints use the core's wall clearance. The Resources shader retains the transparent beam for player builds.
5. `GameSecurityHud.cs` owns selected-tower operating controls and Control feedback. Claude may reorganize the broader HUD; preserve these Build/Refresh hooks and the named controls.

The clock determines sweep phase. Live lamp motion interpolates briefly between simulation headings; paused views snap exactly to the authoritative direction. Pausing freezes the phase; reloading preserves it. No real-world elapsed time is applied. Player builds and device performance remain separate acceptance work.

## Follow-on design priorities

These are proposals, not implemented features or a duplicate assignment to Claude:

1. **Visible economy loop** — Claude's jobs, kitchen and commissary design should connect a specific room, staff, shift and participant to materials, service quality and reconciled earnings. Show people doing the work before adding more financial panels.
2. **Security commissioning** — physical scanners, booking stations and drone-defense equipment should replace abstract upgrade benefits with built, powered, staffed coverage. Coordinate exact intake/contraband hooks with Claude before editing those active files.
3. **Maintenance choices** — repair visits and power problems should identify the affected asset, a visible symptom and a player action. Avoid anonymous facility-wide penalties.
4. **Character readability** — purpose-specific animation, carried props, individual appearance and useful selected-person stories should precede crowd-size promises. Replace procedural character art with production models/rigs as a distinct asset milestone.
5. **Phone acceptance** — measure construction gestures, small-screen panel flow, thermal cost and frame time on a connected device. Editor captures establish layout and behavior only.

## Validation

Core checks cover gates, cone/direction, ordinary lookout independence, deterministic reload/pause, bearing vs geometry, walls/openings, sleep eligibility/overlap, migration/malformed controls and actual upgrade handover. The security Play review additionally checks rendered bearing, beam lifecycle, Unity JSON, outage, phone bounds and temporary/permanent resident actor lifetimes. Current pass results are recorded in `COORDINATION.md` and `docs/reviews/`; a plan or compilation alone is not a passing Play result.
