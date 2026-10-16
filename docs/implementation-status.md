# Unity construction milestone: implementation and evidence

This records the first code milestone, not completion of the full game or the device acceptance criteria in the design brief.

## Implemented source

- Root Unity project layout with three assemblies: engine-independent core, runtime presentation, and editor setup. URP is pinned to 17.0.4; the Mac launcher records the installed Unity 6 editor version instead of inventing one.
- Three opening contract offers with different acquisition/reserve budgets, populations, daily costs, payment calendars, and meal-service bonuses. They currently share a starter-building kit.
- Editable straight and quadratic-curve walls, door positions measured by arc length, partial/shared-wall subtraction, connected prefab placement, room faces, and floors that subtract nested boundaries.
- Planned and operational geometry separated until project handover. Wall and furnishing alterations quote costs before approval. Furnishing footprints block routes, occupied-bed replacements retain resident assignments, and meal-service table relocation moves the service point.
- Hired crews with mobilization and ongoing wages, limited work fronts, preparation, prior-project dependencies, and handover. Extra crews accelerate parallel work. Physical worker travel and materials logistics remain future work.
- Integer-cent cash accounting, receivables, contract payments, operating/crew expenses, and service-based earnings. Basic prisoner routines provide a measurable connection between circulation and delivered meals.
- Unity source for procedural 3D presentation, cutaway-height controls, touch/mouse construction and camera input, contract selection, stable management controls, quote review, project status, and company accounts.
- Separate selectable company saves, atomic file replacement with backup recovery, saved unapproved complete plans, and explicit pause/speed controls. Pause and focus use separate lifecycle flags. No elapsed real-time catch-up runs on load.
- Editor preparation for URP, a retained Resources material/shader, and the main scene. A Mac launcher selects installed Unity 6. Generated editor assets and Unity's eventual lockfile still require a real editor run.

## Verification performed in the cloud

| Check | Observed result | Scope |
| --- | --- | --- |
| .NET 8.0.426 compilation and executable core checks | 27 passed, 0 failed | Geometry, routes, furnishing clearance, prefab/door joins, crew progress, payments, earnings, ledger, relocation, numeric/pending-work validation, drafts, and state round trips |
| Roslyn C# 9 syntax parsing | 7 Unity/core/editor source files, 0 syntax errors | Syntax only; no Unity API/type checking |
| Shell parsing | Mac launcher and core-check launcher parse successfully | Not Mac execution |
| Package and assembly JSON | Valid JSON | Does not resolve packages |
| URP API/package reference | Official Unity Graphics `6000.0/staging` source confirms version 17.0.4 and the pipeline creation/properties used | Static source reference, not an editor compilation |
| Independent critic | Core and runtime findings corrected; final narrow review found no new blockers | Source inspection only |

The SDK download was checked against the SHA-512 in Microsoft's official release metadata with TLS verification intact. Core tests were rerun after corrections, including the furnished starter layout and critic findings. The SDK is local cloud tooling and is not included in the source archive.

The saved cloud configuration draft now contains a replacement `install_script` for the verified .NET/core workflow and a replacement `start_skill` describing the Unity project, local Mac startup, and outstanding editor/device validation. The exact saved install sequence was executed successfully. The draft save is confirmed; it neither publishes the environment nor opens Unity on the Mac. Review and save the draft in environment settings, then publish the environment when applying that cloud configuration.

## Not yet validated

Unity imports and API compilation; generated scene/material/render-pipeline setup; Play mode; actual 3D output and UI; finger gestures and safe areas; Unity JsonUtility/file-replacement behavior on devices; native Mac/iOS/Windows builds; shader variants in players; performance, thermals, memory, battery, and suspend/resume on hardware.

The current shell is Linux. Codex project metadata identifies an existing local project at `/Users/trading/Documents/ChatGPT/Cellblock Holdings`, but the available tools cannot execute shell/editor commands on that Mac or synchronize this cloud checkout into it automatically. The user reports Unity installed on the current Mac, described as a Mac Pro. The exact installed version and Xcode availability are unverified.

To close that gap, use a local execution session with the prepared source, run `bash Tools/open-unity-mac.sh --batch-check`, then inspect Play mode. Deploy the same milestone to the iPhone 17 Pro and identified iPad Pro through the user's local Apple/Xcode setup. The M4 Air remains an earlier named reference device; do not assume it is the current Mac Pro.

## Remaining game scope

The complete asset library, production character animation, specialist prisoner/staff systems, legal/security simulation, training, supplier logistics, staged service relocation, full renovation cancellation/refunds, varied starter models, financing, multiple facilities, investing, IPOs, platform storefront integration, and final narrative tone remain ahead. See the design brief and README for the intended direction and the current prototype's boundaries.
