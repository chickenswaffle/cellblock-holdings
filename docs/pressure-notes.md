# Population pressure: transfers, intake, contraband, unrest and audits

Owner: Claude, scope `population-pressure`, branch `codex/claude-overload`. All rules are fictional game mechanics, not a model of a real correctional or justice system.

## The loop

An overloaded justice system keeps sending people. Beds, meal places, intake lanes, officers and nurses have to keep up. When they do not, crowding, hunger and contraband raise stress and unrest. Unrest becomes disturbances and riots, and audits fail. Three failed audits within 30 days end the contract, and so does deep debt. Building, hiring, intake programs and upgrades are the levers.

```
transfer demand ─▶ bus ─▶ intake (staged) ─▶ bed or floor mattress ─▶ daily life
      ▲                       │ misses: contraband, conditions, enemies        │
      │                       ▼                                                ▼
 per-diem pays          contraband inside ─▶ dependency, weapons, debts ─▶ fights, unrest
      │                                                                        │
      └──────────── audits (crowding, staffing, intake, violence, welfare) ◀───┘
```

## Transfers (Core/Population.cs)

- **Demand.** Transfers per day = base residents × tier rate × facility factor × growth multiplier. Tier rates per base resident per day: city 0.22, county 0.12, state 0.08, federal 0.06. Growth adds 4–5% a day, capped at ×3. A supermax receives half, a reentry campus 0.8, holding centers and camps 1.1. Demand starts on the first evening.
- **Surges.** Every 6–10 days, 3–6 extra people arrive with a reason that fits the tier (e.g. "The county next door is over capacity").
- **The bus.** A secure transfer coach on road coordinates carries up to 8 and is dispatched 06:00–18:00. It arrives after 20 minutes plus the location's court drive and unloads one person every 2 minutes into intake holding.
- **The gate.** The bus waits at a closed vehicle gate. An officer on the Gate post duty admits it without opening the gate to everything else. After 2 hours waiting it turns back, and every seat counts as a fined refusal, so keeping the gate shut is not a free way to stop transfers.
- **Policy.** *Accept all* (default) houses everyone, using floor mattresses when beds run out. *Only free beds* refuses the rest at $300 each (× penalty scale), and each refusal counts against the contract at audit.
- **Moratorium.** A 2-day pause costs $2,500/day (scaled), once every 7 days. It also clears two days of backlog.
- **Ceiling.** Committed population stops at 96 (performance guard); extra demand is diverted, without penalty.
- **Money.** Each resident above the contract's base earns 85% of the base per-resident rate as per-diem. It is paid with the contract, into receivables. Each also costs $18/day × the location's wage index in food and supplies.
- **Advance on revenue.** Draw half of the earned, unpaid receivable now, less a 3% fee. This is bridge financing for monthly-paid state and federal contracts; the rest arrives on payment day.
- **Contract terms** follow the chartered facility type and location; the three original offers map to identical terms. New companies start from `World.StartCharter`, with the type's own capital and land price.

## Housing and crowding

- Beds are assigned on housing. Classified arrivals avoid rooms with keep-separate enemies (+100 penalty), rival gangs (+12) and other security levels (+3).
- With no free bed, a clear floor spot in a housing room is chosen: at least 0.85 m from every home and furnishing access point. If none exists, the person sleeps in intake holding.
- Floor sleepers get +0.6 stress and −0.25 safety every 30 minutes. They move to a bed as soon as one frees up. A mattress never blocks construction (CheckRoutes exempts it); geometry changes re-house the sleeper.
- Residents who are fed, safe and in a bed lose 0.35 stress per 30 minutes. Grudges between people more than 6 m apart cool by 0.04 per 30 minutes.

## Meals (Simulation.cs, handed-off file)

Meal places are 6 around each table and one per dining chair, in the designated dining room and in every room with a serving counter. A dining unit near new housing therefore adds capacity and shortens walks.

- **Shifts.** When the roster exceeds the places, meals run in up to four shifts. Each person takes the nearest place that still has a free shift (earliest shift first), keeps it until the building or roster changes, and sets off 45 minutes before their block.
- **Grace.** Someone still hungry keeps the place for an hour past the block.
- **Trays.** After each meal (10:05, 15:05, 21:05), anyone on site still under 55 food gets a tray in housing: +25 food, +2 stress, $4 × wage index, and a little officer time. Trays also feed people in lockdown and intake.
- **Pressure from trays.** Nobody starves for lack of a seat, but trays are a cost, an unrest factor (up to +10) and an audit finding (per resident-day, up to −8). Malnutrition still has its own welfare message.

The service bonus measures people at their seat, plus those already full, against those due now plus those with no seat. With one shift, behavior matches the original. Quotes validate the meal places, and the yard places beyond the first twelve, that the plan would leave. Extra yard places are clear outdoor cells, so building in the yard removes a spot instead of stranding someone.

## Intake (Core/Intake.cs)

| Program | Steps (role, minutes) | Search detection |
| --- | --- | --- |
| Basic | Arrival (officer 5), Identity (officer 8), Pat search (officer 5), Housing | 35% |
| Standard | + Property (officer 10), Medical (nurse 20), Basic classification (caseworker 20) | 62% (+20% scanner) |
| Enhanced | + Mental health (caseworker 15); classification with gang intelligence (30) | 72% (+20%) |
| Federal | Biometric identity (15), Search 15, Medical 25, Classification 35, Orientation (caseworker 20) | 82% (+20%, max 95%) |

- **Included programs.** Federal contracts include the federal program; state contracts include Standard. City and county facilities start at Basic.
- **Required by audit.** City Basic, county Basic, state *Enhanced*, federal Federal.
- **Lanes.** The sally port counter, plus each administration desk within 12 m of the sally port (up to 4 lanes in all). State and federal charters start with a booking desk there. A case whose lane is removed returns to holding and resumes.
- **Night staff.** Any line staff member, nurses included, can move to the night shift from the personnel file. Evening arrivals otherwise skip nurse and caseworker steps.
- **Staffing.** Each step needs its role physically at the lane station. Each minute, free staff are matched to the nearest waiting case, with Intake-duty staff first. A step is skipped, and audited, only when nobody qualified is on shift for 60 minutes. Riots, escape responses and serious patients take priority.
- **Hidden arrival conditions.**
  - *Injury* (10%): health 55–75.
  - *Withdrawal*: dependency 40–70.
  - *Chronic condition*: health declines for 3 days unless screened.
  - *Crisis risk*: a 22% chance per 6 hours of a welfare crisis in the first 3 days, unless screened (then a 3-day watch). A crisis costs the welfare penalty and an audit finding.
- **Carried contraband and feuds.** Carry rate by tier: 18% city, 15% county, 12% state, 8% federal (×1.4 for gang members, ×1.5 in withdrawal). Street feuds: a 10% chance per rival-gang resident, at most 2, sets tension 70–86.
- **Classification.** Sets the security level (Maximum / Medium / Minimum) from charges and impulsivity. It also sets the known gang (only 60% true at Standard), and turns known enemies into keep-separate orders (all at Enhanced+, half at Standard).
- **Corruption.** An officer with integrity under 40 who favors the carrier's gang pockets the find.

## Contraband (Core/Contraband.cs)

**Kinds:** phone, narcotics, diverted pills, tobacco, homemade alcohol, improvised weapon, cash, escape tools, lighter, tattoo kit.

| Route | How often | Counter |
| --- | --- | --- |
| Drone | per 30 min: city 0.2%, county 0.4%, state 0.8%, federal 0.6%; ×2 at night, ×0.6 by day, up to ×1.75 with phones inside | RF detection (−35% runs, detects all), defeat system (repels), yard netting (yard drops caught), towers/CCTV/officers spot |
| Throw-over | per 30 min at night or yard time: 0.8% (federal 0.4%) | perimeter barrier within 9 m blocks 35/65/85% by level; CCTV sees |
| Visit | every 3rd day, 45% get a visit; hand-off 6% contact | supervised 2.5%, booths 0.4%, video 0; post-visit search 55% |
| Mail | daily, 4% for gang members or the dependent | photocopy 85%, ion scanner 97% |
| Staff | shift change; misconduct risk ≥40 and integrity <55; ×tier corruption share; ×1.6 if a gang holds cash | bag checks 45%, full screening 80% → write-up |
| Delivery | 4% of unloading trips | gate-post officer +50%, K9 near parking +30%, gate search bay +30% |
| Intake | see above | program level, body scanner |
| Court return | 6% | return search 60% |
| Made inside | alcohol 5% a day for impulsive residents (needs dining fixtures); weapons for people with a beef, 0.4% per 6 h (3% near worn furnishings) | cell checks, maintenance |

**Use and harm:**
- **Narcotics:** −12 stress, +6 dependency. Overdose chance 1.5% + dependency/2000; naloxone near a nurse or officer reverses it. Withdrawal adds +1.2 stress per 30 min.
- **Alcohol:** fights ×1.6.
- **Weapon:** victim injury ×2.2, logged as an assault.
- **Lighter:** small fires.
- **Tattoo kit:** infections.
- **Phone:** comfort, and drone demand.
- **Cash:** staff bribes.
- **Debts:** a 40% chance on cross-gang trades, +8 tension.

**Detection:**
- Cell checks: 35% + integrity/400 + search tools.
- Shakedown: 55% + 5% per officer (up to 4) + tools + K9. Costs $400 overtime, +6 stress, +4 unrest; once a day.
- Pat search after yard: 30%, +25% for metal items with walk-through detectors.
- K9: 35% within 3.5 m. CCTV hand-offs: 25%, 60% when monitored. Managed access: 25% a phone per 30 min.
- Officers recover detected drops in person.
- The owner sees evidence and a coarse estimate, never the true count.

## Unrest, disturbances and riots

- **Unrest target** (every 30 min) sums stress, unsafe residents, crowding over capacity, floor sleepers, missed meals, harmful contraband, fights in the last day, rival gangs and officer coverage against the standard. Lockdown pushes it up; regimes and commissary/phones pull it down. Unrest moves 15% of the gap each time.
- **Disturbance.** Two hours at Critical (75+) start one where the most stressed people are gathered (three or more). If officers in sight reach a quarter of the participants for 10 minutes within 30 minutes, it is contained.
- **Riot.** Otherwise it becomes a riot:
  - Lockdown, and stressed people within 10 m join.
  - Furnishings within 5 m lose condition; injuries every 3 minutes.
  - A $15,000 penalty (scaled) and a triggered audit.
  - It is retaken when officers in sight reach a third of the rioters for 20 minutes. Disturbance training counts officers ×1.5 or ×2; a response team adds 4 after 25 minutes. Otherwise it burns out after 6 hours.
  - Rioters are written up. The facility returns to the lockdown, gate and alarm states it had before.
- **After a fight with officers on shift:** both are kept in housing for 6 hours, a keep-separate order is recorded, and one is moved to another room when a bed is free.

## Audits and contract standing

- **Schedule.** Routine audits every 10 / 7 / 5 / 4 days (city / county / state / federal) at 10:00, announced a day ahead. They take 4 hours with an inspector walking room to room. A riot triggers one the next day.
- **Score.** 100 minus findings, plus credits:
  - **Crowding:** over the tier limit (125% / 115% / 110% / 100%), and floor sleepers.
  - **Staffing:** residents per on-shift officer against 14 / 12 / 10 / 8.
  - **Intake:** program below the requirement (−20, federal −30), skipped steps, waits over 12 h.
  - **Violence:** fights (1.2 points per fight per ten residents, cap 15), weapon assaults, riots.
  - **Welfare:** violations, crises, overdoses, hungry residents.
  - **Records:** lapsed write-ups, missed hearings.
  - **Contract:** refusals.
  - **Security:** escapes.
  - **Contraband:** a sample search.
  - **Credits:** seizures, an intake program above the standard, generous staffing.
- **Results.**

  | Score | Result | Consequence |
  | --- | --- | --- |
  | ≥85 | Exemplary | $3,000 bonus, clears a strike |
  | ≥70 | Pass | none |
  | ≥50 | Conditional | $2,500 fine |
  | <50 | Fail | $10,000 fine and a strike |

  All money is scaled by the tier's penalty scale.
- **Termination.** Three active strikes (30 days) end the contract. So does insolvency: cash plus money owed below the larger of $50,000 of debt or a quarter of opening capital. After termination, contract revenue, transfers, audits and Claude-owned fines stop; Justice's custody penalties are Codex's and still apply.

## Upgrades (Core/Upgrades.cs)

There are 16 purchasable programs with one-time prices and daily running costs (see `docs/upgrade-roadmap.md`), plus 8 planned entries shown as coming later. Commissary and resident phones earn $1.50 and $1.00 per resident per day.

## Balance (critic's harness, 21 days, 3 seeds each)

| Strategy | County intake | State medium | Federal camp |
| --- | --- | --- | --- |
| Idle, accept all | collapses by day 10–14: riots, all audits fail, terminated | collapses, terminated | collapses, terminated |
| Refuse transfers, 2 officers and a nurse | survives at 12 residents; Conditional or Fail audits; most cash ($62–72k) | hard: violence and contraband audits; 2 strikes | survives at 12 residents |
| Builder (wings when beds run short, officers to the standard, nurse and night staff, required intake, dining chairs, advances) | survives 3/3: 47–56 residents, 0 strikes, $44–48k | survives 3/3: about 50 residents, 1 strike, solvent with $59–90k owed | survives 3/3: 38–39 residents, 0–1 strikes |

Over 35 days the same builder also survives 9/9 (no termination, 0–2 strikes, $0–136k cash): county reaches the 96 ceiling at 91–97 residents, state-medium holds 89–94, and federal-camp 65–72 with near-zero unrest. This needs dining units with serving counters, nearest-place meal assignment and trays; before those, the builder collapsed between days 21 and 35 from the single dining room's 64-meal ceiling.

Before the post-critic fixes, the builder failed all 24 of its runs: starvation from long walks to meal shifts, monthly-payment insolvency, and absolute violence counts. Refusing everything is no longer the dominant strategy: refusals cost strikes, and the builder runs a facility four times the size on per-diem.

**Fuzz** (critic's harness): 26 random action types, a round trip every 30–240 minutes, and the ledger and remainders checked every minute. Final runs: 30 seeds × 14 days (604,800 minutes, 4,489 reloads) plus 10 seeds × 7 days with all actions and JsonUtility-style null handling (100,800 minutes, 749 reloads). Both had 0 failures.

## Unity check request (Codex as runner)

Please run in Play, at iPhone landscape and 900×420 as well as desktop.

1. **Pressure tab.** It fits at 280–360 px without horizontal scrolling. Buttons stay stable while the game runs at 4×. The upgrade list scrolls, and Buy buttons enable and disable with cash.
2. **Demo · transfer bus.** The coach drives in on the road, stops at a closed gate (close it first), parks east of the van and unloads 6 people. They wear orange, hold their hands behind their backs, and walk through the painted holding bays to the sally port lane.
3. **Desk lane.** Place an administration desk within 12 m of the sally port. A second lane box appears and two cases run at once.
4. **Demo · drone.** A drone with spinning rotors and a hanging package flies in at about 7 m, hovers at about 2.6 m, drops the package and leaves. With RF detection bought, the drone blinks red and the dropped package shows a pulsing marker, and an officer walks over to it.
5. **Demo · riot** with officers held away. A pulsing red zone appears, rioters raise their arms, debris shows during the riot, and lockdown is on. When it ends, the gate and lockdown return to their previous state.
6. **Demo · audit.** The inspector in a suit with a clipboard walks room to room. A report appears after 4 hours, and the alert banner shows during the audit.
7. **Floor mattresses.** With more residents than beds, mattresses appear on floors in housing rooms, and sleepers lie on them at night. Bedded residents lie on their beds from 22:00 to 06:00, head at the pillow.
8. **Meals.** Residents sit on the table benches facing the table during their meal block.
9. **Faces and clothing.** At close zoom, faces show eyes, brows, ears and a mouth, with varied hair, facial hair and some glasses; tattoos on gang members. Jumpsuits are orange in intake, then green, khaki or red by classification.
10. **Ride along.** From a person card, the camera follows from behind and above, and drag or Q/E orbits. The body-cam monitor updates. Tab or Next cycles people, and Esc or Exit returns to the exact previous build camera. A resident boarding the court van ends the ride with a notice.
11. **Saves.** Save and reload mid-bus, mid-intake, mid-drone and mid-riot, using Unity `JsonUtility` saves.
12. **Console.** No runtime errors. Check that the `lamp-*` emission keyword survives a player build; if the variant is stripped, the markers just render unlit.
13. **New-game menu.** It shows the "Charter a facility" card above the three classic offers. Choose → jurisdiction, institution and location → Charter facility starts that company: reserve equals the type's capital minus land, and state and federal starts have a second desk south of the dining table.
14. **Personnel file.** The Duty tab shows Day 08:00 / Night 20:00 for every line staff member (not the warden), and the selection persists through save and reload.
15. **Pressure tab.** "Advance on revenue" enables once $2,000 is owed. Pressing it moves half of the owed amount, less 3%, into cash. A transfer bus held at a closed gate turns back after two game hours with a refusal notice.

## Interface requests (Codex-owned files)

1. **Walk cache.** `World.Walk` caches an empty route forever when the first `Routes.Find` fails, so a walker with a fixed target never retries. I wrap it as `WalkFresh` in my code; please consider dropping empty cached routes in `Walk` itself.
2. **Escape-risk hook.** Escape tools have no escape-risk effect yet. Please add `partial void ExtraEscapeFactors(Resident resident, List<RiskFactor> factors, ref float total)` in `Justice.EscapeRisk`, and I will add +12 for escape tools and +4 for a phone.
3. **New fixtures.** Please add body scanner, intake/booking desk, drone detector mast, yard netting and walk-through metal detector to `FixtureCatalog` and `FacilityView`, with exactly those kinds. Until then they are program upgrades.
4. **Starter layouts.** Build them from `FacilityType.StarterRooms`. State and federal charters already get a booking desk near the sally port through `World.StartCharter` (Claude's `Simulation.cs`); distinct starter buildings per type would still help.
5. **Penalties after termination.** Justice custody penalties continue after contract termination; suppress them via `PenaltiesApply`.
6. **Staff look.** Faces and hair for staff actors need a hook equivalent to `PrepareResidentPose` (the pressure view only dresses residents).
7. **Visitation.** It is abstract (no visitors on site). A visiting room and visitor actors would make the visit route physical.

## Load robustness

On load, a resident standing where the route planner cannot start (inside a furnishing's footprint or a corner they reached in a straight line) is returned home rather than making the save unloadable. The standing-position route check accepts a route home or to meals. Items held by people who left are dropped, and feuds and separation orders naming them are pruned.

## Known limitations

- Visits, mail and staff smuggling are abstract events with no actors.
- The audit sample search removes items without moving anyone.
- Riots move rioters to a rally point; there is no fighting choreography.
- Disturbance force counts officers in sight within 6 m.
- The 96 ceiling is a performance guard, not a design target.
- The 30-minute crowding and unrest pass iterates every relationship: O(n²) at the 96 ceiling.
