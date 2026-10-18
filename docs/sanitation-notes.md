# Sanitation: hygiene, showers, toilets and illness

Claude's `sanitation` scope (2026-10-18). Before this, toilets, basins and showers (and the Hygiene unit prefab) were in the catalog but changed nothing. Now they decide how clean people are, how often they get ill and what inspectors write down.

## How it works

**Hygiene** is a saved 0–100 score per resident (`Resident.Hygiene`, old saves start at 80).

- It falls 0.018 a minute awake and 0.008 asleep: about 21 points a day without washing.
- Yard time adds a little sweat (+0.008 a minute). A floor mattress adds 0.006.
- With no toilet within reach of the bed it falls 35% faster. A crowded shared toilet (more than 8 sharing) adds 2% per extra person, up to 35%.
- Contract laundry (new upgrade, Services) takes 18% off every loss.
- Below 45 it adds stress. Below 30 it costs belonging. Below 25 it strains cellmates (+0.6 tension every half hour on existing relationships) and inspectors count the person as unable to wash.

**Washing** happens only inside each resident's own yard window (morning 10–12 or afternoon 15–17, shifted by their routine offset), where free-time planning is already off. Meal travel always wins, so routines, activities and `ChooseResidentActivity` are untouched.

- **The wash plan** (`World.WashAssignmentOf`) gives each resident a station and a session. A session is one yard window on one day group, and holds five people. Every working shower has two sessions a day (daily) or four over two days (alternate days). Basins take the overflow when every shower session is full. Someone with nothing within reach is *unplaced*.
- **Cost of a station** = walking distance (real route from the housing room) + 3 m for each person already in that line. People in the first lunch shift lean to the afternoon. A station further than a 75-minute walk is never assigned. Stations must be working (condition above 25) with a clear, reachable spot in front.
- **The queue.** First come, first served while a station is free. Behind the washer, the others wait in line in front of it, in assignment order. A changed target re-routes at once, so the line moves up as soon as the station frees.
- **The wash itself.** Eight minutes at a shower takes hygiene to 100 with soap, 82 without. A basin wash tops out at 62 (50 without soap). Four minutes counts if the person is called away. Each washing minute wears the station 0.06 condition. Orderlies mend wear; a contract plumber restores every worn hygiene fixture for $8 a condition point.
- **Soap.** Issued supplies (default on, 45¢ a present resident a day × wage index) or a commissary hygiene kit (a week per purchase). Turning supplies off saves money and leaves people without kits washing poorly.
- **Missed washes** are tallied at midnight for everyone whose wash day passed unwashed (alternate-day groups only on their day; new arrivals and the company's first partial day are skipped).

**Toilets and basins within reach** (`World.AccessOf`): the nearest working toilet and basin whose walking route from the bed is within 14 m. A toilet in the same room is "In unit"; otherwise "Shared". This drives hygiene loss, stress (no toilet: +0.005 a minute by day, +0.003 by night) and illness.

**Illness** (`Stomach bug`: 2 days, contagious for 1; `Skin infection`: 4 days, contagious for 2):

- Once an hour each healthy present resident may fall ill. The base chance is 0.04%, multiplied by dirt (up to ×3.5 at zero hygiene), missing or crowded toilets (×1.6 / ×1.25) and no basin within reach (×1.3).
- Each contagious person in the same room or housing room adds 0.6% × (1 − hygiene/140), × 0.75 with a basin. A filthy dorm passes it on; a clean one with basins lets it fizzle.
- While ill: health falls (0.012 or 0.008 a minute), a stomach bug also takes food, and natural healing stops.
- **Treatment.** The nurse's sick call now takes serious injuries first, then anyone contagious. A visit ends contagion within the hour and shortens the illness. Twelve hours under 25 health sends the person to hospital as a welfare violation, with illness-specific wording.
- **Quarantine** (default on) keeps the contagious in their housing through the existing "kept in housing" restriction (`SeparatedUntil`), so routines, jobs, fights and meals all honour it. Trays feed them. Lifting it ends only the stay quarantine added; a fight separation keeps its own time.
- **Outbreak:** three sick at once, or 12% of those present. It records an event and a notice, and brings the next inspection forward to within a day ("Outbreak report"). It ends when nobody is sick.

**Inspections** (`SanitationFindings`), capped so complete neglect costs about 22 points (32 with outbreaks), comparable to crowding or staffing:

| Finding | Points |
| --- | --- |
| Residents unable to wash (hygiene < 25) | up to −6 (½ each) |
| Missed washes per resident-day | up to −4 |
| No toilet within reach | up to −6 (⅓ each) |
| No working shower | −6 |
| Shower ratio over the tier standard (city 16, county 14, state 12, federal 10) | up to −4 |
| Outbreaks since the last inspection | −5 each, up to −10 |
| Credit: clean housing and ample showers | +3 |

**Unrest:** "Residents who cannot wash" (share × 10) and "Illness in housing" (share × 20, up to 8).

## Where to look in game

- **Pressure workspace → SANITATION:**
  - the outbreak card;
  - working showers, basins and toilets, wash places against residents, and how many are beyond walking time;
  - today's washes, yesterday's misses, average hygiene, illness;
  - the five largest housing rooms with their fixtures;
  - Daily / Alternate days, Issue supplies, Quarantine the sick, Plumber.
- **Banner:** "OUTBREAK" (below riots) or "NO SHOWERS" (lowest priority). The banner now sits beside the command meters instead of under them.
- **Resident's full file (Management options):** a HYGIENE AND HEALTH block with the meter, their wash status ("Shower · afternoon yard · 17 min walk · second in line"), toilet and basin access and illness.
- **In the world:** running water at a shower or basin while someone washes there, a wet tray for 20 minutes after, and a yellow wet-floor sign at any out-of-service fixture.
- **Demo** (editor/debug builds): "Demo · outbreak" in the Sanitation section.

## Evidence

- **Core checks:** 22 sanitation checks. They cover:
  - migration and invalid saves;
  - pinned decay rates and the laundry ratio;
  - plan capacity, balance, basin spill and walking limits;
  - alternate days;
  - washing only in yard windows, never in meal travel or free time;
  - one-at-a-time queues with exact wear;
  - soap caps, the supplies ledger and midnight tallies;
  - a mid-wash reload;
  - toilet kinds and stress;
  - illness spread, course and treatment;
  - quarantine versus fight separation;
  - the hospital transfer, outbreak start and end, and pinned audit points;
  - the plumber;
  - unrest and hygiene kits.
- **Unity** (`Editor/SanitationCapture.cs`, Claude's clone, batchmode with graphics): a county-maximum company builds two showers through a real quote and crews (the vehicle gate must be opened first). Residents wash in the yard window, then an outbreak is staged. Five frames: the running shower, the queue, the Pressure Sanitation section, the compact profile and the full file's hygiene block. The HUD is rendered into a texture (batchmode reports a 640×480 screen). No runtime errors; `CELLBLOCK_SANITATION_CAPTURE_PASSED`.
- **Balance** (21 days, seed 101):
  - A builder that never fits hygiene fails audits on sanitation.
  - A builder that fits a toilet per six, a basin per eight and a shower per eight inside each housing room keeps average hygiene around 60 and gets 10–15 showers a day per few stations. Sanitation drops out of the top findings (the remaining county failure was contraband, records and welfare).

## Requests for Codex (also in the user's message)

1. **Starter layouts** (`ModeStarts.cs`) need working hygiene from day one: a toilet and basin in every secure or single cell, an alcove with two showers, two toilets and two basins per dorm, and a small shower and toilet room by the shared starter's bedrooms. Without showers a new company slides to filthy in three days.
2. **Washing poses and props** in the routine view:
   - Shower: step into the tray (like the bed `SeatOffset`), face the head, scrub. Basin: lean over, hands to face. Waiting in line: arms folded. A towel to and from the shower.
   - Queries: `World.Washing(r)`, `World.WashDue(r)`, `World.WashAssignmentOf(r)` (`Kind`, `Place`, `FixtureId`).
3. **Compact dossier:** a Hygiene vital or line from `r.Hygiene`, `HygieneBand`, `WashStatus`, `IllnessStatus` and `ToiletStatus`.

## Known limits and follow-ups

- Residents stand in front of the shower while the water runs inside it until the step-in pose exists.
- The planner measures walks from each housing room's first bed, so the displayed walk is approximate for large rooms.
- Furniture can still be placed so it boxes in a doorway or another station's access spot; a "keep doorways clear" placement rule is a build-tool follow-up (Claude).
- No on-site laundry yet (needs washer and dryer fixtures); no bladder visits to toilets (access is evaluated from the bed).
