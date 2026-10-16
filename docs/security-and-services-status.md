# Security, connected doors and services — implementation notes

Codex owns this slice. Read COORDINATION.md before edits. This document describes the current implementation; see the evidence section for what has actually been checked.

## Player flow

- Build a prefab. Shell is walls/openings; Essential includes an electrical panel; Enhanced includes improved wiring and AC, with a computer in the staff unit. The whole kit is one quoted construction project.
- In its quote, choose **Add connecting door**, then tap the existing shared wall. Both edges of the new opening must border the draft unit. Existing straight-wall doors are preserved by splitting the wall into editable pieces. A curved wall can gain its first opening; adding another door on that same curved piece currently asks for another wall segment.
- Build → **Security** contains CCTV, exterior lights, perimeter barriers, dog kennels, guard towers and an armory. **Services** contains electrical panels, ventilation/AC and control terminals. There are 23 fixture types across seven categories.
- Select installed equipment for its level, condition, power demand, local comfort and operational reason. **Upgrade** creates a normal construction quote. The old capability remains until the builders complete handover. The installed identity stays the same, including staff post assignments.
- Assign an Officer to a tower, a kennel as a K9 handler, or an upgraded armory for armed duty. These are specializations of the existing officer, not extra invisible employees. K9 and armed qualification each costs $750 once; assigned duty adds $80/$100 daily respectively. Arrival, shift, actual reachable movement and equipment availability remain necessary. Broken/offline/pending-renovation posts reject new assignments before charging.
- Control shows grid capacity/demand, electricity isolation, staffing/morale, dog count, staffed dispatch and perimeter reinforcement. Connected added equipment electricity is metered at 18 cents/kWh through the reconciled ledger. Existing contract operating costs already cover starter lighting.
- **Approve & place another** retains the item and rotation. **Z** rotates a new furnishing/prefab. Each placement still gets its own quote and revalidation. Quotes fold the inspector to leave the facility visible.

## Consequences

- Cameras use their installed level, wall LOS, direction, condition, network power and local night lighting. Basic battery camera: 15 m day / 8 m unlit night. Improved mains/IR: 20/14 m. Enhanced: 25/20 m. Selected live feed and coverage use the same per-camera range.
- Floodlights illuminate a local 7/11/14 m area. Powered equipment loses availability during grid isolation or overload, or when its room lacks wiring. New shells do not silently inherit full electrics from a neighboring room. Existing saves retain their starter room service through additive migration.
- Towers require an arrived on-shift officer at their access point plus five game minutes to climb. Lookouts show elevated in the platform. Coverage uses actual LOS and installed range. Upgrades show a searchlight, console and cooling; higher tier comfort affects the guard over time.
- A kennel creates one persistent dog after handover. An assigned handler walks normal routes; the dog follows or returns to rest. Detection needs a usable kennel, nearby on-shift handler and an alert, nearby dog. Fatigue forces a real rest period. Tier upgrades improve shelter/fatigue; the best shelter requires power.
- Reinforcement must be placed near the property edge. Collision and effective perimeter fraction follow real placed geometry; a single section cannot confer perfect site security. Enhanced barriers also have a powered local sensor.
- Mobile officers physically route toward detected escape attempts. Alarm increases response speed; a nearby staffed and powered dispatch console improves coordination. Only real officer proximity and clear sight can intercept. An ETA expiring cannot intercept someone. Lookouts detect from their post and are excluded from instant ground interception.
- Local ventilation, cooling and improved beds/benches/desks raise comfort. Staff morale moves gradually toward a comfort-dependent target, changing pressure. Resident housing comfort affects stress every thirty game minutes; nearby qualified armed deployment supplies local deterrence and adds a small distress tradeoff. These are fictional balancing mechanics, with no guarantee against escape.

## Persistence and code boundaries

Core/Security.cs owns equipment queries, specialization, dogs, utilities and comfort. Connections.cs owns full-draft door additions. FixtureCatalog/PrefabCatalog expose the equipment and kit configuration. Justice consumes shared detection/risk queries and still owns custody/legal clocks. Staff movement stays in Operations. New Lives code must preserve these staff assignments and justice priorities.

All charges use integer cents and explicit game time. Fractional utility cents persist. Upgrade work saves through the existing typed construction payload. Dogs, qualifications, posts, morale and grid settings persist. Pending assets are never included in operational security queries. A handover hold physically clears spare builders before enabling the final furniture footprint.

## Boundaries

The permanent decorative site fence is not a complete breach/climbing simulation. Escape attempts currently target the vehicle exit; placed reinforcement influences risk and collision, with local sensors/detection rather than a full fence-damage algorithm. Tower visibility uses the core's wall LOS at ground projection, so it does not look over opaque walls. Dog scent tracking, weapons combat, inventory-controlled tools, full electrical circuit routing, backup generators and supervised training sessions are not implemented. Qualifications are immediate paid actions. Visuals remain procedural. Hardware trackpad feel, native builds and phone performance require separate measured runs.

## Evidence

- Current core suite: 84 passed, 0 failed, including 72 prefab/package/rotation quotes, stable upgrade identity, save/resume, no early capability, wiring/isolation, local lighting, qualifications/fees, dogs/rest, staffing/climb, comfort, metering, perimeter placement, door preservation and exact two-crew handover clearance.
- Actual Unity 6000.3.24f1 security review passed all nine captures with no runtime errors. It covers a connected unit quote and build, actual tower arrival/climb, dog/patrol rendering, upgrade quote/handover, Unity JSON reload, powered night illumination, isolation, phone equipment controls and repeat-placement purchase persistence. Local images/report: `Builds/SecurityReview/2026-10-16/`. The small report and core transcript are retained in `docs/reviews/`.
