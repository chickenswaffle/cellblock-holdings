# Kitchen, counsel and roles: implementation notes (phases B, D, E)

Plan: `docs/economy-and-roles-plan.md`. Economy: `docs/economy-notes.md`. Informants: `docs/intel-notes.md`.

## Phase B: kitchen

**Files:** `Core/Kitchen.cs` (serialized as `State.Kitchen`, plus `Resident.Diet`), `Runtime/GameKitchenHud.cs` (a block in Company › Prison economy) and `Tools/CoreChecks/KitchenChecks.cs`.

**Edits to Claude's files:**

- `Personnel.cs`: a new Cook post (role Technician; ranks Cook and Head Cook; day shift, at most 2). Cooks work behind the serving counter. A listing includes a cook when a kitchen exists and none is on staff.
- `Economy.cs`:
  - `MealGain` applies the menu;
  - kitchen line slots per counter are 2 with catering, 3 with a kitchen line, 4 with an industrial kitchen;
  - a diet is recorded when the account opens.
- `Contraband.cs`: fruit on the menu ×2 hooch.
- `Population.cs`: hooks.
- `Upgrades.cs`: kitchen (2 tiers) and tool control.

**Catering stays the baseline.** Without a kitchen, nothing changes: meals are catered inside operations, and the menu has no effect.

**Cooking on site** needs a kitchen line ($9,000, $30 a day) and a serving counter.

- A meal window (07, 12, 18) is cooked on site if a cook is on shift.
- An industrial kitchen ($18,000, $60 a day) also cooks breakfast with 2+ line workers and no cook.
- At the end of each window the company books:
  - food: covers × the plate price;
  - +40¢ per special diet;
  - 60¢ per loaf;
  - and a catering saving of covers × $2.80 (all × the location's wage index).

| Menu | Plate | Food gained ×, mood per minute eating | Unrest | Note |
| --- | --- | --- | --- | --- |
| Budget | 90¢ | ×0.75, +0.004 | +3 "Thin, bland meals" | State and federal: a nutrition finding (−2). Hungry people buy soups |
| Standard | $1.50 | ×1 | 0 | What the caterer served |
| Fresh | $2.40 | ×1.08, −0.004 | −1 | Fruit: ×2 hooch |
| Premium | $3.80 | ×1.17, −0.008 | −2 | Credit at inspection (+2) |

**Diets:** about 12% of residents have one (4% medical, 5% religious, 3% vegetarian), deterministic per person. If special diets are switched off:

- those residents eat ×0.7 and get +0.01 stress per minute;
- unrest "Special diets refused" up to +6;
- a Welfare finding: −1 county, −3 state and federal.

**Other policies:**

- **Food loaf:** residents with a write-up in the last 2 days eat ×0.85 with +0.008 stress; +2 unrest; a state or federal finding (−2).
- **Holiday dinner:** once every 14 days; the premium plate + $3 per cover; −0.02 stress per minute and a little belonging while eating; −4 unrest for 2 days.

**Daily kitchen risks (15:00):**

- Line workers with Discipline below 50 steal food 4% of the time: +4 soups and a $5 loss.
- Line workers with Impulsivity above 60 take a knife 0.8% of the time (×0.1 with tool control): a weapon, with stash intel.
- Impulsive workers brew hooch 1.5% of the time.
- Searching the line catches 70% of thefts and knives (a write-up), at −1 belonging and +1 stress a day for the workers.
- Food poisoning: 0.3% × 3 on Budget × 2.5 with a counter below 60 condition × 0.5 for an industrial kitchen. 3–8 sick (−14 health, −20 food) and a Welfare finding (−6 each).
- Grease fire: 0.15% × 0.7 for an industrial kitchen. Counter −30 condition, or −8 with smoke detection.

**Evidence:** 12 kitchen checks, with plate costs, the catering saving and the holiday dinner pinned to the cent.

## Phase D: counsel

**Files:** `Core/Counsel.cs` (`State.Counsel`, plus `Resident.CourtSeen`, `StatusSeen`, `ProSeFiled`), `Runtime/GameCounselHud.cs` (a Counsel block on the person card and Pressure › Courts) and `Tools/CoreChecks/CounselChecks.cs`. Also `SavingsGoal` (declared in `Economy.cs`), a law library upgrade, and hooks.

**Justice boundary.** Court rolls stay in `Justice.Resolve` (Codex's file, not edited). Counsel acts only at these points:

- **The afternoon before a hearing (16:00):** motions. A granted dismissal or bond uses Justice's own `Release`.
- **After a hearing (every 10 minutes, once per appearance):**
  - a continued hearing's `NextCourtMinute` moves earlier for private counsel (never before tomorrow 09:30), or later for an overloaded public defender;
  - at sentencing, `SentenceDays`/`ReleaseMinute` are cut, never below tomorrow.
- **Codex review requested:** if these belong inside `Resolve` instead, a `CounselOdds` hook there would replace the after-the-fact adjustments.

**Public defender load:** 0.6 + backlog/40 + (unrepresented pretrial)/60, clamped to 0.5–2. Above 1.1, continued hearings slip round((load−1)×3) days, up to 3. "Cases stalled at the public defender" adds up to +4 unrest when the slip is 2+ days.

| Counsel | Retainer (× wage index) | Sentencing cut | Earlier date | Dismissal motion | Bond motion | Discovery |
| --- | --- | --- | --- | --- | --- | --- |
| Storefront | $250 + $100 × severity | 5% | — | 10% | — | — |
| Experienced | $700 + $200 × severity | 12% | 1 day | 18% | 35% | — |
| Big firm | $1,500 + $350 × severity | 20% | 2 days | 28% | 50% | Names a confirmed informant (+60 suspicion) |

**Paying:** a quarter down from the resident's trust account, then a quarter weekly. A missed instalment and the lawyer withdraws.

**Motions:**

- **Dismissal:** +(60 − referring credibility)/200 for charges built on a write-up referral.
- **Bond:** severity 2 or less, funded by the account ($200 × severity × wage index) or by family support of 40+.
- **Cooperation:** an informant with a court letter gets +15% off at sentencing, with or without a lawyer.

**Who hires:** residents decide on the day before their deposit day. A pretrial resident with a severity-2+ charge hires the best tier they can afford with 35% of the retainer + $5 in hand, half the time. Until then they save, cutting extras at the store but not necessities.

**Law library** ($2,500, $10 a day): unrepresented residents with Discipline 55+ file one motion of their own (6% + half the referral weakness); "Legal access" −2 unrest.

**Evidence:** 10 counsel checks: the retainer formula, instalments and withdrawal, defender delay, earlier dates, a 20% sentencing cut (60 → 48 days), cooperation credit (40 → 34), dismissal with release and account closure, bond, discovery, AI hiring, pro se motions and reload.

## Phase E: taking a role

**Files:** `Core/Roles.cs` (`State.Roles`; the controlled person is runtime-only), the ride panel in `Runtime/GamePressureHud.cs` and `Runtime/GameRideAlong.cs`, and `Tools/CoreChecks/RoleChecks.cs`.

**How it works:**

- During a ride-along, "Take the role" makes that person's decisions. Prisoners, officers and the warden have decisions; cooks, caseworkers and technicians do not yet.
- The simulation still walks them.
- At each decision point the system posts a card instead of deciding. The player's choice runs the same core method the simulation uses: `Shop`, `AssignJob`, `HireCounsel`, `TipFrom`, `Confront`, `SetProtectiveCustody`, `PlayCards`, `Introduce`/`FileWriteUp`, `ResolveSearch`, and the policy setters.
- After 60 game minutes the default, what they would have done, applies.
- Play pauses for each new card.
- Cards are saved; the controlled person is not. After a reload, pending cards resolve by default.

| Role | Cards | Default |
| --- | --- | --- |
| Prisoner | Store day: shop, only necessities, or save everything | Shop |
| | A job offer (the slot is held while the card is open) | Take it |
| | A lawyer: stay with the defender, or hire an affordable tier | Stay |
| | You know something: drop a note (becomes a Note tip) or keep quiet | Keep quiet |
| | A known snitch within 6 m: confront (the fight logic, if within 2.6 m), spread the word (+10 suspicion), or leave it | Leave it |
| | Not safe: ask for protection or tough it out | Protection below 30 safety |
| | Cards after dinner: play or sit out | Play |
| Officer | A 20% chance per shift of an offer from a gang member: take it (the item goes in, −6 integrity), refuse, or report (a write-up on them, +3 integrity) | Refuse |
| | On a tip search: search properly, quick look (−25%), or look away (the tip fails, −5 integrity) | Search properly |
| Warden | Protective custody requests when auto-approval is off | Approve |
| | Every 3 days the owner demands one lever that applies: budget menu, 65% markup, stopping yard searches, or 10% cost of care. Agree (+8 confidence) or refuse (−12) | Agree |

**Goals** shown on the ride panel:

- **Prisoner:** release or next hearing, savings, counsel, write-ups, suspicion, safety.
- **Officer:** integrity, pressure, detections, fights stopped.
- **Warden:** unrest, standing, owner confidence.

**Evidence:** 7 role checks:

- nothing waits without a role;
- store-day choices and the default after expiry;
- job, lawyer and cards;
- note, confrontation and protection;
- bribe report and looking away;
- owner demands and confidence;
- reload behavior and malformed cards.

## Unity check request (Codex as runner)

1. **Company › Prison economy › Kitchen:**
   - with no kitchen, the text reads "catered";
   - "Demo · store day and debts" then a kitchen (Pressure › Upgrades, or the kitchen demo on a counter) shows the menu chips and today's dishes;
   - hiring a cook from People: the cook stands behind the counter during meals.
2. **Person card on a pretrial resident:**
   - the Counsel block shows the defender's caseload and three hire buttons with prices (disabled with a reason when unaffordable);
   - after "Pressure › Demo · counsel" (or `StageCounselDemo`), the block shows the tier, paid amount and next payment.
3. **Ride along with a prisoner and press "Take the role":**
   - the goals text appears;
   - at a store day (or after the economy demo), a card with three choices appears and play pauses;
   - choosing one updates the Money block;
   - "Hand back the role" removes the goals and cards.
4. **Ride along with the warden, take the role and run 12 game hours:** an owner card appears with Agree and Refuse.
5. **Fit:** at phone landscape and 900×420, the ride panel with two cards stays inside the safe area. The body-cam feed shrinks first (`LayoutPressure` already caps the panel height).
