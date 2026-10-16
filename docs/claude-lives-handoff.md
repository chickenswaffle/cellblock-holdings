# Claude next slice: resident animation, daily life and stories

Status: handoff ready. Reviewed integration base: `9624674` on `codex/live-operations-handoff`. Fetch the latest published branch and verify this commit is present before branching. It includes your justice work, Codex integration fixes,69 core checks and actual Unity review evidence. Do not start from your earlier justice branch without merging the reviewed shared base. A final deterministic capture-timing rerun continues under Codex; it does not change your scope.

## Scope

The user wants richer prisoner lives and stories and more natural animation. Your justice branch at `49aff6e` has been imported for Codex review. Preserve those legal identities, court clocks, custody states and releases. Codex is fixing integration gaps and owns connected doors, security equipment, power, upgrades, staffing/comfort and all Unity runs.

Allowed new files only (plus their metas and your own Claude log in COORDINATION.md):

- `Assets/Cellblock/Core/Lives.cs`
- `Assets/Cellblock/Runtime/FacilityLivesView.cs`
- `Assets/Cellblock/Runtime/GameLivesHud.cs`
- `Assets/Cellblock/Resources/Lives.uss`
- `Tools/CoreChecks/LivesChecks.cs`
- `docs/lives-notes.md`

Use a new branch `codex/claude-resident-lives` in your separate clone. Claim `resident-lives` and acknowledge the plan in your own log. Do not change the shared base branch or existing implementation files. Request interface changes in your log. Do not launch Unity; request its checks from Codex. Commit scoped milestones and push your branch for integration; report SHA, tests, remaining issues and a deterministic demo.

## Requested implementation

- Distinct daily activities and personal goals: reading, exercise, conversation, quiet time, constructive work/learning and rest, tied to actual installed furnishings and reachable positions. Keep meal/court/confinement priorities clear. No crowd-wide instant target changes.
- Persistent personal memories and story arcs: evolving friendships, tensions, rivalries, clique/gang connections, mentoring, setbacks and progress. Ground events in proximity, the person's traits, facilities, wellbeing and named relationships. Avoid arbitrary repetitive story spam or replacing existing identities.
- Visible activity poses using the existing articulated shoulders, elbows, waist and knees: sitting/reading, conversation gestures, exercise and idle variation. Respect held objects, contact points, walls/furniture, walking and pause. Keep per-frame animation free of object/mesh/material allocations.
- Compact People profile story/activity card with named participants, causes and consequences. Stable UI buttons, bounded event history and touch-friendly expansion. Add a clear way for Codex to demonstrate several activities/stories.
- Additive save migration; bounded memory/history; explicit game minutes only; no repeated rewards/fees on load. No routine, physical social contact or facility consequences for people away at court/at large. No escape algorithm or new security system: existing stress/safety/belonging already feed Justice risk.

## Hooks already added by Codex

`World` partial:

```csharp
static partial void HydrateLives(State state); // additive migration/validation during load, before normal world validation
partial void AdvanceLives(); // after AdvanceJustice, before MoveResidents, once per game minute
partial void ChooseResidentActivity(Resident resident, ref Point target); // normal routines only; justice-held residents have already been skipped
```

Use `World.IsPresent`, `World.InCustody`, `Resident.Legal`, real `Routes`, operational `Data.Fixtures`, `Rooms`, existing people traits/relationships/needs. `ChooseResidentActivity` must preserve meals, court and other obligations; inspect the current clock/state before overriding. Never target a furnishing that is still being built. Use the existing routed movement rather than moving actors through walls. Put new serialized fields in partial Resident/State.

`FacilityView` partial (same namespace/class; nested Actor is available to your partial):

```csharp
partial void PrepareResidentPose(Actor actor, Resident resident, World world); // before MoveActor
partial void AnimateResidentLife(Actor actor, Resident resident, World world); // after MoveActor
```

Actor has Root, Body (waist pivot), Limbs (two shoulders, two hips), Forearms, Hands, Knees, PoseArms and a reused Motion. Set PoseArms only while your activity owns the upper body; clear it during ordinary locomotion/idle so neutral restoration works. Reuse `ArmPose` if useful. Do not alter builder/staff animation. Pose props must be reused and cleaned on world changes.

`GameHud` partial:

```csharp
partial void BuildLives(); // after existing People + Justice UI exists
partial void RefreshLives(World world, Resident resident); // resident can be null for staff
```

Reuse `personCard`, `peoplePane`, helpers and your own Resources/Lives.uss. Do not edit existing USS or Justice UI. `static partial void RunLivesChecks()` is called by the existing core driver; add it in LivesChecks.cs.

Read docs/security-and-services-plan.md to understand Codex's boundary. It is a plan, not an already implemented API. Ask for a needed interface rather than inventing another power, comfort, escape, transport or staffing system.
