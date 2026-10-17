# Personnel, administration and resident welfare — notes

Owner: Claude (`personnel` scope, user request). Branch `codex/claude-personnel`, merged with `codex/live-operations-handoff` at `d5f89ef`. People, employers, places and outcomes are fictional.

## Files

| File | Role |
| --- | --- |
| `Assets/Cellblock/Core/Personnel.cs` | `PersonnelState` (files, candidates, write-ups, regime), `PersonnelFile`, `Candidate`, `WriteUp`, resident health fields, `PersonnelRules` (generation, migration, validation) and the `World` partial (recruitment, careers, discipline, duties, warden office, health, fights) |
| `Assets/Cellblock/Runtime/GamePersonnelHud.cs` | Staff & Administration panel in People, file button on staff profiles, Assign staff on resident profiles, and the full-screen sheet: Personnel file (Profile / Resume / Record / Duty), Hiring board, Warden's office, Assign staff |
| `Assets/Cellblock/Runtime/PortraitElement.cs` | Painted ID-photo portrait from a persistent `FaceSpec` (Painter2D; no textures) |
| `Assets/Cellblock/Runtime/FacilityPersonnelView.cs` | Warden shoulder boards and tie, nurse cap and armband, and a watch link line while the watcher or watched resident is selected |
| `Assets/Cellblock/Resources/Personnel.uss` | Styles; other stylesheets untouched |
| `Tools/CoreChecks/PersonnelChecks.cs` | 19 checks, called from `RunJusticeChecks` |

Wiring: hydration, the minute tick and the HUD/view refresh all go through Claude's justice hook implementations. No Codex file was edited. **Justice files changed on this branch** (for Codex review, since Codex now integrates justice): personnel calls in `HydrateJustice`/`AdvanceJustice`/`BuildJustice`/`RefreshJustice`/`RefreshJustice(view)`; escape-risk factors; officer lists that exclude the warden, suspended staff and compromised watchers; detection credit; CCTV monitoring; court escort; good-time reviews and referral credibility in `Resolve`; and the court call-out fix below.

## Gameplay

**Recruitment.** Six applicants per listing (three officers, a caseworker, a nurse and a technician; a warden applicant replaces an officer while the post is vacant). Each has a resume, a reference, an asking wage, age, experience, a face and a hidden true integrity, sometimes with gang ties. Experienced and honest applicants ask for more. A free interview gives a hint that is right 70% of the time. A $250 background check is accurate and returns after one game day. The listing refreshes every three days; reposting costs $300. Hiring uses Codex's `HireStaff` (the $500 mobilization and the staff shuttle), then applies the applicant's identity.

**Files.** Face, backstory, original resume, references, background note, rank, pay, hire date, discipline and career record, supervisor remarks, write-ups and duty statistics. Backstories and references are generated from the true integrity band and gang ties, so careful reading pays. The three opening staff get files on first load (Lee Morgan's reads as compromised). Staff can be renamed.

**Ranks.** Officer → Senior Officer → Sergeant → Lieutenant → Captain; Caseworker → Senior Caseworker → Case Manager; Technician → Senior Technician; Nurse → Charge Nurse.
- **Promotion** needs two days of service and no recent written warning, suspension or final warning. Sergeant and above also need the warden.
- **What a promotion does:** a raise of 15% of max(standard wage, current pay), +3 integrity and −8 pressure. The opening staff are paid inside the operating budget, so their raises appear as additional wages.
- **Demotion** reverses the raise and adds stress. Under high pressure it triggers a resignation notice; the person leaves after one day.
- **Supervision:** the most senior officer on a shift (Sergeant and above, or the warden) pulls the integrity of the officers under them toward their own, about 25% of the gap per 12-hour shift. An honest sergeant reforms a shift; a corrupt one spreads corruption.

**Discipline.** Commendation, verbal warning, written warning, suspension (two days, warden only) and final warning (warden only). Discipline corrects people with integrity of 50 or more and breeds resentment in people below that. Termination goes through the warden and pays two days at max(standard wage, pay) as severance. Staff with pressure of 90 or more hand in a notice at review time (burnout).

**Supervisor remarks.** Every three game days, the warden (or the senior sergeant, or the owner) reviews each employee from real statistics: shift minutes, watch closeness, interceptions, confrontations defused, sick-call minutes, misconduct and write-ups. A supervisor with integrity below 45 covers for staff who share their gang favors (+2 rating, red flags omitted) and marks honest staff down. A poor honest review becomes a staff write-up.

**Watch assignment.** A line officer shadows one resident through their shift using Codex's staff movement (the destination is replaced after `AdvanceStaff`). Escape risk drops by 18 × reliability, where reliability = clamp((integrity − 30) / 50, 0, 1). An officer who favors the resident's gang (favoritism 20 or more) has reliability 0 and is excluded from detecting or intercepting that resident. Losing sight for 90 minutes is a neglect-of-duty write-up.

**Special duties.**
- **Gate post:** officer at (−1.1, −22); attempts are met in person.
- **Court escort:** boarding is faster and allowed during lockdown.
- **CCTV monitoring:** with nobody monitoring, a camera in view detects with a 35% chance per minute; with a monitor, it detects at once and the response is 3 minutes faster.
- **Perimeter patrol:** a route around the site.
- **Cell checks:** −6 escape risk for 8 hours; +1.5 stress per check.
- **Mentoring:** the junior's integrity drifts toward the mentor's.
- **Counseling** (caseworker, caseload of up to 3): stress relief and −8 risk for 12 hours.
- **Reentry planning:** strengthens good-time.

**Warden.** A post on the officer ladder, hired from applicants or appointed from a lieutenant or captain.
- **Write-up queue.** Residents: dismiss, sanction (confined 12 hours) or refer to court. Staff: dismiss, warning or suspend.
- **Court referral ($200).** Files an alleged "Institutional misconduct" charge carrying the warden's credibility.
- **Referral at court, sentenced residents.** P(dismissed) = clamp(0.65 − credibility/200 − (severity − 1) × 0.1, 0.1, 0.8). Otherwise the sentence grows by 10 + 10 × severity days.
- **Referral at court, pretrial residents.** Referrals lower the chance of release at court.
- **Credibility feedback.** An upheld referral adds +3 credibility; a dismissed one costs −6, so weak referrals hurt.
- **Good-time recommendation.** Needs a sentenced resident with 5 days served (including prior custody), a clean week, no recommendation in 10 days and a release more than 3 days away. It is reviewed at court.
  - Chance: 0.3 + credibility × 0.004, +0.15 with reentry planning, +0.05 with recent counseling, +0.1 under a rehabilitative regime, −0.25 with recent write-ups.
  - Credit: 5–15 whole days, never inside the last 2 days.
- **Expedite a pretrial hearing ($150, once per resident).** Moves the hearing to tomorrow.
- **Facility regime, once per day:**
  - **Rehabilitative:** stress −0.004/min, risk +3, fights ×1.1, counseling ×1.5, better good-time.
  - **Strict:** stress +0.006/min, risk −5, fights ×0.7, a minor write-up every 2 hours.

**Nurse.** A post on the caseworker role. On shift, they walk to the most seriously hurt present resident (health under 85) and treat them by +0.8 × (0.6 + integrity/250) per minute.

**Health and fights.** Residents have health (92 at start). It falls when food is under 20 and recovers slowly when food is over 50. Health under 50 adds stress; health under 40 lowers escape risk by 10.
- **Welfare violation:** an untreated resident under 25 health for 12 hours costs $2,000, at most once per 12 hours per resident.
- **Fight checks:** every 30 minutes, present residents in the facility with tension of 58 or more who are within 2.6 m with line of sight can fight. Chance = (Nemesis 0.16 / Beef 0.05) × impulsivity × stress × regime factor, with a 12-hour cooldown per resident.
- **Officer within 5 m with line of sight:** the officer defuses it. Both residents get a "Threatening behavior" write-up (severity 1) and tension falls.
- **No officer nearby:** both are hurt (8–22 health), stress +10, safety −8, nearby witnesses lose safety, tension +6, and both get a "Fighting" write-up (severity 2).
- **Attempted escape write-ups:** an interception files a referred write-up (the allegation already exists); a detected abandonment files "Out of bounds"; recapture files "Escape".
- **Staff misconduct:** the contraband and favoritism events from `PeopleDynamics` become staff write-ups, unless the supervisor shares the employee's gang favors.

## Justice fix found during this work

Regular court runs boarded only from the van's 07:33 arrival with an 08:15 deadline. Residents in the far west cells walk about 22 m at Codex's waypoint pace (around 0.3 m/min), so they systematically missed hearings and were charged $750 each. Scheduled residents are now called to the sally port when the van is dispatched and wait there. The regular deadline is 09:05, the latest departure that still makes a 09:30 hearing. The regular-run check now requires every in-facility docket resident to attend.

## Evidence (this clone; no Unity process)

| Check | Result |
| --- | --- |
| `check-core.sh` on the merged base | 88 passed, 0 failed (Codex's 69 combined + 19 personnel) |
| Mutation testing of personnel rules plus the call-out | 26/26 deliberate bugs caught, after strengthening three weak checks; two survivors had exposed a real pay bug (opening staff earn 0, so raises and severance did nothing), now fixed |
| Offline Roslyn typecheck of Core + Runtime against Unity 6000.3.24f1 reference assemblies, editor/macOS and iOS defines | Compiles, including Painter2D portraits, TextFields, sheets and badges |

Not validated: Unity Play, USS parsing, layout at phone width, touch on the sheet, portrait rendering.

## Requests for Codex

1. **Keyboard focus.** `GameHud` stops Space in a trickle-down handler, and `GameController` reads WASD/QE/RF/Esc from `Input`. Typing in a `TextField` therefore pauses the game, moves the camera, and cannot enter a space. The rename uses separate first/last fields as a workaround. Please skip those shortcuts while `root.focusController.focusedElement is TextField`.
2. **Security model alignment.** Personnel duties are staffing (who stands where), meant to feed your planned security queries. When `Security.cs` lands, the CCTV-monitoring detection rule, gate-post presence and the abstract response timer should move behind your queries and dispatch. I'll adapt my side.
3. **Placement drag-and-drop** (from the earlier request): hand over the placement input path, or add hooks.
4. **Opening staff wages** are 0 inside the contract's operating budget. Personnel uses the standard wage for raises and severance. If you itemize opening wages later, `StandardWage` can go.

## Unity demonstration

1. **Open the hiring board.** In People → Staff & Administration → Hiring board, read the applicants, Interview one, order a Background check, and hire the warden applicant. They arrive by shuttle; their portrait, resume and backstory show in their file.
2. **Show discipline and remarks.** Select Lee Morgan → Open personnel file → Record. Issue a written warning (resentment, because integrity is under 50). Review remarks after three days.
3. **Assign a watch.** Select a resident → Assign staff → assign an officer to watch. A teal link appears while either is selected, and the risk factor shows "Watched by …".
4. **Stage a fight.** Use Demo · fight (editor/dev builds): the tensest pair fights in the yard, or an officer defuses it. With no nurse, the injury persists; hire the nurse to see sick call.
5. **Work the Warden's office.** Refer a fighting write-up for a sentenced resident to court, recommend good-time, and switch the regime.
