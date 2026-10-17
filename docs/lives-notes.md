# Resident lives: implementation notes

Scope (from `docs/claude-lives-handoff.md`):

- **New files:** `Core/Lives.cs`, `Runtime/GameLivesHud.cs`, `Resources/Lives.uss`, `docs/lives-notes.md`.
- **Existing files Claude already owned:** `Runtime/FacilityLivesView.cs` and `Tools/CoreChecks/LivesChecks.cs`.
- **One-line memory calls in other Claude-owned systems:**
  - Economy: job, first family money, clothes, debts;
  - Informants: snitch label, protective custody, agreeing to inform;
  - Counsel: lawyer, sentencing cut;
  - Personnel: fights;
  - Contraband: visits;
  - Population: arrival;
  - Kitchen: holiday dinner.
- **`Simulation.cs`:** `Relationship` became `partial`.
- **Not edited:** no Codex-owned source. Codex's `GameModesChecks.cs` fight-scale comparison now also holds activities off, like jobs.

## Hooks used

- **`HydrateLives`:** additive migration. Old saves have no plans or memories, and relationship kinds count as already noticed.
- **`AdvanceLives`:** after `AdvancePressure`, before `MoveResidents`. It ends plans that are over or no longer in free time, plans new activities on each person's own staggered clock, applies effects only when the person has actually arrived, and every 30 minutes notices new friendships and feuds. Court events become memories.
- **`ChooseResidentActivity`:** a pure read of the stored plan. It replaces the target only when the routine target is home and the person is in free time. Meals, yard, work, court, confinement, separation, protective custody, riots and lockdown always win. It is safe for the secure-movement planner to call first. `World.DestinationFor` wraps it for checks.
- **`PrepareResidentPose`/`AnimateResidentLife`:** five new poses (below). They run after `MoveActor`, so they win over the idle head glance from `FacilityCharacterDetail` while active, and that glance takes the head back afterwards.
- **`BuildLives`/`RefreshLives`:** the Life block on the resident card, seated directly under the person summary after the other blocks are built.

**Free time:** 06:00–22:00, when none of the above applies.

## Activities

| Activity | Where | Capacity | Effect per minute there | Milestone |
| --- | --- | --- | --- | --- |
| Reading | Bookshelf (2 places) | 2 | −0.010 stress | A book every 240 min (a named book; memory) |
| Exercise | Exercise bench | 1 | −0.02 stress, +0.005 health (up to 95) | Every 600 min (memory) |
| Television | Television; seats on benches and chairs within 5 m, else standing places in front | 5 | −0.012 stress. With another viewer: +trust and belonging, or with a rival (tension 58+) +tension and an argument memory | — |
| Study | Table places (6), not the dining table within an hour of a meal | 6 | −0.006 stress, +0.002 discipline (up to 90); ×2 progress with a library clerk at work or a caseworker on shift | A certificate every 900 study min (first: equivalency) |
| Talk | A common-room bench or table near both friends (trust 60+, tension below 35), or halfway | — | +0.04 trust, −0.03 tension, +belonging, −stress | Mentoring (below) |
| Quiet | Their bunk (a daytime lie-down) | — | −0.008 stress | — |

**Choosing:**

- Each activity is scored by traits and needs: discipline favors reading and study, stress and impatience favor exercise, sociability and impulsiveness favor television, low belonging favors talking, high stress and low sociability favor quiet time.
- Each score gets deterministic noise of up to 0.5 per half hour, and loses points for distance.
- Only furnishings that are installed, not scheduled for removal, inside a room, under capacity and reachable from the person's home count.
- A plan lasts 40–80 minutes. Each person replans 10–30 minutes after their last plan ends, so the room never switches all at once.

**Mentoring:** while two friends talk, a resident with Discipline 70+ who is at least 15 steadier than a friend with Impulsivity 60+ (and at least 15 more impulsive) mentors them. The mentee gets −0.002 impulsivity and +0.002 discipline per minute. It runs one way only, and both people remember it.

**Certificates at court:** each certificate is +5% program credit at sentencing (in `Counsel.WatchHearings`), with or without a lawyer.

## Memories

- Up to 12 per person. The same kind about the same person counts once a day.
- **Sources:**
  - arrival;
  - friendships, feuds, enemies and cooling off;
  - fights, attacks and fights an officer broke up;
  - books, fitness, certificates, mentoring and TV arguments;
  - jobs, first family money, new clothes, unpaid debts;
  - visits;
  - snitch labels, protective custody, agreeing to inform;
  - a lawyer, days cut at sentencing, court events;
  - holiday dinners.

## Story chapter

A pure function of state, checked in this order:

1. In protective custody
2. Watching their back (jacket)
3. Finding their feet (first 3 days)
4. Counting the days (release within 7)
5. In over their head (overdue debt)
6. Running the {gang} (a Codex gang organizer)
7. Hard time (2+ fights in a week)
8. Learning the ropes (has a mentor)
9. Showing someone the ropes (is a mentor)
10. Going straight (a certificate, 5+ hours of study, or 5+ days worked with no write-ups this week)
11. Struggling (stress above 60)
12. Doing their time

## Poses (FacilityLivesView)

| Pose | What it looks like |
| --- | --- |
| Read | Both hands up holding a book (no prop, so mesh combining is safe), head bowed, an occasional page turn |
| Exercise | Curls with squats |
| Watch | Arms folded, an occasional laugh |
| Study | Seated at the table place using the existing bench seat alignment, writing |
| Rest | A daytime lie-down using the verified sleep placement, one hand behind the head |

Talk reuses the existing gesture pose. There are no per-frame allocations.

## HUD

The resident card › Life block shows:

- the story chapter and what they are doing now;
- progress: books, certificates and progress to the next, exercise hours, mentor or mentee;
- their closest friend and worst enemy by name;
- the last 5 memories, with the day.

In editor and debug builds, "Demo · free time" (`StageLivesDemo`) works between 09:00–10:00 or 20:00–21:40.

## Evidence

- **Core checks:** 303/303, including 10 lives checks:
  - migration;
  - free time only (never meals, night or lockdown);
  - the real hook is pure and replaces only a trip home;
  - capacity;
  - books, certificates and sentencing credit (60 → 57 days);
  - talk and one-way mentoring;
  - relationship, job and arrival memories;
  - the memory bound and same-day dedupe;
  - story chapters;
  - a day with nobody stuck, reload equivalence and malformed saves.
- **Offline Unity typecheck:** passes for editor/macOS and iOS.

## Unity check request (Codex as runner)

1. Add a library shelf, an exercise bench and a television with a bench in a common room. At 09:00 or 20:00 press "Demo · free time":
   - residents walk to the shelf (Read pose), the bench (Exercise), the TV (Watch) and table places (Study, seated and aligned);
   - pairs of friends meet and use the Talk gesture;
   - others lie on their bunks (Rest).
2. At 12:00 everyone with an activity leaves for their meal place; at 22:00 everyone goes to bed.
3. On a resident's card, the Life block shows the chapter, activity, progress, people and memories, and fits at 280–350 px.
4. After a reading pose ends, the head returns to its idle glance (no stuck bow).
5. Save and reload mid-activity: the same plan continues.
