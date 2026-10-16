# Local Unity playable: implementation and evidence

This records a working local development milestone, with the broader game still in progress.

## Implemented

- Unity project with separate engine-independent core, runtime presentation, and editor setup assemblies. The Mac launcher uses an installed editor; the current editor is **6000.3.24f1**.
- Three contract starts with different reserves, populations, obligations, payment timing, and service bonuses; they share the editable starter building.
- Straight, diagonal, and quadratic curved walls; arc-length door openings; connected editable prefab wings; room/floor topology and routing derived from the building data.
- Draft, approved construction, and operational geometry remain separate. Quotes, crews, wages, dependencies, handover, assignment-preserving bed replacements, and route checks connect building decisions to operation.
- Fifteen furnishing definitions in Housing, Hygiene, Dining, Recreation, Administration, and Security. Each has a model, footprint, access side, material price, placement rotation, and quote/handover flow. The separate Structure category contains walls, curves, doors, and wings.
- More detailed procedural architecture, furniture, human proportions and clothing, parking, vehicles, perimeter, landscaping, and ground texture. Static meshes combine by material. Cutaway/full-height presentation, contact shading, and tone mapping improve spatial readability.
- Smooth perspective/plan camera, pointer-anchored wheel zoom, Select-mode drag pan, middle-button pan, arrows/WASD, orbit/tilt keys, and two-finger pan/pinch/twist code. Camera buttons and a dismissible controls guide provide alternatives.
- Reworked contract menu and Facility/Build/People/Company workspaces. Responsive panels, categorized cards, dimensions, construction quotes, accounts, and a compact utility dock.
- Persistent resident identities, names, temperament, patience, sociability, impulsivity, discipline, walk speed, routine offsets, basic needs, fictional gang/clique affiliations, and trust/tension relationships. Nearby contact changes relationships; friendships, beefs, and nemeses are named and recorded.
- Staggered movement decisions and individual service positions, with bounded core movement and smoothly turning, distance-driven procedural walking in Unity. Presentation route interpolation respects walls and furnishings.
- Staff integrity, pressure, favoritism, misconduct risk, resident wellbeing consequences, bounded event history, and a funded support action. New staff arrive by shared transport, park, disembark and walk to duties. Officers patrol/check welfare nearby, caseworkers consult nearby on-shift residents, and technicians maintain camera condition.
- Persistent shared transport, seven reserved marked bays, slow final parking, physically gated work, material collection/carrying, articulated elbow/hand tool poses and a saw bench. Construction visuals progress through foundations, framing, cladding and fitout; authoritative geometry changes only at handover.
- Six prefab units with Shell / Essential / Enhanced packages, all editable after construction.
- Control workspace for lights, alarm, gate, lockdown, camera power, hiring, transport and eight-hour shift reports. Placeable CCTV has condition, directional wall-aware coverage, lighting-dependent range and a selected live feed with full-height occlusion. Alarm coordinates restrictions/stand-down and has visual/audio cues.
- Universal native macOS AppKit magnify plugin; mobile pinch direction and ownership handling; queued Mac events drain through modals/focus changes.
- Integrated legal profiles (allegations/charges, sentences, court dates), court van/away visibility, release records, escape risk and warnings, close supervision, CCTV/guard/gate detection and interactive alarm controls. Original abstract response timer remains a boundary for the physical-security milestone.
- Integer-cent accounting and reconciled ledger. Company saves preserve identities, plans, and jobs, with atomic replacement/backup recovery and version-1 social-field hydration. New/resumed companies start paused; no closed-game catch-up.

## Validation on this Mac

| Check | Observed result | Scope |
| --- | --- | --- |
| GitHub main | `f4266ca` fetched; existing local work preserved | Continues the committed Unity reboot |
| Installed Unity | 6000.3.24f1, Intel macOS | Actual local installation, not a cloud editor claim |
| Editor import/API compilation | Successful | URP 17.3 bundled packages resolved by the installed editor; generated scene, retained material/shader, UI theme, and render assets |
| Project setup | `CELLBLOCK_PROJECT_VALIDATION_PASSED` | Actual editor validation; fixed the clean untitled startup-scene failure that previously aborted batch mode |
| .NET core checks | 69 passed, 0 failed | Geometry/construction/accounting, people/migration/conduct, arrivals/carpools, gate/work travel, carrying/resume, bay reservation, alarm, CCTV, reports, and 72 prefab/package/rotation quotes |
| Unity Play review | Passed; 16 actual Game-view captures; no runtime errors | Desktop/900×420 layout, contracts, catalog, profiles and named ties, live routines, connected wing quote, camera/full-height views, all furnishing models, and Unity JSON social-state round trip |
| Operations Play review | Passed; 12 actual captures; no runtime errors | Native magnify initialization/direction, shared arrival queues and marked-bay parking, physical supply carry, articulated saw/hammer/drill, staged housing and capacity handover, CCTV, night alarm/lights, reports, and phone Control |
| Combined justice Play review | Passed; eight captures; no runtime errors | Legal desktop/phone layout, court boarding/absence/return, mid-court Unity JSON reload, furnishing install/move/removal saves, detected escape, alarm interception and ledger. A subsequent deterministic capture-timing pass is running. |
| Independent critic | Focused implementation and UI findings corrected | Includes preservation of existing ties, wall-separated contact, station clearance, initial departure timing, compact quote framing, and relationship mix |

Operations evidence and the operations-check transcript are preserved in ignored `Builds/OperationsReview/2026-10-16/final/` and its parent folder. The final October 16 general review artifacts and core-check transcript are preserved locally in ignored `Builds/PresentationReview/2026-10-16/`. The report lists the actual captures. Unity emits UI Toolkit deprecation warnings for the existing default-event suppression API; compilation succeeds and the Play review recorded no runtime errors.

The Mac is an Intel machine with an AMD Radeon Pro 580X. This establishes local editor execution, not minimum hardware or a supported population/frame-rate promise. The official .NET 8 SDK is stored in ignored `.local-tools`; its archive was verified against Microsoft's SHA-512 release metadata.

**Cellblock → Capture presentation review** runs a disposable Play-mode preview and writes actual Game-view captures plus a JSON report to ignored `Temp/CellblockPresentation/`. It preserves an active company before changing Play state, refuses to replace dirty editor scenes, and never saves preview companies. **Cellblock → Open game and Play** leaves the normal game ready to use.

## Validation still ahead

Manual pointer/trackpad feel and real finger gestures; device safe areas and lifecycle; native macOS/iOS/Windows player builds; shader retention in a stripped player; sustained frame pacing, navigation stalls, thermals, memory, battery, and capacity on reference hardware. Resizing the Editor Game view is a layout check, not an iPhone performance test.

## Current gameplay boundaries

The social system is an initial simulation, with small fictional affiliations and bounded history. Gangs do not yet control territory or inventories. Misconduct is an event/risk model, with no full investigation, bribery negotiation or contraband economy. Needs and personality affect wellbeing/contact/routines, but the complete health, security, programs, admissions, and employee systems remain ahead.

Beds provide capacity and dining tables support meal service. Other catalog furnishings currently provide geometry and presentation; complete hygiene, recreation, administration-service and utility models remain ahead; camera maintenance is implemented. Starter buildings have one floor. The perimeter is visual, without validated security/escape rules.

Production human models/animation, a larger authored asset library, varied starter models, supplier inventories and delivery logistics, staged relocation, renovation refunds, financing, insolvency recovery, multiple holdings, investing, IPOs, and platform delivery remain future work. See the design brief and README for the broader direction.
