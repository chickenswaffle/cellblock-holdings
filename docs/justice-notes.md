# Justice and custody slice — notes, evidence, limits

Owner: Claude (`justice` scope). Branch `codex/claude-justice-custody`, based on `codex/live-operations-handoff` at `950455e`.

Everything here is **fictional game behavior**. Charges, court calendars, outcomes, sentence arithmetic and custody procedures are simplified rules for play. They do not describe or model real legal procedure, and escape attempts are abstract (a route to the vehicle gate, not methods).

## Files

| File | Role |
| --- | --- |
| `Assets/Cellblock/Core/Justice.cs` | Serialized legal data (`LegalProfile` on `Resident`, `JusticeState` on `State`), `JusticeRules` (generation, migration, validation), and the `World` partial: calendar, court van, custody movement, escape attempts, presentation queries, demo hooks |
| `Assets/Cellblock/Runtime/FacilityJusticeView.cs` | Court van model and motion, sally-port bay markings, hidden absent residents, detected-escape marker |
| `Assets/Cellblock/Runtime/GameJusticeHud.cs` | Court & Custody panel at the top of People, compact legal card in the resident profile, roster tags, demo buttons (editor/dev builds only) |
| `Assets/Cellblock/Resources/Justice.uss` | Styles for the above; `Cellblock.uss` is untouched |
| `Tools/CoreChecks/JusticeChecks.cs` | 19 executable checks plus one interface probe |

No existing file was edited. All integration uses the hooks Codex provided: `HydrateJustice`, `AdvanceJustice`, `UpdateResidentCustody`, `ResidentPresence`, `ResidentVisibility`, `RefreshJustice` (view and HUD), `BuildJustice`, `RunJusticeChecks`, plus private `Walk`, `Routes`, `SightLines`, `NearbyClear`, `Book`, `IsOnShift`, `CameraSees`.

## Model

**Legal profile** (per resident, persisted): status `Pretrial` / `Sentenced` / `Released`; charges with outcome `Alleged` / `Sentenced` / `Resolved` (pretrial charges are always shown as allegations); transfer minute and prior custody days; next hearing; sentence length and release minute; hearing, missed-hearing and attempt counts; close supervision; a bounded event history (10). Profiles are generated once from the company seed and the resident's stable id, so the same company always produces the same people. The contract shapes the mix: County Intake is mostly pretrial, Reentry Campus mostly sentenced with near release dates, Modernization more serious allegations. A new company always has at least one hearing on day 2.

**Custody states**: `In facility`, `Escorted`, `Boarding`, `Aboard`, `At court`, `Release processing`, `Released`, `Escaping`, `At large`, `Awaiting return`. Present on site: `In facility`, `Escorted`, `Boarding`, `Release processing` and `Escaping`; the rest are absent. While away (aboard, at court, at large, awaiting return), a resident's `Position` is parked at their own `Home`, which the existing route checks already protect, so an absence never adds a new protected point. They are hidden and skipped by routines. On return they step down at the sally port, or at home if the sally port has no route home. **Released people leave `State.Residents`**: they move to `JusticeState.Former` (the most recent 40), their relationships are removed from others, and stale home moves naming them are dropped. Their bed, meal slot, route constraints and staff workload no longer count. `Resident.Position` never leaves the validated site bounds; the van's road coordinates live only in `CourtRun.Position`.

**Court day** (regular): the van leaves the court base at 07:15 when anyone has a hearing that day, drives the public road, waits at the vehicle gate if it is closed, parks at the sally port (−2, −14.8), boards scheduled residents until 08:15 (they walk to the boarding point at 0.45 × walk speed). A van delayed at the gate still gets a boarding window, max(10 min, min(30 min, hearing − 25 min)), drives to court (hearings 09:30, resolved 10:30), returns at 11:00, unloads, and residents rejoin routines in time for lunch. Outcomes are deterministic per resident and hearing: released at court (does not return; bed freed), sentenced (credit for time served, minimum 10 days remaining), or continued (3–7 days). A sentenced resident with a new allegation is either cleared or given added days.

**Failure modes the player can cause or prevent**

| Cause | Consequence |
| --- | --- |
| Vehicle gate closed when the van arrives | Van waits outside; if not at the sally port by 09:00 the run is cancelled; each scheduled hearing is missed ($750 each, rescheduled 2–4 days out) |
| Gate closed when the loaded van tries to leave | Van waits; arrival after 09:30 costs $300 per resident; if it is still held inside the gate at 10:30, the run is abandoned and everyone is returned (missed hearings). A van that has already passed the gate always continues to court |
| Lockdown during boarding | Nobody can board; missed hearings at departure ($750 each) |
| No route to the sally port | Boarding fails after 8 stalled minutes; missed hearing |
| Resident out of custody (escaping, at large) | One missed hearing without a fee; a new date is set once they are back in the facility |

**Escape risk** (0–100, shown with its factors): −20 baseline; +0.30 × impulsivity; +0.22 × (100 − discipline); +0.12 × (100 − patience); +0.40 per stress point above 40; +0.30 per safety point below 60; +0.20 per belonging point below 40; +8 serious allegation pending; +6 serious allegation with a hearing within two days; +10 more than 45 days left to serve; +8 sentenced within three days; +6 previous attempt; officers on shift −4 each (max −12) or +10 if none; −10 if an operational camera actually sees the resident's housing; −22 close supervision ($150 per resident per started game day; switching it off and on again the same day costs nothing more); +6 lights off at night; −15 lockdown. Low < 35, Elevated 35–54, High ≥ 55.

**Escape attempts**: every 30 game minutes, residents at High risk are reviewed. A fresh warning always comes first (event feed, profile, notice), and no attempt can start until at least two game hours after that warning. After that, the chance per review is (risk − 50)/100 × 0.25, never during alarm or lockdown, at most one attempt at a time, two days between attempts facility-wide and three to six days per resident. The attempt walks the resident along the real routing to the inside of the vehicle gate.

- **Detection**: an operational CCTV camera whose actual cone, range (15 m, or 8 m at night with lights off), power, condition and wall line of sight include the resident (`World.CameraSees`); an on-shift officer within 8 m (5 m at night with lights off) with a clear wall line of sight; or reaching a closed vehicle gate. An undetected attempt is invisible to the player.
- **Interception**: an on-shift officer within 1.8 m with line of sight stops it at once. After detection, mobile officers physically route to the escapee; alarm and a staffed dispatch console improve response speed. The ETA describes actual reachable travel. It cannot intercept someone by expiring. Tower lookouts detect but cannot intercept from their elevated post.
- **Outcomes**: intercepted (confined to housing 8 hours, alleged "Attempted escape", hearing within 3 days, +8 stress); open gate reached (escaped: $6,000 contract penalty once, at large 10–40 hours, then located, returned by the court van with an alleged "Escape from custody" and a new hearing); closed gate with no response for 40 minutes, no route for 8 minutes, or 120 minutes elapsed (abandoned, confined 4 hours).
- Present residents held out of routines (escorted, boarding, releasing, escaping, confined) still get hungry; confined residents are fed in their housing during their meal windows. Absent residents' needs are frozen.
- **Player controls**: close supervision per resident; CCTV placement and aim; officer hiring and shifts; lights; and, during an alert, Raise alarm / Close vehicle gate / Lockdown / Show in the Court & Custody panel.

## Saves

- `State.JusticeVersion` (0 → 1) is an additive marker; `State.Version` stays 1. A version-0 save gains profiles and an idle van. Names, ids, homes, positions, needs, cash, ledger, construction, history and the clock are untouched, and nothing is charged or dispatched on load.
- Every justice field is a plain field with an initializer. There are no nullable class references whose meaning depends on null, so Unity `JsonUtility` (which turns null class fields into default objects) and the System.Text.Json round trip used by the checks agree. `EscapeAttempt.Detected` and `LegalCharge.Label` are computed properties and are not serialized.
- Validation rejects unknown phases or custody states, manifests that disagree with custody, escape records without a matching resident, non-finite or out-of-range positions and minutes, empty charge lists, and oversized histories.
- Supervision is billed at most once per resident per game day (`LegalProfile.SupervisionPaidDay`). Fees are booked once, at the moment they occur. There is no wall-clock catch-up, and paused time never advances a timer.
- A reload is not bit-identical to continuing. Path caches (Codex's `paths`/`workerPaths` and justice's stall counters) are rebuilt on load, so walking positions can differ slightly from the first minute after load. This can shift later social contact and attempt timing. Court runs, fees and outcomes resume exactly, as the mid-trip check shows.

## Evidence (this clone, macOS, no Unity process)

| Check | Result | Scope |
| --- | --- | --- |
| `check-core.sh` with SDK 8.0.426 | 59 passed, 0 failed (40 existing + 19 justice) | Core C# only |
| Mutation test of the justice checks | 21/21 deliberate bugs caught: presence, van gate, CCTV detection, warning lead, escape penalty, response time, alarm, routine hold, lockdown boarding, supervision discount, missed-hearing fee, hydration, validation, officer line of sight, gate abort removed, gate abort on the road, late fee, late boarding window, same-day supervision recharge, release staying on the roster. One experiment also removed wall line of sight from Codex's `CameraSees` (reverted afterwards) | Confirms the checks fail when the rules break |
| Independent read-only critic review of the core (subagent) | 6 confirmed defects and several minor ones, all fixed and covered by checks above: released people blocked construction, zero boarding window for a late van, gate abort triggered off-site, same-day supervision recharge, wider absent social contact, tests passing for the wrong reason | Code review plus experiments |
| Critic's fuzz harness re-run on the fixed code: 3 contracts × 12 runs × 6,000 minutes with random gate/lockdown/alarm/camera/light toggles, both demos, supervision and hires, plus a save/load every 9 minutes | 24,012 reloads; no validation rejections, stuck custody or van states, off-site positions, or absent-food changes; ledger always reconciled; 267 court runs, 58 escapes, 318 interceptions, 28 releases | Core C# only; System.Text.Json, not `JsonUtility` |
| Offline typecheck of `Core` + `Runtime` with Roslyn against the installed Unity 6000.3.24f1 reference assemblies (`UnityEngine*.dll`, plus `UnityEditor*.dll` for the editor variant), with stubs for two URP types and `PresentationLighting` | Compiles for `UNITY_EDITOR;UNITY_STANDALONE_OSX` and for `UNITY_IOS` | Real Unity API signatures and partial-method matches; not imports, USS parsing, or Play |

Not validated: Unity import, USS parsing, Play mode, rendering, layout at phone width, touch, device performance.

## Interface gaps and requests for Codex

1. **Social contact ignores presence.** `PeopleDynamics.AdvanceRelationships` does not call `IsPresent`. A resident away at court or at large, parked at their home, can still change relationships with cellmates. The core checks print `INTERFACE GAP social contact while away at court` to show it. Request: skip pairs where `!world.IsPresent(a) || !world.IsPresent(b)`. Staff-misconduct stress (`PeopleDynamics.cs` ~245) also applies to absent residents.
2. **Meal-service ratio** divides served residents by `Data.Residents.Count`. Released people no longer count, but residents away at court or at large still do. Consider the present count or `World.CustodyCount` (a design decision for Codex).
3. **Population display** and the officer workload formula use `Residents.Count`, which includes people away at court or at large. `World.CustodyCount` is available.
4. **Optional officer dispatch hook** such as `partial void StaffDutyTarget(StaffMember staff, ref Point goal)` would let a detected attempt send the nearest officer physically. Codex has now implemented physical mobile-officer dispatch and removed the abstract interception timer.
5. **Intake**: releases free beds and shrink the roster. No resident intake exists to refill them. `World.FormerResidents` keeps the released records.
6. **Not justice; please verify.** Saves use `JsonUtility`. `WorkItem.Wall` / `WorkItem.Fixture` are null for furnishing and demolition work, and `JsonUtility` reloads them as default objects. The `World` constructor then rejects them ("Invalid pending construction work"), including in completed projects. Claude reproduced this at `f4266ca` in a separate scratch Unity project. The validation code is unchanged at `950455e`. Please try a save and reload after moving or removing a furnishing.

## Unity demonstration script (for the Unity runner)

1. Start a County Intake company. Open **People**. The Court & Custody panel sits at the top. Select any resident: the legal card shows status, escape risk and court line, and **Details +** expands charges, timeline, risk factors, close supervision and legal events.
2. **Demo · court run** (editor/dev builds; refused before 12:00 on a day with regular hearings, so the demo never takes the van from a real docket): the van drives in from the east along the road, parks at the bay south of the entrance, and the scheduled resident walks to the yellow boarding point and disappears. The van leaves; after about 3 game hours it returns, and the resident reappears and walks home (or stays released). Save and reload mid-trip: the van resumes from the same point.
3. Close the vehicle gate (Control) before step 2: the van waits outside the gate, and the hearing is missed after 110 minutes with a $750 fee.
4. **Demo · escape**: the highest-risk resident heads for the vehicle gate. With the gate open and nothing watching, they escape: a $6,000 penalty, and the resident returns later by van. Place a CCTV camera near (−4.5, −19) facing north first to see detection: a red marker, the alert actions, and officers' response countdown. Close the gate to see the hold and interception.
5. Regular play: a new company has a regular court run on day 2 at 07:15.
6. Note: the pause and load-does-not-advance checks are regression guards. With today's `Speed == 0` early return they cannot fail, but they will catch any future catch-up logic.

Please report console errors, layout problems at iPhone landscape width, and whether `Resources/Justice.uss` loads.

## Codex integration review — October 16, 2026

Integrated from `49aff6e` into the current operations branch. Codex ran 68 combined core checks (46 operations + 19 justice + 3 integration) and an actual Unity 6000.3.24f1 review: eight captures, no runtime errors. Actual `JsonUtility` install/move/removal saves, mid-court reload, visibility while absent, van return, legal phone layout, detected attempt and alarm interception passed. Evidence: ignored `Builds/JusticeReview/2026-10-16/`.

Original interface requests 1–3 are resolved: physical social contact/misconduct, staff workload, meal-service denominator and camera coverage denominator use presence. Release processing waits for an accessible exit and resumes after access returns. Construction work now has a discriminator that removes Unity’s phantom null payloads without discarding real work, including completed projects. The absent empty draft is also normalized; real unapproved plans remain.

Physical officer dispatch is implemented in the security milestone; only real proximity and line of sight intercept. Intake remains future work. The next Claude scope is Lives, not further justice/security edits; see `docs/claude-lives-handoff.md`.
