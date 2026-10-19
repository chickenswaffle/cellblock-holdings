# Movement, combat and restraint realism — joint plan (Claude + Codex)

Owner's direction (2026-10-19): "improve the mechanics of movement — the way people move needs to be better, the way
they turn; when I'm handcuffing or punching someone it should look legit … the fighting, movements, handcuffing,
takedowns, pat-downs, everything in first person should look legit." Also: "I don't like the look-around toggle — it
should be swipe to look around, like pinch to zoom, and the pad for walking … best mechanics, especially on mobile."

## Split (one owner per layer, a data contract between them)

**Codex — what happens (choreography and rules).** The action timeline for every officer action and the resident's
response: phases, timings, positions, contact moments, reactions, cancel windows. Real procedure, not just poses.
**Claude — how it looks and feels (bodies, camera, controls).** Locomotion and turning, the skinned rig and IK that make
hands and feet actually land on the targets Codex publishes, first-person body/camera, impact feel, and the mobile
controls. Claude does not change action rules; Codex does not change the rig, IK, camera or touch input.

### Contract: `ActionChoreo` frames (Codex publishes, Claude consumes)
Per actor in an action, per frame (presentation side, from the action/beat state):
- `Phase` (e.g. `Approach, Setup, Windup, Contact, Control, Apply, Release, Recover`) and `Progress` 0–1.
- `Root`: where this body should stand (ground point + yaw) relative to the partner — e.g. standing rear cuff: officer
  0.45 m behind and 0.15 m to the resident's strong side, facing the resident's back.
- `HandTargets[2]`: a named point on the partner (`wrist_l`, `wrist_r`, `elbow_l/r`, `shoulder_blade`, `small_of_back`,
  `waist_l/r`, `pocket_l/r`, `ankle_l/r`, `head`, `jaw`, `chest`) or a world point, plus `Grip` (0 open … 1 fist) and
  `Weight` (how fully the hand must reach).
- `KneeTarget` (e.g. knee on the shoulder blade during prone cuffing), `LookTarget`, `Lean`/`Crouch` hints.
- `Events`: `Impact(strength, direction, bodyPart)`, `CuffClick(side)`, `Lift`, `Fall(direction)` with exact times.
- The resident's frame carries its own phase (`Brace, Flinch, Stagger, Fall, Prone, Kneel, HandsOnHead, Interlace,
  Cuffed, Rise, Escorted, Resist`) and its own root/hand targets.
Claude's rig moves bodies there with root alignment (motion-warping style: blend the root to `Root` during
`Approach/Setup` so contacts line up, never snap), two-bone IK for arms and legs, and fires feedback on `Events`.

## Research — what shipped games and real procedure do (and what we take)

**Locomotion and turning** (motion matching / For Honor GDC 2015, Clavet & Zadziuk GDC 2016, Bollo inertialization
GDC 2018; foot-lock practice):
- Bodies have **momentum**: accelerate over ~0.25 s and decelerate over ~0.3 s with 1–2 stopping steps, not
  instant velocity. Turning while moving **banks** into the turn; sharp reversals (> 120°) **plant and pivot**.
- **Turn in place** for idle turns > 45°: head leads (0.1 s), then chest, then hips with small stepping feet; under 45°
  only head/upper body twist.
- **Feet lock** while in contact (pin the ankle, then the ball as the heel lifts) — no skating; stride length follows
  actual speed so the cadence matches the ground speed.
- Inertialized transitions between states (a short decaying offset), not long cross-fades that float.

**First person** (Condemned, Dying Light, Mirror's Edge, Ready or Not's arrest animations):
- **Full-body awareness:** in first person you see your own arms and hands doing the action (cuffing, patting,
  punching) — the camera rides the head bone with damped rotation, never floating.
- Melee feel: **anticipation → contact → follow-through**; weighty camera motion tied to the strike; hit reactions
  that read clearly; hit-stop ~50–100 ms on heavy hits, small screen shake (large shakes cause nausea); sound on
  contact (Dying Light, game-feel primers).
- Head bob comes from footsteps (heel strikes), small, and reduced-motion aware.

**Real procedure** (police training handouts and trade articles):
- **Standing rear cuff:** subject faces away, hands behind the back, palms out, thumbs up; officer controls the elbow
  and wrist from behind and to the side, cuffs the first wrist, then the second; double-lock.
- **Prone cuff:** after a takedown, knee on the **shoulder blade** (not the neck), little weight; secure the first
  hand, sweep the lower back, cuff the second; then roll the person onto their side / sit them up (breathing).
- **Kneeling:** hands on the head with fingers interlaced, ankles crossed; officer approaches from behind.
- **Pat-down:** from behind and slightly to the side, subject's fingers interlaced on the head (or small of the back),
  officer's one hand controls the interlaced fingers while the other pats/sweeps in order: collar, arms, armpits,
  chest, waistband, small of the back, pockets, groin line, each leg to the ankles; then switch sides.
- **Escort:** officer behind and to the side, one hand on the elbow/upper arm, one on the cuffs or wrist.
- Takedown: break balance first (pull/turn at the shoulder and elbow, or a level change), guide to the ground, follow
  down — the officer does not fall with them.

**Fighting** (Hard Time III-style, Sifu/Arkham readability, hit-reaction libraries):
- Strikes have windup (0.12–0.2 s), contact on a body part, recovery (0.25–0.35 s); the target reacts in the direction
  of the hit with intensity tiers (flinch → stagger → fall); guards/blocks raise the forearms.
- Distance management: a short step-in for a punch (root warp ≤ 0.35 m) so fists land instead of hitting air.

**Mobile controls** (CoD Mobile, PUBG, Fortnite mobile, critic review):
- **Floating move pad:** spawns where the left thumb lands (left ~40% of the screen); 12% dead zone; push past the
  ring to sprint, push up past a lock arrow to auto-run. No RUN button.
- **Swipe anywhere else to look** (right side and empty screen), **pinch to zoom** the third-person camera,
  double-tap to recentre. No LOOK stick (owner's request).
- Buttons pass drags through to look (a press that moves > ~10 pt becomes look), so action buttons never block the
  camera. One large contextual primary button at the right thumb; others only when valid; tap a person to target.
- Haptics on target lock, cuff click, impact.

## Work items

### Codex
1. `ActionChoreo` contract (above) for: pat-down (both sides), standing rear cuff, kneeling cuff, prone cuff after a
   takedown, takedown (balance break → guide down → follow), escort, strike/counter/shove/grab, resident reactions
   (directional flinch/stagger/fall, resist, comply poses), get-up.
2. Real timings per phase and the target points/relative roots from the procedure research; cancel windows and what
   interrupts what.
3. Keep pause semantics consistent: actions may begin from a paused clock when the player commits (the officer now
   enters direct control at 1×).
4. Keep `StaffEncounterCapture` / `ActionCapture` and add frames that assert contact: officer hand within 3 cm of the
   resident wrist at `CuffClick`, knee within 5 cm of the shoulder blade during prone cuffing, root within 5 cm of the
   choreographed spot at `Contact`.

### Claude
1. **Controls:** remove the LOOK stick; swipe to look; pinch to zoom (third person); floating move pad with
   push-to-sprint; buttons pass drags to look; contextual primary button; tap to target; haptics plugin.
2. **Locomotion/turning:** momentum (accel/decel), banking, plant-and-pivot reversals, turn-in-place sequencing,
   foot locking with no slide, cadence matched to speed, inertialized transitions.
3. **Rig consumption of `ActionChoreo`:** root alignment, two-bone IK for hands/knees/feet onto partner bones, grips,
   look-at; impact feedback (hit-stop, camera kick, sound, directional reaction blend).
4. **First person:** visible own arms/hands from the skinned body during actions, head-bone camera with damping,
   footstep-driven bob, reduced-motion.
5. Review gates: foot slide ≤ 2 cm per planted step, turn-in-place sequence present for > 45°, hand-to-target error at
   contact ≤ 3 cm, first-person frames show the officer's hands in every action.

## Coordination
- Both agents log in `COORDINATION.md`; claim scopes atomically; one Unity runner per checkout (Codex: the Mac
  checkout's editor; Claude: batch in its clone only).
- Claude works on branch `codex/claude-overload` (fast-forwarded to `main`). Codex publishes on its handoff branch;
  each merges the other's published SHA and records it.
- Until `ActionChoreo` lands, Claude builds controls and locomotion (no dependency) and an adapter that reads today's
  beat state so IK work can start; it switches to the contract when Codex publishes it.
