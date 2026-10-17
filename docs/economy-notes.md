# Prison economy: implementation notes (phase A)

Plan: `docs/economy-and-roles-plan.md`. This note covers what phase A ships.

**Files:** `Core/Economy.cs` (World partial), `Runtime/GameEconomyHud.cs` (GameHud partial), `Resources/Economy.uss` and `Tools/CoreChecks/EconomyChecks.cs`. All are new and Claude's.

**Small edits to Claude's existing files:**

- `Population.cs`: hydrate, the tick, the job target in `PressureTarget`, and the unrest and audit hooks.
- `Simulation.cs`: a new `MealGain` hook, and kitchen-line residents count as served.
- `Contraband.cs`: deals are paid for in soups, visits warm families, and `Introduce` is now internal.
- `Upgrades.cs`: a commissary stockroom tier, and the flat store and phone income is gone.
- `GameHud.cs`: three hooks.

## Two books

| Book | What it holds | Check |
| --- | --- | --- |
| Company ledger (existing `Book`) | Company money | `Reconciles()` |
| Trust fund (`EconomyState.Accounts`) | Money held for residents | `TrustReconciles()`: the sum of all balances equals `TrustCreditedCents − TrustDebitedCents` |

- Every balance change goes through `Credit`/`Debit`.
- Loading a save that does not reconcile is rejected.
- Soups and black-market goods move between residents and never create cash.

## Money flows

All amounts are multiplied by the location's wage index.

| Flow | Amount | When |
| --- | --- | --- |
| Booking money | 0–25% of support × $1 | When an account opens (arrival, or the first load of an older save) |
| Wages | 25–60¢ an hour by job × pay scale (half, standard, double) | Paid at 21:00 for minutes actually at work |
| Restitution | 15% of a sentenced worker's pay | Leaves the trust fund |
| Cost-of-care deduction | 0, 10 or 20% of wages and deposits | Goes to the company |
| Family deposit | Support × 60¢ × mood (0.7–1.3, from Belonging) × warmth (+15% per letter, call or visit, up to 3), rounded down to $5; skipped 15% of weeks | Weekly on the person's deposit day, 10:00 |
| Deposit fee | $1.95 + 0, 3, 5 or 8% | Goes to the company. Above 5%, families send 15% less |
| Commissary | Retail = wholesale × (1 + markup) | Weekly store day, 14:30 |
| Phones | 8–30¢ a minute; calls of 10–20 minutes | Evenings 20:00–21:30; the facility keeps 40% |
| Gate money | $25 city, $40 county, $75 state, $100 federal | At release, from company cash, with the account balance |

**Family support** is a deterministic function of seed and person. About 35% of residents get almost nothing, 45% get a middling amount and 20% get a lot. It fades slowly without contact.

## Jobs

| Job | Slots | Pay | Hours | Effect and risk |
| --- | --- | --- | --- | --- |
| Orderly | 1 per 12 present (up to 4) | 25¢ | 09–12, 14–17 | Mends the most worn furnishing nearby; rotates rooms every 90 minutes |
| Kitchen line | 2 per serving counter (up to 3 counters) | 45¢ | 06:30–09, 11:30–14, 17:30–19:30 | +8% food per minute per cook (up to 3) for diners in that room; eats at the counter and counts as served |
| Library clerk | 1 per shelf (up to 3) | 35¢ | 09–12, 14–17 | Readers within 5 m settle; needs Discipline 45+ |
| Maintenance helper | 1 per line technician (up to 2), with an electrical panel or climate unit | 60¢ | 09–12, 14–17 | Mends utilities; low-discipline helpers may pocket escape tools |
| Commissary clerk | 1, with a commissary | 45¢ | 14–17 | Saves $12 of staff time a day; a few soups go missing |

**Rules:**

- Workers set off 60 minutes before a shift, but meals come first for everyone except the kitchen line.
- Work replaces yard time.
- Maximum-security residents can only be orderlies.
- A serious write-up in 7 days blocks or ends a job. So does a contraband or weapon write-up in 14 days for the kitchen and store.
- Residents with a beef (tension 58+) are kept off the same crew.
- Work spots must be both clear and reachable from the yard.
- A removed furnishing or a blocked route ends the job.
- Each morning at 08:15 a caseworker or the warden fills open slots: steadiest and most in need first, kitchen first. This can be switched off.

## Commissary

**Items and base wholesale prices:**

| Item | Base wholesale |
| --- | --- |
| Soups | 35¢ |
| Hygiene kit | $2.50 |
| Instant coffee | $4.50 |
| Snacks | $1 |
| Stamps and paper | $1.50 |
| Sneakers and sweats | $24 |

**Prices:**

- There is a new supplier list every 3 game days.
- The index is a deterministic, mean-reverting walk with occasional shocks (a 5% chance per item per cycle, ±15–35%), bounded to 0.7–1.5. It is a pure function of seed, item and cycle: no state is stored and nothing can reroll on reload.
- Shocks post a supplier notice.

**Demand:**

- Each person shops once a week. Necessities come out of the balance less $3. Extras come only out of what isn't being saved: a pretrial resident facing a severity-2+ charge keeps a storefront lawyer's down payment back, and a client keeps their next instalment.
- Elasticity compares retail to a "fair" price (base × 1.35): 0.5–0.7 for needs, 1.2–2 for wants.
- Needs are always bought when affordable.
- Hunger, recent trays, debts and running a store raise soup demand.
- Hungry people (food below 45) eat their own soups: +7 food each, at most 3 a day.

**The stockroom** (commissary tier 2, $6,000) can buy two weeks of each item ahead at today's price. Later sales draw down that stock at its cost basis.

**Markup** above 50%:

- unrest of 0.12 per point, up to +6;
- families send up to 40% less;
- above 65%, state and federal monitors cite it (−2).

Because needs are inelastic and budgets are limited, raw profit keeps rising with markup. These consequences are the counterweight.

## Debts, the store man and cards

- **Store man.** Any resident with 12+ soups, Sociability 55+ and Discipline 40+. They lend up to 3 soups at two-for-one to a hungry, broke resident whose home is within 10 m, due on the borrower's next store day.
- **Repayment** happens on store day, from soups, or with a Cash item worth 20 soups.
- **Missed payments.** Each miss adds 25% to the debt and pushes the due date out 2 days. A large debt is 10 soups or more.

  | Miss | What happens |
  | --- | --- |
  | First | +6 tension between the two (×1.5 for a large debt), the debtor loses 2 safety per miss so far (4 for a large debt), a story |
  | Second | +10 tension (×1.5 for a large debt); for a large debt the creditor's gang leans in (+6 each). If the creditor has a stash, the debtor ends up holding it and half the debt is forgiven |
  | Third | A small debt is written off (+4 tension). A large one defaults: tension at least 70, −10 safety, and, when the creditor is in a gang, a planned attack |

  The first balance run showed why this matters. Without a store, nobody has soups, every deal became debt, and 75 defaults by day 32 cost two county contracts.

- **Card games** run at 20:30 between impulsive residents (Impulsivity above 55) with homes within 10 m: stakes of 2–6 soups, and a loser who can't pay goes into debt.

## The black market

**Street prices, in soups:**

| Item | Base |
| --- | --- |
| Tobacco | 2 |
| Lighter | 4 |
| Alcohol | 6 |
| Tattoo kit | 9 |
| Pills | 10 |
| Weapon | 14 |
| Drugs | 22 |
| Escape tools | 60 |
| Phone | 110 |

- **Scarcity:** × (1.5 − 0.12 × that item's count inside), clamped to 0.6–1.8.
- **Enforcement:** × (1 + 4% per seizure in the last 3 days, up to 50%).
- So the HUD's price line is a second, independent read on how much is getting in.
- **Paying:** a buyer pays in this order:
  1. soups;
  2. a Cash item (20 soups);
  3. their trust account at the soup's going price, as a transfer arranged with family outside. The money moves between two trust accounts, so dealers build up balances.

  Anything still short becomes a debt to the seller, and a buyer already owing more than 20 soups is turned away. This replaces the old random cross-gang "debt" tension.

## Unrest and audits

- **Commissary prices:** see markup above.
- **Phone rates:** above 21¢, +0.4 a cent, up to +4. State and federal monitors cite it (−3).
- **Cost-of-care deductions:** 0.2 per percent.
- **Unpaid debts:** overdue debts per 10 residents × 3, up to +8.
- **Residents with jobs:** up to −6.
- **Store and phone access** now calm only the people who actually used them in the last 8 days. The old flat −3/−2 is gone.

## HUD

**Company › Prison economy:**

- trust fund total, number broke, number working, overdue debts and gate money;
- job slots filled and pay, an auto-assign toggle and the pay scale;
- markup chips and the price list (▲/▼ for this cycle, stock held, "Stock up" with a stockroom), plus the supplier notice;
- phone rate chips, deposit fee and cost-of-care chips;
- street prices, loans and defaults;
- 7-day sales sparkline and totals, and the last three economy stories;
- in editor and debug builds, "Demo · store day and debts".

**Person card › Money:**

- balance, soups, family support and the store-man flag;
- job, pay, days worked and earnings, with assign chips per job (disabled with the blocker as tooltip) and Leave job;
- debts owed and owed to them;
- last store purchase and call spending.

## Evidence

- **Core checks:** 238/238 (23 new economy checks). Literals are pinned:
  - base street prices 33/3, supply 17, seizures 24;
  - a deposit of support 50 nets exactly amount − ($1.95 + 3%);
  - wages equal minutes × rate / 60;
  - 15% restitution and 20% cost of care;
  - $40 county gate money;
  - two cooks per counter.
- **Fixes to existing checks:**
  - the old commissary/phone unrest check now requires actual use;
  - the fight-scale mode comparison clears jobs, so only the type's fight scale differs;
  - the determinism check includes `Economy`.
- **Offline Unity typecheck:** passes for editor/macOS and iOS.

## Unity check request (Codex as runner)

1. Company tab at 280, 300 and 314 px widths and at phone landscape: the Prison economy section wraps without horizontal scroll. Each chip is at least 34 px tall, and all 8 markup chips stay visible.
2. "Demo · store day and debts": the price list shows ▲/▼; the person cards for the demo's lender, borrower and buyer show soups, a store-man loan and a tobacco debt.
3. Assign a resident to the kitchen line with a serving counter installed. They walk to the counter before 06:30 and stand there through breakfast, then return. Pay appears on the card after 21:00.
4. An orderly visibly moves between rooms during 09–12; nobody employed goes to the yard during work hours.
5. Buy the commissary, then the stockroom. "Stock up" buttons appear; after a stock-up, the row shows units in stock.
6. Save and reload mid-day with debts and jobs (both company slots and the backup): the card shows the same balance, debts and job.

## Interface requests (Codex's files)

- A storage rack or stockroom fixture would let stock capacity come from the building instead of the upgrade tier.
- A washer/dryer fixture would add laundry jobs, and a workbench would add industries.
- A visible store-day queue at the counter is optional presentation.
