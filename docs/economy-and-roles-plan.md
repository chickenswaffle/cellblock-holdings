# Economy, kitchen, informants, counsel and playable roles: design plan

Status: **implemented** on `codex/claude-overload` (2026-10-17), all five phases, in the recommended order A → C → B → D → E. The implementation notes are in `docs/economy-notes.md` (A), `docs/intel-notes.md` (C) and `docs/kitchen-counsel-roles-notes.md` (B, D, E). Where the build differs from this plan, the notes are authoritative:

- wages are hourly;
- prices change every 3 days;
- debt consequences were rebalanced after the first balance run;
- counsel works around `Justice.Resolve` rather than through a hook;
- "Take the role" covers prisoners, officers and the warden. It builds on the systems that exist on `codex/claude-overload`: population pressure, intake, contraband, upgrades, personnel, justice and the ride-along. Owners and hooks are named per system.

The user asked for:

- a kitchen with hired cafeteria workers, bought food in different qualities and types;
- a commissary with fluctuating prices;
- a heavy, antagonistic snitch system with known snitches, confidential informants and jailhouse snitches;
- paid lawyers and public defenders;
- decisions in every role: prisoner, guard, warden, worker and builder. For example, a prisoner who earns money in the chow line can hire a real lawyer or move on the snitches;
- a prison economy that makes sense.

## 1. The idea that ties it together

**Every dollar and every secret moves between people.** There is one economy, and each role sees it from a different seat:

- **The company** sets food quality, commissary markup, phone rates, inmate wages and fees. These are its profit levers, and each one pushes on unrest, contraband demand, audits and lawsuits.
- **Prisoners** earn and spend real balances. Money buys comfort (store goods, calls home), protection, a lawyer or contraband. Being broke pushes people toward hustles, debt and the black market.
- **The black market** runs on commissary goods. Ramen soups are the currency, so the commissary price of a soup sets the exchange rate of every illegal deal.
- **Information** is the second currency. Prisoners know where stashes are and who is planning what. The administration buys that knowledge from informants. The population punishes anyone it believes is selling it.
- **Staff** sit in the middle. A poorly paid, burned-out officer is the cheapest smuggling route and the easiest leak of an informant's name. This uses the existing `Integrity`, `Pressure` and `Favoritism` fields.

The central loop the player has to manage is the **chow-to-store squeeze**:

```
cheap food -> hunger -> commissary food sales (profit) -> broke residents -> debt and the black market
   -> fights and contraband -> unrest, audit findings and grievances -> fines, a lost contract or a riot
```

Each lever can be tuned for profit or for stability. No single setting wins: the right answer changes with population pressure, the tier's audit standard and how much informants are telling you.

## 2. Money map

There are two separate, reconciled books.

- **Company ledger.** This is the existing `Book()` ledger.
- **Resident trust fund.** It is new. The facility holds this money for residents and cannot spend it. Its sum must always equal trust inflows minus trust outflows (`TrustReconciles()`, checked like `Reconciles()`).

Black-market trades move goods (soups and items) between residents and never create cash.

| Flow | From | To | Base amount (× location wage index) | Lever |
| --- | --- | --- | --- | --- |
| Inmate wages | Company | Trust | $2–$12 a day by job (below) | Wage band per job, inside the tier's legal range |
| Family deposits | Outside | Trust | $0–$60 a game week, from the resident's support level | Visits and calls home raise it |
| Deposit fee | Trust | Company | $1.95 plus 0–8% | Fee setting; above 5% draws grievances and press |
| Commissary sale | Trust | Company | Retail = wholesale × (1 + markup) | Markup 0–100% |
| Commissary stock | Company | Supplier | Wholesale × weekly price index | Buy now or wait |
| Phone minutes | Trust | Company commission and provider | $0.10–$0.30 a minute | Rate; above $0.21 a minute is an audit finding in state and federal tiers |
| Lawyer retainer | Trust | Outside | $250–$2,500 by charge severity, in instalments | Resident's choice |
| Court fees and restitution | Trust | Outside | 0–30% of wages | Court order; the company can add a "cost of care" deduction |
| Gate money | Company | Released resident | $25–$100, set by tier statute | Fixed |
| Food purchases | Company | Supplier | $0.90–$4.00 a meal by quality | Quality and menu |
| Kitchen staff | Company | Staff | Food service director $380 a day; cook $300 a day | Hiring |

These amounts fit today's numbers. A county intake contract earns $4,100 a day for 12 residents and an officer costs $400 a day. An average supported resident spends about $5 a day at a 35% markup, which earns the company about $1.40 a day per resident. That matches the current flat commissary income of $1.50, so the real store replaces the flat amount without changing balance.

The economy is time-compressed like sentences are, since a 60-day game sentence stands for years. Wages are game-scaled so that saving for a lawyer takes two to five game weeks of work plus some family help, inside a typical pretrial stay.

**Food is carved out of operations.** Today each contract's `DailyOperatingCents` silently includes food. The plan moves the "Standard quality" food budget out of operations and into explicit purchases. At Standard quality the company's cash flow is unchanged, so existing balance holds. Cheaper or better food then moves money either way.

## 3. Kitchen and food

### Building

The kitchen upgrades in the existing roadmap order: serving counter, then kitchen line, then industrial kitchen. It is a room with real fixtures:

- **Already exists:** serving counter.
- **New fixtures (Codex catalog request):** range, prep table, walk-in cooler or pantry, dish station.

Kitchen capacity sets **hot meals per meal window** (each window is two hours):

| Kitchen | Hot meals per window | Note |
| --- | --- | --- |
| No kitchen | 0 | Meals come from an outside caterer as today's $4 trays |
| Serving counter only | 0 | Serves food cooked off site |
| Kitchen line | 40 | |
| Industrial kitchen | 120 | Scales with cooks and inmate workers |

People beyond capacity get a cold bag meal, which costs less and lowers morale. This plugs into the existing meal shifts. The meal plan already gives each person a place and a shift, and the kitchen decides what lands on the tray.

### Staff

- **Food service director.** A civilian, required for a kitchen line or larger. Without one, sanitation decays.
- **Cooks.** Civilians, about $300 a day. Each supervises up to 6 inmate workers.
- **Inmate kitchen workers.** Prep, line server and dishwasher jobs, paid $3–$6 a day. Workers raise throughput but bring risks:
  - **Food theft:** a stock leak that turns into black-market food.
  - **Hooch:** the existing hooch hook gets fruit and sugar as inputs.
  - **Knives:** become weapons without tool control. A "shadow board" upgrade checks tools in and out.
  - **Favoritism:** extra portions for friends add tension with everyone else.
- New staff kinds go in `Personnel.cs` (Claude's): `Cook` and `FoodDirector`. Their role ids need `Operations.ValidRole` (Codex's), or they map onto an existing role the way nurses map to caseworkers.

### Food quality

| Quality | Cost per meal | Food gained per minute at the table | Stress per meal | Morale note |
| --- | --- | --- | --- | --- |
| Budget (commodity) | $0.90 | 0.45 | +2 | Hunger by evening; commissary food sales +40% |
| Standard | $1.60 | 0.60 (today's value) | 0 | Baseline |
| Good (fresh) | $2.60 | 0.65 | −2 | Belonging +1; fresh produce raises hooch risk |
| Premium | $4.00 | 0.70 | −4 | Inspectors notice; commissary food sales −30% |

### Meal types

Each menu item has cost, nutrition, morale, prep minutes, shelf life and contraband risk.

- **Breakfast staples:** oatmeal, grits, eggs. Cheap and fast.
- **Hot entrées:** chicken and rice, chili mac, fish, beans. The core of the menu.
- **Cold bag lunch:** a sandwich and chips. No seat needed, so it eases meal shifts and works during lockdown. Low morale.
- **Fresh produce:** fruit and salad. Nutrition and morale, but fruit and sugar feed hooch.
- **Special diets:** medical, halal, kosher and vegetarian, flagged at intake. A missed diet is a grievance and an audit finding, and repeated misses turn into a lawsuit.
- **Holiday and event meals:** a Thanksgiving tray or a cook-out day. A one-day morale spike that costs extra.
- **Disciplinary loaf:** a nutritionally complete punishment meal. Cheap and legal in some tiers, with legal risk, press and an audit finding in state and federal tiers.

### Menu and stock

- **Weekly rotation.** 7 days × 3 meals, from presets: Budget, Standard, Dietitian approved, or Custom. A dietitian upgrade unlocks "Dietitian approved", which audits score well.
- **Stock and spoilage.** Stock arrives by delivery truck. Deliveries are already a contraband route, so food and contraband share the dock. Perishables spoil in the cooler.
- **Running short** means bag meals, then outside trays. This is the same `ServeTrays` path that exists today.

### Events

Each event has a cause the player can see:

- **Food poisoning:** low sanitation plus Budget food. Medical load and an audit finding.
- **Grease fire:** the existing `SmallFire`.
- **Cooler failure:** spoiled stock.
- **Kitchen strike:** inmate workers refuse to work over wages or treatment.
- **Supplier price shock:** see the commissary below.

### Decisions by role

- **Company.** Quality, menu, kitchen tier, staffing, worker wages.
- **Warden.** Who gets kitchen jobs, whether to use loaf as discipline, and holiday meals.
- **Officer.** Pat-search kitchen workers at shift end. This catches theft and costs worker goodwill.
- **Prisoner.**
  - Apply for a kitchen job.
  - Work the line honestly or skim food to sell.
  - Brew hooch with stolen fruit.
  - Give friends extra portions.
- **Cook (worker role).** Cook fast or cook well, report or ignore theft, and take or refuse a supplier's bribe to bring in a package.

## 4. Commissary with price fluctuations

### Items

These are base wholesale prices. Retail price is wholesale × (1 + markup).

- **Soups (ramen):** $0.35. Also the black-market currency.
- **Coffee:** $4.50. **Snacks:** $1.00.
- **Hygiene kit:** $2.50. **Stamps and writing kit:** $0.70 and $2.00.
- **Shoes:** $40. **Radio:** $25.
- **Tablet music:** $1 a song. Requires the phone or tablet upgrade.

### Price index

- **Weekly movement.** Each category has a weekly wholesale index. It is a deterministic, mean-reverting random walk of ±0–8%, keyed `Hash(seed, "price", category, week)`.
- **Shocks** come from events: "coffee up 22% this week", "supplier strike: snacks out of stock", "holiday season".
- **HUD.** The Pressure or Economy pane shows a small sparkline per category and next week's forecast band.
- **Bulk buying.** Storage racks (new fixture) let the company buy ahead when prices are low. That gives the store a light trading game and gives the building a reason for a stockroom. Stock without a stockroom is bought daily at the day's price.

### Demand

Each resident decides once a day. Demand depends on:

- the resident's trust balance;
- retail price elasticity;
- hunger, which is stronger after Budget meals;
- black-market substitutes, since tobacco competes with contraband;
- withdrawal (the existing `Dependency`);
- gang tax: gangs take a share of members' store goods.

### Markup consequences

- **0–35%:** no consequence.
- **35–60%:** grievances.
- **60–100%:** a "price gouging" story. The existing family protest incident and a reputation hit follow, and residents shift spending to the black market.

There is no runaway profit. Elasticity and substitution cap revenue, and above about 60% markup total profit falls.

### The store man

A resident hustle and a real prison institution. The store man lends store goods at 2-for-1 interest. Debts that come due unpaid turn into extortion, then fights or a protective-custody request. Debt is the engine behind many fights that look random today.

### Decisions by role

- **Company.** Markup, bulk buys, stockroom size, product list (tablets earn more but phones compete with contraband phones).
- **Warden.** Commissary restriction as a sanction, and store-day schedule by unit.
- **Prisoner.** Buy, save, lend as a store man, run a tab, or pay off a debt.

## 5. Inmate jobs and wages

### Jobs and pay

Pay is game-scaled, per day of work.

| Job | Pay a day | Notes |
| --- | --- | --- |
| Orderly or janitor | $2 | Sees everything: high information value |
| Laundry | $3 | Laundry carts are a hiding route |
| Kitchen: prep, line, dish | $3–$6 | Section 3 |
| Library clerk or tutor | $4 | Raises others' program progress |
| Barber or tattoo (licensed in minimum security) | $4 | Unlicensed tattoos are a hustle and a health risk |
| Maintenance helper | $5 | Tool contraband risk; helps technicians |
| Commissary clerk | $4 | Skims goods |
| Industries: furniture, signs | $8–$12 | The company sells the output as revenue. Federal tier only, or an upgrade |
| Trustee: grounds, outside work | $6 | Minimum security only. Escape and smuggling risk |

### Rules

- **Slots.** Job slots come from installed fixtures: kitchen stations, laundry machines, library shelves.
- **Eligibility.** Classification (from intake), no serious write-up in 7 days, and a caseworker's approval.
- **Payoff for the resident.** Work raises Belonging, lowers Stress, and adds to the good-time chance at court. That second effect uses the existing `Resolve` good-time roll.
- **Cost of a job.** Time away from the cell, plus each job's specific risk.
- **Deductions from pay.** Court-ordered restitution and fees take 0–30%, and the company can add a "cost of care" deduction. A deduction recovers cost but lowers work effort and legitimacy, and is an audit finding in rehabilitative modes.

## 6. Counsel: public defenders and paid lawyers

Court odds today live in `Justice.Resolve` (Codex's file): the release, sentence and continuance rolls and the term length. Counsel changes those odds through a hook Codex would add:

```csharp
partial void CounselOdds(Resident resident, ref float release, ref float sentence, ref int termDays, ref bool continuance);
```

### Public defender

The default, free to the resident.

- **Caseload.** The county's public defender office has a caseload that grows with the transfer backlog from `Population`. When the system is overloaded, public defenders are overloaded too. The result is more continuances, longer pretrial stays and more crowding.
- **Why it matters.** This links legal quality to the pressure the player is already fighting. Video court (planned) and a "public defender office funding" upgrade or event push back.

### Paid attorney

Three tiers, each with a retainer scaled by charge severity:

| Tier | Retainer | Release chance | Sentence term | Continuances | Motions |
| --- | --- | --- | --- | --- | --- |
| Storefront practice | $250–$600 | +5% | −5% | | One per case |
| Experienced defense | $700–$1,500 | +10% | −12% | Fewer | Bail reduction, suppression |
| Big firm | $1,500–$2,500 | +16% | −20% | Rare | All, including discovery |

- **Paying.** A retainer can be paid in instalments: 25% up front, then weekly. A lawyer drops the case if payments lapse.
- **Funding.** Money comes from trust balances and family. Gang funds can pay too, which puts the resident in the gang's debt.
- **Bail reduction motion.** A pretrial resident can be released on bond, using the existing `Release` path.
- **Suppression motion.** Contraband charges from a search without cause are thrown out. Searches backed by a CI tip are vulnerable unless the tip was corroborated (section 7).
- **Discovery motion.** It can force disclosure of a confidential informant behind a charge. A big-firm lawyer is far more likely to expose informants than an overloaded public defender. So poor defendants are convicted on informant testimony while wealthy ones expose the informants.

### Plea offers

A plea offer is a deterministic roll before a hearing: a known shorter term in exchange for giving up trial. Refusing and losing carries a trial penalty of up to +30% term. A **cooperation plea** cuts the term further and lowers the release threshold, but writes a cooperation clause into the resident's paperwork (section 7).

### Legal visits

Legal visits need a private attorney room, which is a building requirement. Video legal visits come with the video-court upgrade. A crooked lawyer is a rare contraband route. Attorney-client meetings cannot be monitored; monitoring them is an audit finding and a lawsuit.

### Self-help

- **Law library.** Residents file their own motions and grievances. Successful grievances cost the company, but a legitimate outlet lowers unrest.
- **Jailhouse lawyer.** A resident with high Discipline and education helps others for store goods. Their help raises a case's odds a little, and they become a target if they help the wrong people.

### The company's own legal costs

Lawsuits come from conditions, injuries, missed diets and abuse findings. A company "Legal department" upgrade lowers settlements. Lawsuits become a new category of audit finding.

### Decisions by role

- **Prisoner.** Stay with the public defender or save for a lawyer; take the plea, cooperate, or go to trial; file grievances.
- **Warden.** Good-time recommendations (exists), whether to support a cooperation deal, and legal-visit access.
- **Company.** The legal department, law library and attorney room.

## 7. Informants and snitches (the antagonistic system)

Information is a resource. The administration wants it and the population punishes anyone who sells it. Some informants play the administration for their own ends.

### 7.1 Knowledge

Every illicit plan creates a piece of **intel**. Plans include a stash, a planned drone drop, an ordered assault ("green light"), staff smuggling, an escape plan, a debt collection and a hooch batch.

A piece of intel is known to the people involved and to those near them:

- cellmates;
- clique and gang members;
- friends through the existing trust relationships;
- whoever stood nearby when it was arranged.

Intel spreads a little each day along trust links and expires when the plan resolves. It is stored as a bounded list:

```
Intel { Kind, SubjectId, Place, Truth, CreatedMinute, ExpiresMinute, Knowers }
```

**Truth** matters. Some intel is false: rumors, a rival's invention, or a double agent's plant.

### 7.2 How information reaches the administration

| Channel | Volume | Reliability | Who runs it |
| --- | --- | --- | --- |
| Staff observation | Low | High | Existing CCTV, rounds, searches |
| Anonymous notes ("kites") | Medium | Mixed: grudges invent | Any resident with a motive: fear, revenge, or wanting a favor |
| Tip line kiosk (upgrade) | High | Low | Anonymous; more noise |
| Confidential informants | Steady | Hidden; learned per informant | The warden or a new **investigator** (a special investigations officer, in Personnel) |
| Cell plants and jailhouse informants | Rare | Low; scandal risk | Prosecutors, with the warden's cooperation |

### 7.3 Confidential informants

**Recruiting.** The investigator approaches a resident. Leverage raises the odds:

- unpaid debts;
- pending charges, which allow a cooperation deal;
- withdrawal;
- fear for safety;
- wanting a transfer or a single cell.

Personality matters too: low Belonging, weak gang loyalty, and a history of conflict with their own clique. A refused approach can itself leak.

**Paying.** Each reward carries a visibility score:

| Reward | Cost | Visibility |
| --- | --- | --- |
| Store credit | $5–$20 a week | Low |
| Phone time | Low | Low |
| Better job | A job slot | Medium |
| Single cell | Housing | High |
| Move near family | Transfer | High |
| Letter to the court (cooperation) | Credibility | High once paperwork leaks |

Residents notice sudden favors. Visible rewards raise suspicion.

**Tips.** Production depends on the informant's network: who they are close to, and their clique and gang centrality. Tip types include:

- a stash in a named cell, which raises `SearchCell` odds;
- a drone drop tonight at a stretch of fence, so an officer can be positioned to intercept;
- an ordered assault on a resident, who can then be separated using the existing `SeparateAfterFight` tools;
- an officer bringing in phones, which feeds the planned internal affairs unit;
- an escape plan, which lowers the existing escape attempt's odds.

**Reliability** is hidden and learned from outcomes: how many tips checked out out of how many were acted on. Bad tips cause unjust searches, which cost legitimacy, unrest and grievances.

**Double agents.** A gang lieutenant turned informant is the most valuable source and the most dangerous. They can feed a planted tip that pulls officers to the east fence while the real drone drop lands at the west yard, or that triggers a unit-wide shakedown while an escape plan runs elsewhere. This is the antagonist the user asked for: an informant who uses the administration.

**Cover.** Acting on a tip immediately is most likely to succeed and most likely to expose the source. The player has ways to protect a source:

- wait;
- search the whole unit, which hides the source but costs unrest and staff hours;
- corroborate with CCTV or a second source;
- schedule meetings under cover: a mass call-out, a medical appointment, or a legal visit.

### 7.4 Suspicion, jackets and protective custody

Each resident has **suspicion**, from 0 to 100, held by the population. It rises when the resident:

- is seen alone with staff without cover;
- receives a visible favor;
- knew about a plan that was then busted;
- is the target of a rumor;
- has paperwork that leaks.

Suspicion decays when nothing happens.

Above a threshold, or on confirmation, the resident carries a **snitch jacket** (prison slang for a snitch reputation). Confirmation comes from a corrupt officer selling names (low `Integrity`, high `Pressure`), court discovery by a paid lawyer, or a records leak. Jacketed residents face:

- harassment: Safety falls and Stress rises;
- extortion;
- food tampering, when a kitchen worker belongs to the rival gang;
- an assault order from their own or a rival gang.

**False jackets.** A manipulative resident can start a rumor to jacket a rival. Rumors from high-status residents spread further. Innocent people get hurt, and the player can only tell the difference through the informant's real record.

**Snitch hunts.** After a big seizure, the gang that lost the goods looks for the leak among everyone who knew. Big busts therefore have a social cost the player can foresee.

**Paperwork checks.** Gangs ask arrivals to show their court papers. A cooperation clause written by a plea deal (section 6) is found unless records are secured. Records security can come from an upgrade or from intake staff with high Integrity.

**Protective custody.** A jacketed or threatened resident can request a protective custody unit. Protective custody needs its own housing and its own yard and meal movements, separate from general population. It reuses the keep-separate logic and `SeparatedUntil`. Overcrowded protective custody is a real building problem, so this creates construction demand, which suits a building game.

**The well runs dry.** When informants get hurt, the population learns that the administration cannot protect its sources. Recruitment odds and spontaneous notes fall for weeks. Protecting informants keeps the intel coming.

### 7.5 Jailhouse snitches in court

A pretrial resident can be offered cooperation in exchange for testimony that a cellmate confessed. If they accept:

- their own odds improve through the counsel hook;
- the target's conviction odds rise.

If the testimony was false, there is a deterministic chance of a scandal weeks later: a wrongful conviction, a lawsuit, a reputation hit and an audit finding. The warden decides whether to place an informant in a target's cell. It is legal, realistic and corrosive.

### 7.6 Decisions by role

- **Warden or investigator.**
  - Recruit, reward and drop informants.
  - Act now, act with cover, or corroborate first.
  - Move a burned source to protective custody, transfer them, or leave them in place.
  - Approve cell plants.
- **Officer.**
  - Hold informant meetings discreetly.
  - Sell names (the corruption path: easy money, an internal affairs risk).
  - Treat jacketed residents fairly.
- **Prisoner.**
  - Accept or refuse an informant approach.
  - Snitch on a rival for revenge.
  - Start or deny a rumor.
  - Check a newcomer's paperwork, as a gang member.
  - Confront a known snitch. This is the user's "slide on the snitches". It is a fight action using the same fight roll the AI uses. The resident gains gang standing and risks a write-up, separation, added charges and retaliation.
  - Ask for protective custody.
- **Company.** Fund the investigator post, the tip line, records security and protective custody housing.

### 7.7 Metrics and audit links

- **Pane metrics:** intel quality, active informants, burned informants, false-tip rate, protective custody occupancy and snitch-related assaults.
- **Audit findings:** a protective custody resident hurt, unjust searches, and monitored legal calls.

## 8. Playable roles: from riding along to taking the role

The plan for direct control (see `docs/possession-plan.md`) still holds: full walking control needs Codex's movement hooks, and prisoner walking control needs door and zone access rules.

**The step in between is decision control.** When the player takes someone's role, the simulation still walks that person, but every decision point is put to the player as a card. The card uses the same core method, duration and roll the AI uses, with a timeout default so time never stalls.

- **Why this step first.** It delivers "make decisions as a prisoner or a guard" without any of the movement exploits the critic flagged. It also builds directly on the ride-along camera and person cards.
- **How it looks.** It extends the existing incident cards in `FacilityEvents` and a new role card in the HUD.

| Role | Goal and score | Decision cards (all existing or planned core methods) |
| --- | --- | --- |
| **Prisoner** | Release date earned back, savings at release, standing, no new charges | Apply for a job; work honestly or skim; buy, save or lend; call home; take a lawyer, plea, cooperation or trial; join or avoid a gang; run a hustle (store man, tattoos, hooch, gambling); snitch or stay solid; confront a snitch; protective custody; programs; file a grievance |
| **Officer** | Shift completed, incidents prevented, fair-treatment rating, integrity | Patrol choice; pat search or let it go; write up or warn; respond to a fight; accept or refuse a bribe; hold or sell an informant meeting; overtime |
| **Warden** | Audits passed, unrest, budget kept, informants protected | Lockdown; searches; discipline hearings; job assignments; protective custody placement; good-time recommendations; informant program; grievance responses; holiday meals |
| **Cook or kitchen worker** | Meals on time, quality, stock kept | Cook fast or well; report theft; extra portions; take a supplier package |
| **Builder or crew member** | Work done safely | Work order; safe or fast; leave a tool for pay (contractors are a smuggling route); report a contraband request |
| **Owner (today's view)** | Company value, contract standing | Everything financial: prices, quality, wages, upgrades, building |

**Role conflict is built in.** When the player plays the warden, the owner becomes an AI that sends demands, such as "cut food costs 10% this month". When the player plays a prisoner, the company still makes its pricing and food choices. The player feels the levers from the other side, which also teaches the management game.

**Consequences are real.** It is the player's facility. A prisoner who smuggles still hurts the company's audit, which turns roles into a choice rather than a toy. An optional "role sandbox" with no company consequences can come later.

## 9. More ideas that tie the game together

1. **Grievances.** Formal complaints about food, prices, diets, searches and staff. The warden answers them. Unanswered ones become lawsuits and audit findings. This makes every lever above visible as named complaints.
2. **Legitimacy.** A population-wide measure of fair treatment. It rises with consistent discipline, answered grievances and fair prices. It falls with unjust searches, loaf punishment and gouging. It scales unrest, informant supply and cooperation with officers. This is the strongest real-world predictor of prison order.
3. **Gang markets.** Gangs control black-market lines (phones in one unit, drugs in another), tax their members' store goods and fight over market share. Seizures shift gang power. This ties together contraband, snitch hunts and the existing fight logic.
4. **Visits.** Physical visitation (pending from Codex) raises Belonging and deposits, and is a contraband route. Video visits earn revenue but cost morale.
5. **Reentry and return.** Release with savings, job training and family ties lowers the chance of coming back. Some former residents (already kept in `JusticeState.Former`) return later as transfers, so people the player knows reappear. A state "pay for success" contract bonus for low reoffending makes rehabilitation pay.
6. **Morning briefing.** One card at 07:00 with intel, debts coming due, price movements, kitchen stock and who is at risk. It is a natural home for the new systems' notices instead of spamming the log.
7. **Economic events.** "Public defender office loses funding" (more continuances), "state budget cut: per diem −5%", "heat wave: cooler at risk", "coffee shortage".

## 10. Build order and ownership

Each phase ships playable, with checks, a deterministic demo and a Unity check request for Codex.

| Phase | Contents | Files (owner) | Needs from Codex |
| --- | --- | --- | --- |
| **A. Economy foundation** | Trust fund with its own reconciled book, family deposits and fees, jobs and wages from fixtures, commissary store with the weekly price index and markup, the store man and debts, gate money. Replaces the flat commissary income | New `Core/Economy.cs`, `Runtime/GameEconomyHud.cs`, `Resources/Economy.uss`, `EconomyChecks.cs` (Claude); small edits to `Upgrades.cs` and `Population.cs` (Claude) | Nothing to start; storage rack fixture later |
| **B. Kitchen and food** | Kitchen tiers, stock and spoilage, quality, menus and diets, civilian kitchen staff, kitchen jobs, hot-meal capacity feeding meal shifts, theft, hooch, knives and poisoning | `Core/Kitchen.cs` (Claude); `Simulation.cs` meal gain and `Personnel.cs` roles (Claude) | Range, prep table, cooler and dish fixtures with art; possibly `ValidRole` |
| **C. Informants and snitches** | Intel, notes, the tip line, informants and their reliability, cover, suspicion and jackets, false jackets, snitch hunts, protective custody, the well running dry | `Core/Informants.cs` (Claude); hooks in `Contraband.cs` and `Personnel.cs` (Claude) | A protective custody housing flag on rooms if zoning lands; `FacilityEvents` grievance cards (optional) |
| **D. Counsel** | Public defender caseload from the backlog, paid tiers and instalments, motions, plea and cooperation offers, attorney room, law library, company legal costs | `Core/Counsel.cs` (Claude) | The `CounselOdds` hook in `Justice.Resolve`, a cooperation flag, the attorney room fixture |
| **E. Take the role** | Role cards on the ride-along for prisoner, officer, warden and cook; role goals and score; the owner AI's demands while the player plays the warden | `Runtime/GameRoleHud.cs`, `Core/Roles.cs` (Claude), building on `GameRideAlong.cs` | None for decision control; movement hooks later (`possession-plan.md`) |

**Recommended order:** A, then C, then B, then D, then E.

- **A first.** Money is the base every other system needs.
- **C next.** It is the deepest gameplay and sits almost entirely in Claude's files, on top of contraband that already exists.
- **B third.** It needs Codex fixtures for full value, but it can start on the serving counter.
- **D fourth.** It waits on one Justice hook.
- **E grows alongside.** Each phase adds that phase's decision cards to the role mode.

### Checks every phase must keep

Check style follows the existing mutation-tested suites.

- **Money conservation.** `Reconciles()` and `TrustReconciles()` hold after every minute in fuzz runs with frequent saves, for 30 seeds × 14 days.
- **No free money.** Rewards paid to informants, wages and refunds are always booked from company cash. A save and reload never pays twice.
- **No runaway profit.** For every tier, total commissary profit across markups 0–100% peaks below 70%.
- **Deterministic.** Every roll uses `Hash(seed, ...)` keyed by a stable count, so reloading cannot reroll a court date, an informant's tip or a price.
- **Bounded saves.** Intel, debts and price history have hard caps.
- **Balance.** The smart builder from the critic's harness still survives 9/9 runs at 21 and 35 days with each phase on. Idle play still collapses.
