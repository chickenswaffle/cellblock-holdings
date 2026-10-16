# Connected units, security and building services

Owner: Codex. Status: implementation plan for the next bounded milestone; this file does not claim these features already work. Claude owns the separately handed-off resident life/story slice. Read COORDINATION.md first.

## Rules the code must enforce

1. One authoritative building model. Doors cut actual walls and routes; a connection is never a painted doorway. A prefab connection can be added to the quote before approval. Additional doors preserve existing openings (split a straight wall into editable pieces when needed). Curved openings keep arc-length semantics.
2. One security model. Equipment condition, actual placement, line of sight, working staff, power and upgrades feed the same queries used by the UI, detection and escape risk. Purchasing a disconnected item must not grant an invisible facility-wide bonus.
3. One construction path. Equipment installation and upgrades use Plan -> Quote -> approval -> physical crew work -> handover. Existing equipment keeps its old capability during the job. Revalidate before charging. Preserve fixture identity, assignments, saves and the ledger.
4. Explicit game time. Power charges, wear, training, comfort and morale advance on game minutes. Pausing/closing/loading never applies elapsed real time or repeats fees.
5. Explain tradeoffs. Better coverage can improve safety; intrusive lighting, constant restrictions and intimidating deployments can increase distress. Comfortable staff work more reliably. No upgrade means guaranteed escape prevention.

## Bounded first implementation

| Asset | Basic operation | Upgrades | Dependencies / actual effect |
| --- | --- | --- | --- |
| Exterior light | Small local pool of illumination | Powered floodlight; controlled efficient fixture | Actual night light coverage improves guard/camera visibility nearby; outages disable powered benefit |
| Outdoor CCTV | Existing directional camera and wall LOS | Mains/infrared; network recording/condition monitoring | Camera-specific range/availability, selected live feed and shared coverage query |
| Perimeter wire / barrier | Placeable collision footprint with marked access | Reinforced barrier; wire top and sensor | Must be near the property perimeter; must preserve gates/service routes; bounded deterrence proportional to effective coverage |
| Dog kennel / handler team | Kennel and a visibly present dog with an assigned officer | Training, shelter and equipment | Dog follows/rests by real routes; detection requires an arrived on-shift nearby handler and usable kennel; no invisible unstaffed dog bonus |
| Guard tower | Ladder/platform and an assigned lookout | Electricity/searchlight; communication/computer and cooling | Staffing, condition, reachable access and real sightlines gate coverage; show access/climb/on-duty phases |
| Armed assignment | Qualified officer keeps their persistent Officer role and identity | Training/equipment/communication | Real arrival/shift/pay and response target; bounded deterrence with resident stress tradeoff; no arbitrary duplicate officer counts |
| Electrical service | Starter service plus explicit unit connection | More capacity/backup/efficient distribution | Meter demand, capacity and availability; powered features lose benefit during outage/overload |
| Comfort service | Basic furnishings/ventilation | Fans; AC and better environmental controls | Local room comfort affects staff morale/pressure and resident stress over time; AC consumes power and needs maintenance |
| Computer / control equipment | Basic records and radio | Networked dispatch and monitoring | Requires power and operational equipment; improves information/dispatch where staffed, not instant all-seeing coverage |

All assets expose level, installed features, condition, power demand, prerequisites, construction cost/labor, recurring cost, current operational reason and next upgrade. Renderers show the actual installed level. Shell / Essential / Enhanced prefabs include the relevant service installations in the same project, with every cost visible in its quote.

## Core boundaries and intended API

Core/Security.cs owns persisted configuration, purchases/upgrade plans, local coverage, utility demand, condition, staff comfort and bounded morale effects. It must compile without Unity. Keep legal clocks, allegations and custody state machines in Justice.cs.

```csharp
// These names describe the intended contracts; implement and document the final signatures.
Plan ConnectDraftDoor(Plan draft, string wallId, Point opening);
Plan PlanSecurityUpgrade(string fixtureId, int nextLevel);
SecurityStatus SecurityStatusFor(Fixture asset); // one reason if unavailable
float CameraRangeFor(Fixture camera);
float StaffComfortAt(Point point);
float ResidentComfortAt(Point point);
float EffectiveSecurityAt(Point point); // actual staffing/coverage/condition
```

- Fixture configuration and level are plain serializable fields. Validate supported levels/features, finite values and references. Old saves retain existing capabilities through an additive migration.
- Keep upgrades linked to the same asset identity. Pending replacement must not cause camera selections, handlers or tower staffing to bind to a different object accidentally.
- `AdvanceSecurity` runs once per explicit game minute. Metering books through `Book`, with fractional cents reconciled. Queries never charge, mutate conditions or schedule work.
- Local power/comfort follows actual room geometry and explicit service installations. A new shell does not silently acquire full electrics, AC and computers.
- Justice consumes security queries for risk/detection/response. New life/story code may affect existing stress/safety/belonging; it must not implement a competing escape algorithm.
- Staff targets must visibly correlate with duties. An alerted officer routes toward the detected event; interception depends on reachable physical presence. An estimated response clock describes that movement rather than substituting for it.
- Vehicles use designated bays and road lanes, with separate pedestrian boarding points. The court van must share gate/traffic coordination rather than becoming a duplicate transport controller with conflicting geometry.

## Runtime / UI boundaries

- Add equipment models and installed-level variants in a dedicated FacilitySecurityView partial. Reuse materials/meshes and props; do not allocate geometry per animation frame.
- Power-aware lights and CCTV share the core status query. Management overlays stay out of CCTV feeds.
- Build -> Security and Services categorize items correctly. Inspector gives operational status, assign staff/handler, upgrade quote and dependencies. Control shows power/capacity, coverage gaps and current restrictions.
- Upgrades are reviewable quotes, with costs, work and ongoing expenses before approval. Keep touch controls stable and readable; avoid turning the menu into a sheet of engineering flags.
- Connections are a draft action: choose a real existing wall, preview the opening, re-quote the complete unit and connection together, preserve previous doors, and reject blocked/overlapping openings.

## Validation gates

Core checks: legacy migration; stable identities; denied/duplicate upgrades do not charge; inactive construction grants no benefit; power loss/condition/no staff disable effects; actual LOS/range; dog/handler/rest and tower staffing; local comfort/morale; bounded security vs distress tradeoffs; pause and mid-upgrade/transport saves; reconciled utility wages/costs; existing doors and routes survive an added connection.

Unity: real construction/upgrade handover, equipment-level visuals, operational lights/camera feeds, dog path and handler, tower access/climb, officer dispatch, explicit power loss, readable upgrade/connection quotes at desktop/phone layout, JsonUtility saves, and no runtime/import errors. Record actual evidence and remaining abstractions; device performance still needs actual devices.
