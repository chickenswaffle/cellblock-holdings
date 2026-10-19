# Staff encounters and direct control

This milestone keeps management staff-only. An officer can speak to, pat down, search the pockets of, hold, cuff, ground and cuff, escort, release or confine a present resident. Ordinary staff can speak and strike, with the existing conduct consequences; custody/search commands require the Officer role. This is manual custody inside the prison, not a new court conviction or sentence.

## Try it on the Mac

Select an arrived, on-duty officer in People and choose Play as staff. Use V to switch first/third person.

| Input | Action |
| --- | --- |
| WASD / arrows | Camera-relative movement |
| Mouse | Look while captured |
| E / right click | Actions for the person you are facing; a nearby bunk can be searched when no person is focused |
| F / left click | Strike; when holding someone, ground and cuff |
| G | Take hold, apply cuffs, then stand/escort; release is an explicit menu choice |
| Shift / R | Hold to run / toggle run |
| Q | Confiscate nearby loose contraband |
| P | Work phone |
| V / C | Change perspective / recenter |
| Space | Pause/resume outside a menu |
| Esc | Free captured cursor; Back out of an open menu; return to management when the cursor is free |

The Actions menu also has Pause/Resume. Time otherwise stays live while choosing. Choices show why they are unavailable. Successful commands close the menu and return to the field; feedback and action progress remain visible.

On touch, MOVE and LOOK are separate controls. Swipe the free right side to look; use Actions and the field buttons to act. Opening or closing a menu releases captured fingers and requires a fresh movement press. Phone portrait uses a scrolling single column; landscape uses two columns. All interaction choices and header controls have at least44px hit targets. Controller face buttons retain their existing mapping; the Actions tray uses focusable semantic buttons for navigation/submit.

## Rules that make the sequence coherent

- Focus comes from camera aim and the shared closest-hit picking rule. The controlled officer is ignored by that aim ray. Four reusable candidate slots bound visibility checks; focus refreshes at most every80ms except an explicit action.
- Commands retain the exact resident ID. Distance, walls/furnishings, presence and incident ownership are checked again. A lost target cannot silently substitute a nearby person. An untargeted punch hits air.
- Each staff strike has a three-game-minute recovery. Force still records injuries, justification, memories, trust and write-ups under existing rules. No damage-reward loop was added.
- Pat-downs take three game minutes; pocket searches take five. The subject and officer remain occupied. Contraband findings settle exactly once at completion, using existing search-tool/experience odds. Cancellation does not confiscate anything. These are game-clock durations, not real-world timings.
- Ground and cuff first performs the takedown, then applies cuffs. The person remains on the ground with restrained hands. The officer must choose Stand & escort before walking away. Searches work while cuffed. Release is an explicit safe action, including if the subject was displaced.
- Searches and conversations reserve the subject against routine wandering. End of control immediately clears manual custody and pending actions, even while paused. Live encounter timers are deliberately not restored as active manual control in a save.
- New functional poses distinguish alternating strikes from two-handed takedown contact, kneeling cuffs and pat/pocket search hand paths. They use the current articulated rig; Claude's separate human-model corrections are not imported by this milestone.

## Research and design choices

Official references consulted for this pass:

- [Ubisoft: designing Rainbow Six Mobile for mobile play](https://montreal.ubisoft.com/en/how-rainbow-six-mobile-was-designed-for-real-mobile-play/): informed separate touch responsibilities and phone-specific layout checks.
- [Rockstar: playing with perspective in GTAV](https://www.rockstargames.com/newswire/article/25o2411812oa29/rockstar-game-tips-playing-with-perspective-in-gtav): informed keeping perspective and aiming response explicit.
- [Rockstar: Red Dead Redemption2 tap assistance](https://support.rockstargames.com/articles/iHuafuI6D4LVge59s82oh/enabling-the-tap-assist-accessibility-settings-in-red-dead-redemption-2): informed discrete command choices without repeated tap requirements.
- [Unity Input System pointer documentation](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Pointers.html): pointer pixel deltas and rate-based sticks have different time semantics. Existing device response math is retained; capture entry now suppresses its first warp/click frame.

These are design references. Exact target IDs, game-clock staged custody, menu organization and consequence rules are our implementation choices.

## Verification limits

See the milestone entry in COORDINATION.md and committed review evidence for final counts. Editor phone render targets check layout, not physical multitouch, thermal behavior or frame rate. Native iPhone/iPad input and performance still require actual device runs. Procedural current-rig choreography is not production motion capture.
