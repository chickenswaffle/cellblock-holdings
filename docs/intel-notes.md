# Informants and snitches: implementation notes (phase C)

Plan: `docs/economy-and-roles-plan.md` §7.

**Files:** `Core/Informants.cs` (World partial; serialized as `State.IntelWork`, plus `Resident.Suspicion`, `Jacket`, `ProtectiveCustody`), `Runtime/GameIntelHud.cs`, styles appended to `Resources/Pressure.uss`, and `Tools/CoreChecks/IntelChecks.cs`. All are Claude's.

**Edits to Claude's existing files:**

- `Contraband.cs`:
  - stash intel when an item is introduced, picked up or traded;
  - drones are planned 90–240 minutes ahead (`PlanDrop`/`LaunchPlannedDrops`);
  - smuggling officers become intel.
- `Personnel.cs`:
  - a new officer duty, "Intelligence";
  - tip dispatch (`DirectIntelStaff`) runs before contraband pickups;
  - fights use `IntelFightFactor` and exclude protective custody;
  - `AfterFight` runs after a fight.
- `Intake.cs`: a paperwork check when intake completes.
- `Population.cs`:
  - hydrate and the tick;
  - protective custody stays home;
  - protective custody is excluded from riots;
  - unrest and audit hooks, with the audit snapshot counters.
- `Simulation.cs`: protective custody eats in the cell and is not counted for meal service.
- `Upgrades.cs`: a new Intelligence category.
- `Economy.cs`: protective custody blocks jobs, `DebtDefaulted` creates a planned attack, and a stash held to work off a debt becomes intel.

## Intel

| Kind | Created when | Known by | Ends |
| --- | --- | --- | --- |
| Stash / Escape (tools) | An item is held or stashed | Holder, cellmates, gang members within 12 m, friends (trust 60+, tension below 40) | The item leaves that holder, or after 14 days |
| Drop | A drone is ordered 90–240 minutes before launch | The ordering resident's circle (a phone holder first) | 60 minutes after launch |
| Hit | Daily at 12:00: a nemesis (tension 82+) backed by a weapon or a rival gang, 40% a day. Also a snitch hunt, a jacket (green light) or a debt default | The attacker's circle; tension rises to at least 72 | 3–5 days, separation or the fight |
| Smuggler | A staff member brings something in | The buyer's circle | 7 days |

- Up to 12 people know each piece of intel.
- Every 6 hours a knower passes it to a friend within 3 m with trust 55+, 30% of the time.
- Up to 120 records are kept.

## How tips arrive

- **Notes** (21:10 daily).
  - The base chance is 5% per person, × trust/60 (at least 0.3), × 0.4 while informants are being hurt, × 2 with the tip line.
  - A target warns about their own planned attack first. Then someone with a grudge (tension 58+) reports what they know. Then a frightened person (safety below 45).
  - Otherwise an impulsive person with a rival (tension 70+) may invent a stash: 25% of the time, or 50% on the tip line.
- **Informants** (19:00 daily). A handler must be on shift: the warden, a caseworker (not a nurse) or an officer on Intelligence duty.
  - A reliable informant reports the most serious thing they know 75% of the time. The order is attack, drop, staff, escape, then stash.
  - Reliability is 55–95%, hidden, and shown as "confirmed n of m" once proven.
  - Double agents lie 70% of the time: they invent stashes on rivals. When their gang orders a drone, they plant a decoy drop tip at the opposite fence.
  - Every report adds 4 suspicion, 2 under "Protect sources", and half that with the intelligence unit.

## Acting on tips

| Action | For | What happens |
| --- | --- | --- |
| Search cell | Stash, Escape | The nearest free line officer (an Intelligence officer first) walks to the cell and searches at +30%. A corrupt officer favoring the subject's gang looks away. Found: confirmed, +1 trust, exposure ×1, a snitch hunt for weapons, phones, drugs or tools. Nothing found: subject +6 stress, −3 trust |
| Search the unit | Stash, Escape | Every cell in the room is searched; everyone there gets +3 stress and trust drops 1. Exposure ×0.3 and no snitch hunt |
| Post an officer | Drop | The officer waits at the spot until expiry. If the drone comes over (within 4 m), the tip is confirmed and the officer takes the drop as it lands. A decoy expires as wrong |
| Separate them | Hit | The attacker is kept in housing for 12 hours with keep-separate orders. True: the attack is prevented and counted. False: the attacker gets +8 stress and trust drops 1 |
| Investigate | Smuggler | Guilty if the tip is true and the officer's integrity is below 55, or always with internal affairs: a severity-3 write-up and −10 integrity. Innocent: +10 pressure, −8 morale |
| Corroborate | Stash, Escape, Smuggler | 3 hours. A true tip covered by CCTV, or known to 3+ people, is marked confirmed by a second source. A false tip is dropped |
| Ignore | Any | If an ignored attack then happens, it is an ignored warning: an audit finding |

**Policies** for tips undecided after 30 minutes:

- **Act at once:** the first direct action.
- **Protect sources** (default): corroborate first, then search the unit.
- **Hold for review:** never acts.

**Exposure:** each knower other than the subject gets 12/√(number of knowers) suspicion, and the source gets +10 if 3 or fewer knew (+4 otherwise), both × the action's scale.

## Standing

- **Suspicion** (0–100) rises with:
  - being pulled aside: +8, or +4 when a caseworker is on shift;
  - meetings;
  - exposure;
  - snitch hunts (+15 to the most suspected knower, plus a planned attack by the holder's gang);
  - rumors: an impulsive, undisciplined resident with a nemesis, 12% a day, +18 or ×1.5 from a gang member;
  - a corrupt officer (integrity below 40 with a favored gang) selling an informant's name: 5% a day, ×0.3 with secure records, ×0.2 with internal affairs, sets 100;
  - court papers showing past cooperation: 6% of arrivals, when 2+ gang members are present, +45 unless records are secured;
  - visible rewards: +1 to +3 a week.

  It fades by 1.5 a day until a jacket is set.
- **A jacket** is set at 70. It is permanent while the person is in custody. It burns an active informant and gives a gang member the green light to attack.
  - **In general population:** −0.6 safety and +0.4 stress every 30 minutes, and a gang member is 1.5× more likely to fight them.
  - **Below 30 safety:** they are moved to protective custody (automatically, unless "Approve protection" is off).
- **Protective custody** keeps the person in their cell: meals on trays, no fights, no riots, no work. Every 30 minutes: +0.6 safety, −0.15 belonging, +0.15 stress.
- **Trust in staff** (0–100, starting at 60):
  - It drifts back toward 60 by 5% a day.
  - It falls with markup above 50% and with cost-of-care deductions.
  - Unrest: "Distrust of staff" below 45, up to +12; "Trust in staff" above 70, down to −4.
  - Notes scale with trust, and so do recruitment odds: ±1 point per 4 trust.
- **An attack on a jacketed person:** informant supply dries up for 10 days (notes ×0.4, recruitment −30%) and trust falls by 2.

## Recruiting

- **Odds:** 15% base, adjusted as follows, clamped to 2–85%:

  | Factor | Effect |
  | --- | --- |
  | Overdue debt | +20% |
  | Awaiting trial | +15% |
  | Dependency above 40 | +10% |
  | Safety below 40 | +15% |
  | Belonging below 30 | +10% |
  | Gang member | −25%, or −10% if their belonging is below 50 |
  | Broke, offered store credit | +10% |
  | Awaiting trial, offered a court letter | +15% |
  | Trust | (trust − 60)/400 |
  | Suspicion | − suspicion/200 |
  | Informants being hurt | −30% |

- **Program size:** 2 informants, or 6 with the intelligence unit.
- **Double agents:** a loyal gang member (belonging 60+, discipline 50+) who agrees is a double agent half the time.
- **Weekly rewards:**
  - store credit: $10 a week from company cash into trust;
  - phone time: $4 a week from company cash into trust, plus belonging;
  - better job: the first open slot;
  - court letter: a cooperation flag for phase D, and a cooperation history that court papers will show.

## Upgrades (Intelligence category)

| Upgrade | Price | Running cost | Effect |
| --- | --- | --- | --- |
| Intelligence unit | $12,000 | $60/day | 6 informants; meetings half as noticeable |
| Anonymous tip line | $3,500 | $15/day | ×2 notes; half by phone, and phone tips are invented twice as often |
| Secure legal records | $4,000 | $10/day | No paperwork exposure; leaks ×0.3 |
| Internal affairs | $8,000 | $60/day | Investigations always find a smuggling officer; leaks ×0.2 |

**Catalog:** 20 available upgrades and 6 planned.

## Audits

| Finding | Points |
| --- | --- |
| Protective custody above 15% of the population | Welfare −3 |
| Each known informant assaulted since the last audit | Welfare −6, up to −18 |
| An ignored warning | Welfare −4 each, up to −8 |
| More than 2 tip actions that found nothing | Records −1 each beyond 2, up to −6 |
| Confirmed tip actions | Contraband credit +1 each, up to +4 |

## HUD

**Pressure › Intelligence:**

- the trust meter and summary;
- policy chips and "Approve protection";
- up to 4 open tips, each with its own action buttons;
- informant files;
- the last three intelligence stories;
- in editor and debug builds, "Demo · tip and snitch".

**Person card › Standing:**

- suspicion, jacket or rumor;
- the informant file;
- protective custody state;
- recruit odds for store credit and for a court letter;
- buttons: approach with store credit or a court letter, reward chips, close file, and the protective custody toggle.

## Evidence

- **Core checks:** 257/257 (19 new intelligence checks). Literals are pinned:
  - a gang rumor adds exactly 27;
  - court papers add 45;
  - a jacket at 75 → burned, with a green-light attack ≥4.4× more likely;
  - a planned attack triples the fight chance.
- Behaviors covered: unit sweeps expose under half as much as a cell search, decoy positions, corroboration outcomes, all three policies, a mid-tip reload, and malformed saves.
- Balance and fuzz results are in the COORDINATION log.

## Unity check request (Codex as runner)

1. Pressure tab at 280–360 px: the Intelligence section wraps, and every tip row's buttons are at least 34 px tall and stay inside the panel.
2. "Demo · tip and snitch":
   - a note row appears with "Search cell", "Search the unit", "Corroborate" and "Ignore";
   - choosing "Search cell" sends an officer who visibly walks to the cell;
   - the labeled resident's card shows "KNOWN SNITCH".
3. Protective custody toggle: the resident walks home and stays there through meals and yard; a tray arrives after the meal.
4. A drone tip with "Post an officer": the officer walks to the spot and stands there; when the drone comes over, the dropped package disappears as it lands.
5. Save and reload with an "Acting" tip: the officer resumes the same task.

## Interface requests (Codex's files)

- **Optional:** a visible note or kite prop, and a "meeting" pose for informant meetings (presentation only).
- **Justice:** an escape-plan hook (`ExtraEscapeFactors`, requested earlier) would let escape intel feed the escape state machine directly. Today, escape intel only finds the tools.
