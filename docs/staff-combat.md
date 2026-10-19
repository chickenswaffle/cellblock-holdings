# Staff combat: fight, control, arrest

This is a staff-playable encounter inside the management simulation. An officer's use of force can provoke sustained retaliation; it also creates conduct records, persistent injuries and staffing consequences. Resident control remains outside ordinary management play.

## Player flow

Select an arrived, on-duty officer and choose **Play as staff**. Use the existing movement/look controls and **Actions** (E/right click on desktop). Punch remains the existing F/left-click action. The Actions menu now includes **Guard / Lower guard**, **Shove** and **Order compliance**. These are exact-target commands; another person cannot silently replace the selected opponent.

1. A strike commits a short windup. Damage waits for actual game-clock contact. Recovery prevents attack spam.
2. Personality and stress determine whether the resident fights, complies or backs away. A fighting resident repeatedly approaches and telegraphs attacks; they do not throw one counter then return to their routine.
3. Face the incoming strike and guard, or move out of range. Guard reduces damage and balance loss while spending stamina; attacks from behind bypass it.
4. Shoves cost stamina and reduce balance without direct health damage. Exhaustion, lost balance and injury make compliance/restraint more likely. Order compliance and use Cuff, or Ground & cuff when control is needed. A knockout is not required.
5. Complete the existing cuff sequence; then search, stand and escort, release, or return to management. A held/cuffed person cannot continue throwing free attacks.

A valid committed action resumes the game at 1×. Merely opening a menu does not. Pause freezes the encounter. Guard is a toggle through the existing menu; quick bindings and mobile touch placement remain Claude's input responsibility.

## Simulation and authored physics

`Core/StaffCombat.cs` owns the rules. No Unity rigidbodies, camera transforms, bones or IK are changed here. One explicit game minute advances .2 action seconds; no wall time advances a closed game.

| Quantity | Rule |
| --- | --- |
| Contact | Resolve once on the next game minute; recheck target, authority, facing cone, distance, route and sight line |
| Reach / step-in | 1.15 m reach; at most .35 m forward along the committed heading, only toward an attainable unobstructed target |
| Recovery | Three game minutes for player strikes/shoves; resident decisions at four-minute intervals |
| Normal / power / shove | 6 / 11 / 0 health damage; 21 / 30 / 32 balance damage |
| Guard | Health ×.2, balance ×.3, impulse ×.25; costs stamina; directional, not immunity |
| Effective mass | Stable identity-derived 70–100 game mass units |
| Impulse | 45 / 70 / 85 authored momentum units; velocity += impulse / mass |
| Motion bounds | Speed ≤2 m/s; drag 2 m/s²; travel ≤.45 m per impact, ≤.25 m per simulation step |
| Collision | Sweep movement in ≤.04 m segments against authoritative navigation; stop at obstruction |

These values are tuning data, not claims of biomechanical realism. This is deterministic kinematic contact, not ragdoll simulation or a dynamic crowd collision solver. A miss cannot pull the officer backwards or home onto a person behind cover. Incoming attacks can be interrupted by an unblocked contact. Falls recover through side-lying, seated, kneeling and rising stages unless custody takes ownership.

The live encounter has one opponent. Switching opponents is blocked during active resistance; distance, withdrawal, compliance, custody, incident ownership, departure, handback and incapacitation bound its lifetime. Routine movement cannot drag the fighting resident toward a scheduled activity.

## Consequences and persistence

Attempted unjustified strikes retain existing conduct consequences even when contact misses. Resident assault records settle at actual contact. Health damage and pressure affect the officer. At health ≤20, direct control ends and the player must switch staff and arrange recovery.

`State.StaffCombatMedical` is an optional additive saved record (version1). Old saves receive an empty record. Toggling control or reloading cannot heal injuries. Live attacks/guards/impulses are not saved. Recovery advances only on the game clock, outside that officer's active fight: .08 health/minute off shift, or .25 with an arrived, on-shift, unsuspended personnel nurse within1.8 m and a clear route. No nursing benefit is granted merely for hiring someone who has not arrived.

## Presentation boundary and evidence

ActionChoreo v1 publishes delayed contact outcomes, guard, compliance, free recovery, resolved roots and once-only impact cues. Claude owns the consuming rig/IK, locomotion, first-person arms/camera, impact feedback and touch controls. Do not add a second visual step-in after Core has resolved one (`RootAlreadyAligned`). Rendered wrist/knee/root contact gates still require Claude's consumer; ordinary combat captures cannot substitute for them.

The shared `combat` feature capture exercises actual menu dispatch, paused windup, retaliation, guard, shoves and the cuff sequence in the single installed editor with a disposable company. It writes a unique Temp folder and never replaces the scene or writes player slots. See the current review manifest for exact source and results. Desktop evidence does not establish native phone feel or performance.

## Research informing the architecture

Unity documents that character-controller movement does not automatically respond to Rigidbody forces: [Character Controller manual](https://docs.unity3d.com/6000.0/Documentation/Manual/class-CharacterController.html). This implementation uses its existing Core navigation rather than adding that component; impulse integration is explicit. Unity's [fixed update documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/fixed-updates.html) describes fixed timesteps; this game's corresponding deterministic step is its explicit simulation minute. These sources inform separation of authored movement from physics; they do not validate the game's tuning or police procedure.
