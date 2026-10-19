# Physical officer backup

Staff play connects a local encounter to the actual staffing roster. Open **Actions → Call backup** while facing the person you are dealing with. One available line officer is reserved; **Dismiss backup** releases them. The request resumes1× if paused. Opening the menu or inspecting status does not advance the clock.

## What happens

1. Control acknowledges the request after a three-game-minute dispatch phase (.6 action seconds). The helper waits during dispatch; the player can keep responding to the encounter.
2. The helper walks a real route at up to .56m per simulation tick to a clear flank position. They do not teleport or move twice through the personnel loop.
3. The status changes from Radioing to En route to Supporting. Dispatch/arrival use existing field feedback; the Actions header shows the current phase.
4. Support counts only while the helper is fit, available, within2m of the subject and3m of the player, with clear navigation and sight lines. It improves standing cuff probability by .20. Order compliance also becomes effective below65 resident stamina or balance, compared with the ordinary35 threshold. A fresh, highly resistant person can keep fighting. Existing injury/compliance rules remain in force.
5. Cuffs, handback, changed target custody, a different active fight/hold, authority loss, departure, excessive separation or lease expiry release the helper. Dismiss is available even after losing reach of the original subject. They resume normal duties.

## Staffing and movement authority

Only arrived, on-shift, unsuspended ordinary officers with health above45, no personnel post and no assigned duty can respond. Wardens, security posts (including assigned post IDs), watch/mentoring/intelligence duties, intake, court/secure/capital escorts, phone calls, player-controlled staff and existing fight/escape/radio/inmate responders are excluded. The player may request as an authorized officer, including a playable warden; the warden is never recruited as a helper.

One helper and one120-game-minute lease bound a call. More than24 minutes without route progress ends a blocked response. Recruitment checks reachable flank positions. Support revalidates access when people or geometry move. Higher-priority incident tasks reclaim the helper before backup movement; no extra benefits persist through walls or reassignment. The helper never independently damages or arrests the subject in this milestone.

## Saves, ownership and limits

Dispatch is live-only. Save files retain normal staff positions, then loading removes our own Backup task labels/destinations and schedules normal duties immediately, even while paused. Temporary route keys are removed after each step; the standard movement-memory loader validates and prunes any stale unknown keys. Other incidents' paths and labels are preserved.

Core/StaffBackup.cs owns the call, eligibility, physical approach and support. IncidentScenes reuses its existing movement-priority hooks, leaving Personnel/Operations sources untouched. World.StaffBackup is a read-only snapshot (phase, helper identity, subject, radio countdown, actual support and outcome) for presentation. Claude owns locomotion, facing/turning, radio gesture, rig/IK, camera and touch. The consuming adapter should face a supporting helper toward its subject without changing Core movement or inventing restraint contacts. No new animation fidelity, native device feel or performance claim is made here.

Final validation and exact source are recorded in docs/reviews/2026-10-19-staff-backup.
