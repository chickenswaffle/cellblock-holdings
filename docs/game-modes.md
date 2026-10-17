# Game modes: jurisdiction, facility type and location

Owner: Claude (`Core/GameModes.cs`, `Runtime/GameModesHud.cs`, `Resources/GameModes.uss`, `Tools/CoreChecks/GameModesChecks.cs`). All charges, courts and institutions are fictional game content.

A new company picks three things:

1. **Jurisdiction tier** (City / County / State / Federal): the difficulty. It sets penalty scale, wages, applicant vetting, oversight and audit cadence, transfer pressure and the intake standard.
2. **Facility type** (three per tier): the specific institution and its core problem. It sets population, temperaments, gangs, charges, court cadence, release odds, starting staff, regime, gate and escape rules.
3. **Location** (eight, each eligible for some tiers): land price, wage index, labor quality, experience, court drive time and a local charge.

`World.ApplyMode(typeId, locationId)` reshapes a first-day company: population, legal records, staff, regime, gate and hiring board. Saves keep the choice in `State.Mode`. The three original contract cards map to `county-intake`, `county-reentry` and `state-modernization` with identical terms. Older saves take that mapping additively without changing their people.

## Tiers

| Tier | Difficulty | Penalties | Wages | Applicant integrity | Locations |
| --- | --- | --- | --- | --- | --- |
| City | Easy | ×0.75 | ×0.85 | -4 | Northbridge, Port Ellery, Halvern, Lakewood, East Tolland |
| County | Normal | ×1 | ×1 | +0 | Briar County, Lakewood, Cedar Falls, East Tolland |
| State | Hard | ×1.5 | ×1.15 | +4 | Mason Ridge, Northbridge, Halvern, Cedar Falls |
| Federal | Expert | ×2.5 | ×1.35 | +12 | Mason Ridge, Halvern, Port Ellery, Cedar Falls, Northbridge, Briar County, Lakewood, East Tolland |

How tiers drive population pressure (`docs/pressure-notes.md`):

| Tier | Transfers per base resident per day | Daily growth | Audit every | Crowding limit | Residents per officer | Intake required (included) |
| --- | --- | --- | --- | --- | --- | --- |
| City | 0.22 | 4% | 10 days | 125% | 14 | Basic (Basic) |
| County | 0.12 | 4% | 7 days | 115% | 12 | Basic (Basic) |
| State | 0.08 | 5% | 5 days | 110% | 10 | Enhanced (Standard) |
| Federal | 0.06 | 5% | 4 days | 100% | 8 | Federal (Federal) |

Contraband pressure also follows the tier: drone runs per 30 minutes are 0.2% / 0.4% / 0.8% / 0.6%, and carried-in rates at intake are 18% / 15% / 12% / 8%. The staff corruption share (30% / 25% / 25% / 12%) scales staff smuggling.

## Facility types

| Facility type | Tier | Security / housing | Residents / beds | Daily revenue / operating | Paid every | Challenge | Signature rule |
| --- | --- | --- | --- | --- | --- | --- | --- |
| City Holding Center (`city-holding`) | City | Minimum / Holding cells | 10 / 12 | $3,300 / $2,300 | 7 d | Churn | Hearings every 1–2 days, and many residents are released at court. |
| Municipal Work Release (`city-work-release`) | City | Minimum / Dormitory | 8 / 12 | $3,600 / $2,600 | 7 d | Trust | Open campus: walk-away risk +6. Good-time credit ×1.5. Rehabilitative regime. |
| Downtown Detention (`city-downtown`) | City | Medium / Shared cells | 12 / 12 | $4,200 / $3,000 | 7 d | Crowding and street feuds | Every bed is filled on day one; two street crews share the block. Fights ×1.2. |
| County Intake Jail (`county-intake`) | County | Medium / Shared cells | 12 / 12 | $4,100 / $2,900 | 7 d | Intake and court calendars | Seven in ten residents await trial. |
| County Reentry Campus (`county-reentry`) | County | Minimum / Shared cells | 8 / 12 | $4,400 / $3,300 | 28 d | Release readiness | Most residents are sentenced with release dates in weeks. Good-time ×1.2. |
| County Maximum Wing (`county-maximum`) | County | Maximum / Secure cells | 10 / 12 | $5,200 / $3,900 | 7 d | Violence before trial | Gate closed at start; escape risk +6; fights ×1.4; one extra officer. |
| State Minimum Facility (`state-minimum`) | State | Minimum / Dormitory | 12 / 12 | $4,800 / $3,600 | 28 d | Programs and walk-aways | Low fence: walk-away risk +6. Good-time ×1.3. You start with a warden. |
| State Medium Prison (`state-medium`) | State | Medium / Shared cells | 12 / 12 | $5,600 / $4,200 | 28 d | Gang politics | Seven in ten residents are gang-affiliated; corrupt applicants +10%; fights ×1.3. Warden and an extra officer at start. |
| State Facility Modernization (`state-modernization`) | State | Maximum / Secure cells | 10 / 12 | $5,400 / $4,100 | 7 d | Old building, tired staff | Staff start worn down (+15 pressure); escape risk +2; you start with a warden. |
| Federal Prison Camp (`federal-camp`) | Federal | Minimum / Dormitory | 10 / 12 | $6,200 / $4,500 | 28 d | Federal standards with no fence | No perimeter fence: walk-away risk +10. Fights ×0.5. Penalties ×2.5. Warden and nurse at start. |
| Federal Correctional Institution (`federal-fci`) | Federal | Medium / Shared cells | 12 / 12 | $7,000 / $5,200 | 28 d | Trafficking networks | Corrupt applicants +15%; gangs common; escape risk +4. Warden and nurse at start. |
| Federal Supermax (`federal-supermax`) | Federal | Supermax / Single cells | 6 / 12 | $9,000 / $7,000 | 28 d | Absolute control | Escape risk +14; escape penalty $50,000; strict regime locked; gate closed; warden, nurse and three elite officers at start. |

## Locations

| Location | Wage index | Land index | Labor quality | Court drive | Local charge |
| --- | --- | --- | --- | --- | --- |
| Briar County | ×0.95 | ×0.85 | +0 | 0 min | Livestock theft |
| Lakewood | ×1 | ×1.05 | +4 | 0 min | Burglary |
| Northbridge | ×1.05 | ×0.9 | -2 | 15 min | Possession with intent to sell |
| Port Ellery | ×1.1 | ×1.2 | -4 | 15 min | Smuggling |
| Cedar Falls | ×0.9 | ×0.75 | +6 | 35 min | Driving under the influence |
| Mason Ridge | ×0.92 | ×0.7 | +2 | 40 min | Assault |
| Halvern | ×1.12 | ×1.25 | +3 | 0 min | Bribery |
| East Tolland | ×0.88 | ×0.8 | -6 | 20 min | Copper theft |

## Checks

`Tools/CoreChecks/GameModesChecks.cs` pins every rule a mode drives:

- catalog consistency, and the original contracts mapping additively;
- population, charges, courts and staff per type;
- penalty scale and the fixed supermax penalty;
- wages and labor quality at a shared location, land index and the escape offset;
- charge severity and pretrial share, and gang share across seeds;
- good-time credit, fight scale and release bias on an identical population;
- the location's court drive, charter timing, two-day runs per type and malformed saves.

All 24 deliberate mode mutants are killed.

## New-game flow (wired)

- The new-game menu opens the charter screen (`ShowModePicker`), and the three classic offers stay as quick starts.
- `World.StartCharter(typeId, locationId)` books the type's own capital and the location's land price and seeds the population per type and location. It adds a booking desk near the sally port for state and federal types, then applies the mode.
- `World.Contract` derives its terms from the chartered type and location.
- Still open (Codex): distinct starter buildings per type from `FacilityType.StarterRooms`/`Housing`/`Beds`.
