# Upgrade roadmap

Everything in Cellblock Holdings that makes sense to upgrade, so later work can be planned against one list.

**Status key**

| Status | Meaning |
| --- | --- |
| **Live** | Purchasable now with simulated effects |
| **Live (Codex)** | Exists as a fixture upgrade level in Codex's security/services work |
| **Catalog** | Listed in `UpgradeRules.All` as Planned and shown in the HUD as coming later |
| **Planned** | Design intent only |

**Principles for every upgrade**

- It changes a simulated outcome the player can see in a report: an audit line, a ledger row, a count, a person's file. It is never a decorative bar.
- It has a one-time price and a daily running cost (staff time, maintenance, licences). Its level is saved, and loading never charges twice.
- It has a downside or a trade-off where one exists. Photocopied mail and video visits cost belonging; strip searches and shakedowns cost stress; more lanes cost staff.
- A tier the contract already includes (federal intake, state standard intake) is shown as included and costs nothing to run.
- A physical upgrade is a placeable fixture with an `UpgradeLevel` where possible (Codex's catalog). A policy or program upgrade lives in `Upgrades.cs`. When a program stands in for missing equipment, the roadmap names the fixture that should replace it.

## 1. Intake and transfers

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Intake program | Basic → Standard → Enhanced → Federal-standard | 4 → 7 → 8 → 9 staged steps: strip search, property, nurse screening, classification, mental health, gang intelligence, biometrics, orientation | **Live** (federal included, state Standard included) |
| Body scanner | 1 | +20% intake search detection | **Live** as a program → fixture `BodyScanner` requested |
| Intake lanes | Sally port counter + up to 3 desks within 12 m | Concurrent cases | **Live** (built with Desk fixtures) → `BookingDesk` fixture requested |
| Holding room | Painted bays → holding room → comfort (benches, water, phone) | Less waiting stress; fewer audit findings for long waits | Planned |
| Biometric records | Paper → digital records → biometrics everywhere | Fewer records discrepancies, faster identity steps, court paperwork | Planned (biometrics are inside Federal intake today) |
| Transport fleet | Contracted bus (8) → larger coach (16) → second coach → owned fleet with your drivers | Arrival batches, timing control, a transport cost line | **Catalog** |
| Video court | 1 courtroom → 2 | Remote hearings: fewer court-van runs, fewer court-return contraband chances | **Catalog** |
| Classification housing units | Minimum / medium / maximum units the classifier fills | Automatic security-level separation | **Catalog** |
| Transfer negotiation | Moratorium (2 days/week) → contract cap clause → preferred-facility status | Control over demand in exchange for money or terms | **Live** (moratorium); others Planned |

## 2. Housing and capacity

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Beds | Single bed → bunk bed (2) → triple bunk (3, crowding penalty) | Capacity per footprint | Planned (the Bed fixture is single today) |
| Floor mattress → emergency bunks | Mattress → cot → emergency bunk | Smaller stress and safety penalty for overflow | Planned |
| Cell type | Dormitory → shared cell → single cell → secure cell | Fight contact, privacy, escape and contraband hiding | Planned (prefab packages Shell/Essential/Enhanced exist — Codex) |
| Cell fronts and doors | Open doorway → barred door → solid door → remote-locked → interlocking sally port | Real access control. This is the prerequisite for prisoner control in the ride-along roadmap | Planned (Codex: doors are openings today) |
| Locks and keys | Keys → keycards → central control | Escape and lock-in speed; key impressions as contraband | Planned |
| Lockers / personal storage | Locker → secure locker | Fewer thefts and debts, cleaner shakedowns | Live (Codex) as a furnishing; effect Planned |
| Ceilings and roofs | Cutaway → ceiling panels → secure ceiling | Needed for first-person views; anti-escape | Planned |

## 3. Food, laundry, services

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Dining places | Tables (6 places) and chairs (1) in the dining room | Meal places × up to 4 shifts | **Live** |
| Kitchen | Serving counter → kitchen line → industrial kitchen | Meal quality, a faster serving rate, kitchen jobs. Hooch risk from produce | Planned |
| Meal delivery to housing | Trays to units | Meals during lockdown | Planned |
| Laundry | Contract laundry → on-site laundry | Hygiene falls 18% slower and fewer skin infections (contract); on-site adds laundry jobs and a contraband hiding place | **Live** (contract tier, see `docs/sanitation-notes.md`); on-site Planned (washer/dryer fixtures) |
| Commissary | 1 | −3 unrest, less tobacco demand, $1.50/resident/day | **Live** |
| Resident phones | 1 → video calls | −2 unrest, less pull for contraband phones, $1/resident/day | **Live** (tier 1) |

## 4. Health and safety

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Nurse post → clinic → infirmary → medical unit | Nurse on caseworker role → exam room → beds → doctor on call | Sick call capacity, intake medical screening, hospital transfers avoided | Nurse post Live (personnel); **Catalog** medical unit |
| Overdose response | Naloxone kits → medication-assisted treatment program | Reversal near staff → falling dependency over weeks | **Live** (kits); MAT Planned |
| Mental health | Screening at intake → counseling caseloads → mental health unit | Fewer crises, a watch list, lower stress | Screening and caseloads Live; unit Planned |
| Smoke detection → suppression | Detectors → sprinklers | Fires caught early → contained | **Live** (detectors); sprinklers Planned |
| Emergency power | Generator → battery backup | Lighting, CCTV and doors stay up in outages | Planned (Codex power grid exists) |
| Climate | Ventilation → powered cooling | Comfort and morale | **Live (Codex)** `ClimateUnit` levels |

## 5. Contraband defense

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Drone defense | RF detection → defeat system | Detect every drone; force them back | **Live** as a program → `DroneDetector` mast fixture requested |
| Yard netting | 1 | Yard drops caught; smugglers shift to windows | **Live** as a program → netting fixture requested |
| Window security | Standard glass → security screens → anti-drone mesh | Stops window drops | Planned |
| Perimeter barrier | Mesh → reinforced → sensing | Blocks 35/65/85% of throw-overs nearby; sensors report | **Live (Codex)** `PerimeterWire` levels, used by contraband |
| Search equipment | Handheld detectors → walk-through metal detectors → millimetre-wave scanners | +10% searches; strong for metal at pat searches | **Live** (2 tiers); scanners Planned |
| Managed-access cellular | 1 | Contraband phones stop working and are located | **Live** |
| K9 teams | Kennel → trained dog → narcotics-detection specialist | Scent detection near holders, vehicle search | **Live (Codex)** kennel and handler; contraband uses them |
| Vehicle inspection | Gate search bay → under-vehicle scanner | Delivery and contractor vehicle searches | **Live** (bay) |
| Mail screening | Photocopy → ion scanner → digital mail | 85% → 97% → 99%; belonging cost | **Live** (2 tiers) |
| Visit security | Contact → supervised → non-contact booths → video | Hand-offs 6% → 2.5% → 0.4% → 0; comfort falls | **Live** |
| Staff entry screening | Bag checks → full screening | Catches 45% / 80% of staff smuggling | **Live** |
| Informants | 1 → network | Tips about stashes and planned attacks; snitch risk | **Catalog** |
| Internal affairs | Investigator → unit | Investigates staff rings; stronger write-ups | **Catalog** |

## 6. Security and response

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| CCTV | Camera → powered HD → monitored → analytics (auto-flag hand-offs) | Coverage, detection, evidence | **Live (Codex)** cameras and upgrade levels; CCTV monitoring duty Live (personnel); analytics Planned |
| Control room | Control terminal → monitoring → central control | Dispatch speed, door control | **Live (Codex)** `ControlComputer` levels |
| Lighting | Solar → powered floodlights → searchlights | Night detection; sleep stress if aimed at housing | **Live (Codex)** |
| Guard towers | Tower → power, searchlight, comfort | Detection range | **Live (Codex)** |
| Armory | Storage → armed assignments | Armed deterrence | **Live (Codex)** |
| Disturbance control | Training ×1.5 → advanced ×2 | Officer force against disturbances | **Live** |
| Special response team | Contracted team → in-house team → regional team | +4 force after 25 minutes; in-house is faster | **Live** (contracted) |
| Less-lethal equipment | Shields → pepper spray → stun | Faster riot control, injury and audit trade-offs | Planned |
| Body cameras for staff | 1 | Evidence in write-ups, deterrence. Unlocks the ride-along body-cam as an in-world feature | Planned (the ride-along body cam is a viewer feature today) |
| Radios | Shared → personal → encrypted | Response speed and coordination | Planned |

## 7. Staff

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Recruitment | Listing → background checks → vetted pipeline | Applicant integrity | Listing and checks Live (personnel) |
| Staff academy | Orientation → academy → advanced certifications | Skill, integrity, promotion speed, armed/K9 qualification | **Catalog** |
| Pay and benefits | Base → competitive → union contract | Morale, pressure recovery, applicant quality, wage cost | Planned |
| Shift pattern | 12 h → 8 h with overlap | Rest recovery, cost, coverage at shift change | Planned |
| Staff wellness | Break room → counseling → support program | Pressure recovery | Partial (support payment Live) |
| Supervision | Sergeant → lieutenant → captain → warden | Integrity drift on shift, write-up review | **Live** (personnel ladders) |

## 8. Programs and rehabilitation

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Education | GED classes → vocational → college | Lower stress, good-time evidence, reentry outcomes | **Catalog** |
| Work programs | Kitchen and laundry jobs → industries → farm | Wages, revenue, belonging; tool contraband risk | **Catalog** |
| Recreation | Bench and TV → library → gym → yard equipment | Stress and belonging; activity variety (lives) | Furnishings Live (Codex); effects Planned (lives) |
| Chapel and counseling | Volunteer chaplain → chapel → peer programs | Belonging, lower grudges | Planned |
| Reentry | Reentry planning duty → reentry unit → partnerships | Good-time, outcomes after release | Reentry duty Live (personnel) |

## 9. Company and finance

| Upgrade | Tiers | Effect | Status |
| --- | --- | --- | --- |
| Credit | Overdraft → credit line → bonds | Cash-flow timing, interest | Planned |
| Insurance | Liability → riot and property | Smaller riot and damage costs, premiums | Planned |
| Headquarters | Office → regional managers → portfolio | Multiple sites (M5 franchise layer) | Planned |
| Public relations | Community board → media office | Fewer complaint audits, better contract renewal | Planned |
| Contract renewal | Standard → performance clause → preferred operator | Per-diem rate and demand cap tied to audit history | Planned |

## 10. Ride-along and control (see `docs/possession-plan.md`)

| Phase | Upgrade-like unlock | Status |
| --- | --- | --- |
| 1 | Ride-along follow camera and body-cam monitor on anyone | **Live** (viewer feature) |
| 2 | Officer control in third person (1× speed, same actions and limits as the AI) | Planned; needs core movement hooks |
| 3 | Officer actions: pat search, shakedown, escort, separate a fight, recover a drop | Planned; each ships with its system |
| 4 | First-person standing look-around | Planned; needs ceilings, door leaves, faces (started) and better textures |
| 5 | Security drill: control a resident to find blind spots (never releases anyone) | Planned; needs door and zone access rules |

## Sequencing recommendation

1. Fixtures for the program stand-ins: booking desk, body scanner, drone detector mast, yard netting, walk-through detector (Codex catalog and renderer). Then switch the programs to require them.
2. Real doors and cell fronts with access rules. This unlocks a secure intake, riot containment by locking areas, and prisoner control later.
3. Kitchen tiers and meal delivery during lockdown. Shifts make dining the next bottleneck after beds.
4. Bunk beds and emergency cots: the cheapest answer to overload, with a crowding cost.
5. Staff academy, pay and shift pattern: officer burnout is the long-run limiter.
6. Medical unit and medication-assisted treatment: dependency and welfare penalties.
7. Work and education programs, then the company and finance layers.
