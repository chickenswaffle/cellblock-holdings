# Local Unity playable: implementation and evidence

This records a working local development milestone, with the broader game still in progress.

## October 18 staff field view, compact profiles and combined motion

Implementation merge **`9d1aaef`** combines the staff checkpoint with Claude's published **f201d81** graphics/motion snapshot. **373 core checks pass, 0 failures.** The final unchanged-source Unity matrix passes **85 actual Game-view captures**, plus **24 animation sequences / 982 rendered frames across 6 disposable scenarios**. Every final report has an empty errors array. Earlier failed/cancelled review candidates are superseded, not added to these totals.

- **Readable People cards:** Now / Life / Record / Items; one purpose and three numbered bars first, with Why/next disclosed deliberately. The full file remains reachable and gets the phone's safe height. All six activity suggestions retain 44px targets.
- **Staff-only direct play:** real main-camera eye view and a wider shoulder view, WASD/arrows or MOVE/LOOK pads, useful work phone, and return to the previous management camera. Residents/crews remain inspectable and followable; a separate prisoner mode is planned.
- **Useful staff phone:** real messages, contacts, duty information, confirmation and game-clock call progress. Reload preserves remaining call time; no offline advance or instant-alarm shortcut.
- **Presentation:** stronger Home hierarchy/Continue, installed furniture hardware/detail, revised light/shadow bias, shared gait/leg IK, staged fight/escape choreography, pooled contact ellipses and restrained alarm grading. Shadow/reduced-motion preferences apply to the new effects.
- **Incident view on landscape phones:** Follow moves the response card beside a reserved world view. Both tracked actors, all three 44px actions and return to the management dock are checked. The review allows the real sprint/radio sequence to play out.
- **Integration:** restraints/recovery/escort reclaim movement; saved holds must match the actual attempt; actor/world removal immediately clears animation/shadow bindings. Generated shadow meshes/material and alarm volume profiles are disposed explicitly.

| Actual Unity review | Captures | Evidence |
| --- | --- | --- |
| Staff controls, compact dossiers, work phone, Home and lifecycle | 27 | [Character report](reviews/2026-10-18-staff-graphics-characters.json) |
| Complete daily interactions and compact/advanced phone flow | 17 | [Routine report](reviews/2026-10-18-staff-graphics-routines.json) |
| Player incident camera, radio, pursuit and physical outcomes | 16 | [Incident report](reviews/2026-10-18-staff-graphics-incidents.json) |
| Home, saves/recovery/autosave/settings and placement regression | 25 | [Frontend report](reviews/2026-10-18-staff-graphics-frontend.json) |
| Staged day/night animation plus unforced contact/cuffs/arrest/escort | 24 sequences | [Animation report](reviews/2026-10-18-staff-graphics-escapes.json) |

[Core transcript](reviews/2026-10-18-staff-graphics-core.txt) · [captured source fingerprint](reviews/2026-10-18-staff-graphics-source.json) · [editor cleanup/Play audit](reviews/2026-10-18-staff-graphics-editor-audit.json). Full local frames are in `Builds/{Character,Routine,Incident,Frontend,Escape}Review/2026-10-18/staff-graphics-final-27011080`. The staged animation camera can include foreground fence bars/reversed labels; these captures establish pose/phase checks, while the separate incident Game-view review checks the player camera. Fixed preview time excludes PNG encoding latency from its game/scene clock; ordinary gameplay retains its existing clock.

Independent critics reviewed source and representative actual frames. Native touch comfort/performance, native stripped builds and production character art remain unverified. This is a playable development milestone, not a finished game. [Controls](character-experience.md) · [planned prisoner mode](prisoner-mode-plan.md).

## October 17 physical incidents and visible guard response

Source **`8f38ce2`** adds [incident scenes](incident-scenes.md) to the Home/save/HUD milestone and Claude's published fixes through `1a9aa5b` (merge `9f55fd9`). **305 core checks pass, 0 failures.** The final focused Unity review passed **16 actual Game-view captures, errors empty**, including physically converging residents, distinct articulated scuffle poses, a real guard approach and separation, a visible 12-minute radio call before alarm/gate/lockdown, pursuit, physical interception, an undefended escape and saved mid-scene states. The incident card reports the current control flags after manual stand-down. Desktop and 900×420 phone-layout bounds passed. The independent critic reviewed source and actual corrected images; no blocking findings remain.

Evidence: [core transcript](reviews/2026-10-17-incidents-core.txt), full local incident frames `Builds/IncidentReview/2026-10-17/final/`. All four final Play reviews passed on unchanged source `8f38ce2`: **73 actual Unity captures in total, every report's errors array empty**.

| Review | Captures | Evidence |
| --- | --- | --- |
| Physical fights, radio, pursuit and outcomes | 16 | [Incident report](reviews/2026-10-17-incidents-unity.json) |
| Home, Continue, isolated real saves, settings, Claude integration and furniture placement | 25 | [Frontend report](reviews/2026-10-17-incidents-frontend-unity.json) |
| Numbered/signed meters and responsive People/Control navigation | 20 | [HUD report](reviews/2026-10-17-incidents-metrics-unity.json) |
| Direct doors, quote/undo/save, resident poses, ride-along, intake, events and materials | 12 | [Construction/presentation report](reviews/2026-10-17-incidents-polish-unity.json) |

Full regression frames are under `Builds/{FrontendReview,MetricsReview,PolishReview}/2026-10-17/incidents-final/`. Earlier attempts are superseded and are not added to the final totals. Unity returns to ordinary Play/Home with actual player holdings preserved. These are editor reviews; native device touch, stripped builds and sustained performance remain unmeasured.

## October 17 Home, safe Continue, settings and command HUD

Source milestone **`3fc7417`** integrates Claude's published economy/informants/kitchen/counsel/role snapshot `9c88d9e` (merge `243441f`). The combined snapshot through Claude `1a9aa5b` passed **294 checks / 0 failures** before the incident additions above; Codex also corrected fixture-interior clearance at construction handover. The new [Home and save flow](home-save-and-settings.md) has original imported night-campus artwork, a prominent Continue card, a separate sorted save library, visible configurable autosave, backup recovery and persistent failure/retry feedback. Global settings group save frequency, interface size, frame cap, shadows, reduced motion, camera controls and audio. The bottom icon dock, header actions and always-visible capacity/stress/risk bars make the main HUD visibly different.

The final frontend review passed **25 actual Unity Game-view captures, errors empty**, on the corrected source. It uses isolated real files in Temp and checks stale/missing/malformed pointers, newest ordering, correct selected-slot identity, pause on load, autosave/backup, damaged-primary archival without losing the good backup, failure/retry, preference persistence/application, first-use Home and actual charter acquisition producing one resumable slot. Claude's economy/intelligence/counsel/kitchen demos round-trip through Unity JSON; a real counter is quoted/built before the kitchen demo. Changing a rider releases the previous controlled role, preventing hidden old-person cards.

An independent critic reviewed the source and actual desktop/phone/narrow images. Corrections include explicit title texture metadata, full phone action visibility within the actual scroll viewport, debounced volume persistence, one-level Back, meaningful capacity colors including zero-bed custody, and vSync disabled so the selected desktop frame cap applies. Earlier artwork, fixture and clipping attempts are superseded. Final broader regression results are recorded in the incident matrix above. Actual phone touch/safe areas, native stripped builds, achieved frame rate and sustained performance remain unmeasured. The title artwork is an illustration, not gameplay rendering.

Full local combined frontend frames: `Builds/FrontendReview/2026-10-17/combined-final/`. The extra two captures exercise the actual pointer path for exterior-light and wall-fitted-bed quotes in a secure-cell start. Committed transcript/report: `docs/reviews/2026-10-17-frontend-core.txt` and `docs/reviews/2026-10-17-frontend-unity.json`. See COORDINATION.md for the exact existing-file ownership exception; Claude's charter/build/catalog mechanics and lives/personnel/economy remain reserved.

## Earlier October 17 numbered HUD and navigation

[Numbered meters](hud-metrics.md) now show resident needs/personality, staff condition, final escape risk and signed contributing factors, power, morale, perimeter, CCTV observations, equipment condition and construction progress. People has fixed Profile / Risk / Record / Roster shortcuts with a retained name; Control has Switches / CCTV / Team / Power / Reports. Highlights follow manual scrolling. Subject changes, reopening Control and resizing preserve a usable section start. Risk colors use the actual 35/55 thresholds; a displayed number cannot round across its classification boundary. Protective factors appear alongside danger factors, and all factors use a shared centered scale.

Implementation `2f54ce4` preserves Claude's reserved main HUD/build/theme and lives/personnel/pressure source. The independent critic reviewed the source and actual desktop/phone images. A phone resize/heading failure was fixed before the final passing run. The latest **215/215 core checks and 36 actual Unity captures passed, no runtime errors**: 20 focused meter/navigation frames and 16 existing general regression frames. The review asserts 44px targets, readable numbers, section jumps, stale-selection reset, resize handling, manual scroll highlights and world-input blocking at 1600, 900 and 640 widths. Full frames: `Builds/MetricsReview/2026-10-17/final/` and `Builds/PresentationReview/2026-10-17/metrics-regression/`; committed evidence: `docs/reviews/2026-10-17-metrics-*`. Actual device touch/performance remains unmeasured.

## October 17 night-watch continuation

The current core passes **215 checks / 0 failures**. Claude's published `650e3ce` and balance note `db6ad58` are integrated. They preserve nearest-free dining places, last-shift meal grace and trays, stranded-resident load repair, contract advances/net solvency and night-shift fixes. The critic found no confirmed regression in that published snapshot. Claude retains its active lives/personnel/pressure/HUD work; the economy design is preserved separately.

Codex added [directed night searchlights](night-watch.md): saved Sweep/Hold/Off and aim, shared wall/door-aware illumination/detection, staffed/powered/climbed operating gates, bounded sleeping-resident stress and exposure feedback. Towers have rotating detailed heads, articulated binocular watch and a corrected access-side ladder/clear deck stand. Cooler moonlight, softer shafts/pools and correctly switched lens emission improve night readability. Saved route endpoints are checked, and permanently removed resident/staff meshes and appearance bindings retire without discarding temporary court actors.

Final corrected source passed **50 actual Unity Game-view captures**, with no runtime errors: security/construction/night watch 17, door/polish/pressure 12, and stories/personnel/modes/menu 21. This includes Unity API/shader compilation, project validation, Unity JSON, narrow-wall shadow, light through a door, outage, phone button bounds and paused same-count roster replacement. Core transcript and reports are `docs/reviews/*night-watch*2026-10-17*`; full frames are in the `night-watch-final` / `night-watch-regression` folders under the October 17 Builds review directories. Native stripped players, actual device touch/performance and production character art remain unverified.

## Earlier October 17 checkpoint

At the earlier checkpoint the combined core passed **202 checks**. Claude overload snapshot `b470a10` is integrated: population transfers, coach/intake, crowding, contraband, disturbances, audits, upgrade programs, resident poses and watch-only ride-along. Codex reviewed it with an independent critic, preserved physical custody/mode/accounting integration, replaced the audit capsule with an articulated visitor and refined the coach and resident presentation.

Doors now use direct clicks on proposed or installed walls, live opening previews, retained earlier doors, curved splits, quote updates and undo. Drafts survive Unity JSON; loading/changing companies clears runtime history. [Door controls](direct-doors.md) and [presentation changes](presentation-polish.md) describe the behavior and limits.

A focused twelve-frame actual Unity Play review passed with no runtime errors, including shared/exterior door hit tests, exact undo and world isolation, phone quote, bed/bench poses, ride/body camera restore, transfer/intake saves, drone/disturbance and real audit visitor. The final phone-footer bounds and clothing-cache lifetime assertions passed. The existing story/menu (21 frames) and construction/security (9 frames) regressions also passed with no runtime errors: **42 actual Game-view captures** in the final matrix. Small reports and the core transcript are in `docs/reviews/2026-10-17-*`; full local frames are in `Builds/PolishReview/2026-10-17/final`, `Builds/StoriesReview/2026-10-17/final` and `Builds/SecurityReview/2026-10-17/final`.

## Implemented

- Unity project with separate engine-independent core, runtime presentation, and editor setup assemblies. The Mac launcher uses an installed editor; the current editor is **6000.3.24f1**.
- Three preserved legacy contract IDs plus a twelve-profile opening charter across City/County/State/Federal tiers and eligible selected/random locations. New holdings receive mode-specific terms, population, staffing and editable shared/dorm/secure/single-cell layouts. Existing saves preserve their terms and geometry.
- Straight, diagonal, and quadratic curved walls; arc-length door openings; connected editable prefab wings; room/floor topology and routing derived from the building data.
- Draft, approved construction, and operational geometry remain separate. Quotes, crews, wages, dependencies, handover, assignment-preserving bed replacements, and route checks connect building decisions to operation.
- Twenty-four furnishing definitions in Housing, Hygiene, Dining, Recreation, Administration, Security, and Services. Each has a model, footprint, access side, material price, placement rotation, and quote/handover flow. The separate Structure category contains walls, curves, doors, and wings.
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
- Integrated legal profiles (allegations/charges, sentences, court dates), court van/away visibility, release records, escape risk and warnings, close supervision, CCTV/guard/gate detection and interactive alarm controls. Mobile officers physically route to detected attempts; only actual proximity and wall sightlines can intercept.
- Integer-cent accounting and reconciled ledger. Company saves preserve identities, plans, and jobs, with atomic replacement/backup recovery and version-1 social-field hydration. New/resumed companies start paused; no closed-game catch-up.

## People, events and modes continuation

133 core checks pass in the latest integration. Twenty-one focused people/events/mode Play frames and nine final security/construction regression frames pass with no runtime errors. Claude's personnel and mode catalog through `029e5d9` is integrated with Codex's security, physical custody, modal input guards and new-world wiring. Personnel files, recruitment, warden/nurse posts, legal reviews, health and fights are available. The shared moving-target route fix makes watch officers advance instead of repeatedly consuming their current-position waypoint.

Facility goals, actual state/federal inspection visits, public demonstrations, rival contract pressure, named gang organizers/instigators, and fictional capital-case appeals/stays/escorted transport/outcome records persist on the explicit game clock. Characters have more detailed faces, clothing and equipment; public frontage has street furniture and planting. Close traffic yields or uses valid sidesteps. Supermax authorized movement requires actual nearby assigned mobile officers.

The broader resident-lives/programs and catalog dragging/HUD redesign are Claude's next reserved work. Known personnel balance/exploit issues are listed in the latest handoff. See [facility stories](facility-stories.md) and [mode starts](game-modes.md) for precise limits.

## Security/services continuation

Connected-door draft action; retained existing openings; eight new security/service items; installed-level models and upgrade quotes with stable IDs; power capacity, room wiring, utility metering, cooling, morale/stress; qualified officer tower/K9/armed assignments; visible dogs and tower lookouts; physical escape dispatch. Essential/Enhanced prefab packages include service installations. Quotes fold the inspector, repeated placement retains item/rotation, and Z rotates new placements. See [exact mechanics and limits](security-and-services-status.md).

The actual Unity security/services review passed nine Game-view captures with no runtime errors, including Unity JSON upgrade saves, night illumination, outage, phone controls and repeat-placement purchase persistence. Mode acquisition/layouts and the first resident-pose layer are integrated; additional authored lives and programs remain a Claude continuation.

## Validation on this Mac

| Check | Observed result | Scope |
| --- | --- | --- |
| GitHub main | `f4266ca` fetched; existing local work preserved | Continues the committed Unity reboot |
| Installed Unity | 6000.3.24f1, Intel macOS | Actual local installation, not a cloud editor claim |
| Editor import/API compilation | Successful | URP 17.3 bundled packages resolved by the installed editor; generated scene, retained material/shader, UI theme, and render assets |
| Project setup | `CELLBLOCK_PROJECT_VALIDATION_PASSED` | Actual editor validation; fixed the clean untitled startup-scene failure that previously aborted batch mode |
| .NET core checks | 305 passed, 0 failed | Geometry/construction/accounting, people/migration/conduct, arrivals/carpools, gate/work travel, carrying/resume, bay reservation, alarm, CCTV, reports, 72 prefab/package/rotation quotes and saved physical incident scenes |
| Security/services Play review | Passed; 17 final actual captures, no runtime errors | Connected building, physical staffing, directed searchlights, narrow walls/openings, Unity JSON, power loss, phone controls and actor lifetimes; `Builds/SecurityReview/2026-10-17/night-watch-final/` |
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

The social system is an initial simulation, with small fictional affiliations and bounded history. Gangs do not yet control territory or inventories. Misconduct includes contraband intake/stashes/searches, staff smuggling risk and disturbances; full investigations and bribery negotiation remain ahead. Needs and personality affect wellbeing/contact/routines, but the complete health, security, programs, admissions, and employee systems remain ahead.

Beds provide capacity and dining tables support meal service. Other catalog furnishings currently provide geometry and presentation; complete hygiene, recreation, administration-service and utility models remain ahead; camera maintenance is implemented. Starter buildings have one floor. The perimeter is visual, without validated security/escape rules.

Production human models/animation, a larger authored asset library, supplier inventories and delivery logistics, staged relocation, renovation refunds, financing, insolvency recovery, multiple holdings, investing, IPOs, and platform delivery remain future work. See the design brief and README for the broader direction.
