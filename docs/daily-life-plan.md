# Purposeful daily life — active implementation plan

## Player direction

The user wants a believable prison management simulation with personal drama. The player sets policy and assignments, with optional direct intervention. People should try sensible alternatives when blocked; their patience, fear and recent experiences can also lead to refusal and trouble. Ordinary life should dominate, with tensions building into occasional major incidents.

Selecting a person should show **goal, reason, next step and blocker**. Personality and lived experience should change habits. This milestone prioritizes complete interactions and believable movement using the current characters; a character art/rig upgrade follows that foundation.

Two interview cards remain open: timetable model and first daily-life slice. The provisional recommendation is a facility timetable with individual choices inside its windows, beginning with coherent free time after breakfast. Existing meals, jobs, custody and security retain priority.

## Foundation we are extending

Published Claude source `83a1d4e` already contains installed-furniture activities, named memories, story chapters, mentoring and daily-life poses. Extend it. Do not create a second routine or story system.

The focused critic found specific seams: new activities wait for an unrelated movement decision; travel consumes activity time; conversation pairs do not reserve space; interruptions discard intent; proximity-based poses can contradict what is actually happening.

## First bounded milestone

1. **One saved lifecycle.** A person's existing Activity becomes Travel → Wait/Align → Act → Leaving → Complete, with a bounded Suspended state for higher-priority obligations. Effects and effort time accrue only while the person is actually doing the task. Journey time and blocked travel have separate limits.
2. **Prompt route ownership.** Starting, ending or changing an activity invalidates only that resident's ordinary route. A pure destination query never mutates a plan. Incident, intake and custody movement keep their own route owners.
3. **Real places and partners.** Claim a usable spot before travelling; two-person interactions claim both places and both willing participants. Meal seats, physical furniture, walls and active tasks constrain those claims. Release claims on interruption/removal; revalidate before resuming.
4. **Sensible choice and variety.** Needs, personality, recent conflict and successful habits affect the next choice. Avoid choosing the same option forever. Estimate a journey from the real navigation waypoints and walk speed, look ahead using the same meal, yard, work and night rules, and choose a short session that leaves time to settle and depart. Suggestions also reject trips that cannot fit the available block, preserving the former intention. A blocked choice tries a reachable alternative, then explains why it is waiting or resting. Frustration is bounded and feeds existing pressure/relationship systems.
5. **Complete visible interactions.** Slow/turn into the spot, face the person or object, settle into the action, use a matching visible prop, then leave cleanly. Reading, conversation, study, exercise and television should each read differently. Natural conversation has a listener and speaker; walking past someone is not enough to manufacture a conversation.
6. **A readable personal activity card.** Goal, reason, phase/progress, next step and current blocker directly beneath the selected person’s identity. Stable Read / Exercise / TV / Study / Talk / Rest suggestions respect priority work and personality; failed suggestions retain the old plan, and repeated refusals cannot farm stress or memories. Existing memories and story chapters remain below the need meters.

## Priority and interruption rules

Safety, active incidents, custody/court/intake and confinement own a person before discretionary life. Meal and work windows retain their existing authority. A suspended routine retains at most one bounded intention. It resumes only if still appropriate, its object/partner exists, its place can be reclaimed, and the person is eligible. Otherwise it ends clearly and replans from the present situation. The first interruption sets a 240-game-minute lifetime deadline for that retained intention; repeating interruptions cannot renew it. A new intention resets that deadline. Waiting for a conversation partner follows that partner’s real progress and the 180-minute journey limit, rather than abandoning a longer moving journey after an arbitrary short wait. All participants receive a minute’s effects before any remaining-effort counters decrease, so a conversation finishes symmetrically. No off-screen catch-up; pause and save/load preserve the explicit game clock.

## Proof required

Core checks for long journeys, prompt route changes, pause/reload, exactly-once progress/completion, partner consent/removal, spot contention, geometry changes, interruption/resume and legacy hydration. Actual Unity review must inspect visible walking/arrival/interaction/departure, prop and pose ownership, incident priority, selected-person explanations and phone layout. Use disposable companies and the sole editor. A critic reviews source and actual captures. Device performance and production animation fidelity are separate work.

## Following milestones

- Expand this lifecycle into hygiene, breakfast queues and scheduled work rather than building separate controllers for each.
- Staff handovers, meaningful patrol stops and staff/resident service interactions.
- Richer group behavior, lasting habits, supervision/disobedience and observed story consequences.
- Character models, rigs, animation assets and close-view art upgrade on top of the same action contract.

## Current mechanics and continuation contract

The lifecycle is additive to Claude’s existing `Resident.Activity`, installed furniture and memory system. Claims are derived from eligible active intentions, never a second saved reservation list. Travel and suspension earn no activity effects. Incidents own their movement and poses; ordinary route invalidation only removes the resident’s ordinary route. Real arrival, object existence and reciprocal participation gate progress. Television seats require the same room and sight; a tutor bonus requires an actually nearby visible worker.

Autonomous activity choice includes health, safety, stress, discipline, sociability, impulsivity, recent conflict memories, cumulative reading/study habits and recent completed activity. Background proximity familiarity still runs at its existing slower cadence; deliberate conversation adds its own small reciprocal benefit. No new incident-frequency inflation or random drama generator is introduced.

Reading has an open book and page motion; exercise uses two weights; study uses a notebook and pencil at a real table bench; conversation turns people toward each other and alternates speaker/listener emphasis. Held props are cached per actor and hidden on departure or priority change. The current procedural characters remain the visual foundation for this milestone.

For the next slice, extend the same action contract into wake-up, hygiene, breakfast queues and jobs; avoid separate activity controllers. Staff service/handovers and richer group decisions follow. Schedule policy controls, art assets, device performance and a complete playable morning remain open work.
