# Ride-along and character control: plan

The user asked to "control any character and view them in first person or third person, like an actual video game", while keeping the management view for building and letting players switch between them. A read-only critic reviewed the idea against the code first (2026-10-16). Verdict: **build-reduced**. Ship a watch-only ride-along now, gate control on core hooks, and hold first person and prisoner control until access control and closer-range art exist.

## What the critic found

- **Clock.** At 1× one game minute is 0.2 s and residents walk 0.32 m per minute (1.6 m/s), so 1× is the only speed at human pace. Control must force 1×. Pause freezes the avatar, and nothing moves at Speed 0 (otherwise pausing is a free teleport).
- **Authority.** The view should propose movement and the core should validate it: `Routes.Clear(old, new)` and at most the role's speed × 1 minute, with soft correction. Control state stays runtime-only and saves store a legal position.
- **Movement has four owners.** `MoveResidents`, `AdvanceStaff`/`Walk`, Justice custody movement, and Personnel `Direct`. Each needs a skip hook for a controlled person. On release, clear route caches and re-plan now. Needs keep ticking; legal events (court call-out, boarding, release, lockdown) end control.
- **Exploits.**
  - Doors are permanent openings and the perimeter is visual, so a controlled prisoner could walk into the armory or the parking lot. Prisoner control stays off until doors and zones have access rules.
  - Controlled interceptions and fight separations must not farm officer ratings.
  - Every player action must be the same core method the AI calls, with the same duration and success roll. No x-ray view.
- **Camera.** Today's rig cannot go closer than about 17 m, and walls are 1.15 m (cutaway) or 3.05 m with no ceilings, so an eye camera sees sky indoors. Switching wall height rebuilds the site. The first camera should be a high follow boom above the cutaway tops, with the build camera's state saved and restored exactly.
- **Input.** The touch code treats any two fingers as pan/pinch/twist. Control mode needs its own finger-ID input branch, WASD for movement instead of pan, and Esc to exit. On a phone, "first person" should be a standing look-around, not two-thumb walking in a 2.5 m cell.
- **Art.** Heads had no eyes, mouth or ears; hands were ellipsoids; textures were 128 px. Close views need faces, hair shapes, door leaves, ceilings and better textures first. A small low-frame-rate body-cam monitor, built like the CCTV feed, suits the setting and hides the primitive art.
- **Purpose.** It is a gimmick unless it feeds decisions. The useful versions are a ride-along "day in the life" (why is this line long? why did this officer lose his watch?), a body cam exposing CCTV blind spots, internal-affairs surveillance of a suspect officer, and an officer chasing an escapee yourself.

## Phase 1 — shipped (watch-only)

`Runtime/GameRideAlong.cs` (GameController partial), with the HUD card in `Runtime/GamePressureHud.cs`:

- **Ride along** on any person card, resident or staff. It follows with a boom 4.5–14 m back at a pitch of 32–62°, always above cutaway walls. Drag or Q/E orbits; scroll or pinch changes the boom.
- **Body cam** monitor: first person at eye height, a little ahead of the face, rendered into a 384×216 texture at about 8 Hz. It can be toggled.
- **People:** Previous/Next and Tab cycle through people on site. Exit or Esc restores the exact build camera (focus, distance, yaw, pitch, plan view).
- The ride ends with a notice when the person leaves the site (court, transport, release). New companies and loads end it silently.
- No core changes: the simulation is not touched, so speed, saves and determinism are unaffected.

Unity checks requested from Codex: listed in `docs/pressure-notes.md` (item 10). The critic's additional acceptance criteria for phase 1:

- Enter and exit 20 times from Inspect, plan view and full height: camera state is restored within 1e-3 and `GeometryRevision` does not change.
- At 4× there are no frame-to-frame jumps over 0.5 m.
- The boom stays at least 1.5 m above the floor at every yaw.
- The card and monitor fit inside the safe area at iPhone landscape and 900×420.

## Phase 2 — officer control, third person (needs hooks)

Core: a new `Core/Possession.cs` (World partial) holding a runtime-only controlled id and a per-minute validated proposal.

- **Hooks:**
  - `partial void ControlledResident(...)` in `MoveResidents` (Claude's file).
  - A skip in `AdvanceStaff`; escape-candidate exclusion and custody-driven exit in Justice (Codex).
  - `FacilityView.TryGetPersonPose` (position and facing) plus a way to hide the head (Codex).
  - Personnel `DirectStaff` and the rating metrics skip owner-directed minutes (Claude).
- **Rules:**
  - Allowed only for an officer on shift; never for commuting, off-shift, absent or intake people.
  - Forced to 1×.
  - Actions arrive with their systems: pat search, cell shakedown, separate a fight, recover a drop, escort.
- **Core checks:**
  - 10,000 random steering proposals never cross a wall or exceed the speed cap.
  - No movement at Speed 0.
  - A controlled interception counts once and is excluded from ratings.
  - A court call-out ends control and the 09:05 boarding still happens.
  - After release the AI re-plans within a minute.
  - Save/load mid-control leaves no control and an unchanged format.
  - Roles that cannot be controlled are rejected.
- **Unity checks:**
  - Walk into straight, diagonal and curved walls and through a door on a curve at 10 angles: no penetration and no sticking over 0.2 s.
  - Joystick plus look never pans, zooms or orbits the build camera.
  - Touches starting on the HUD never move the avatar.
  - iPhone 17 Pro frame time ≤16.7 ms over 10 minutes.

## Phase 3 — officer actions

Each action ships with the system it uses: contraband searches (now available in the core as cell checks, shakedowns, pat searches and drop recovery), fight separation (needs fights with a duration), escorts (Justice), and drone spotting. Parity check: the AI and the player call the same method, and success rates match across seeds.

## Phase 4 — first-person look view (standing)

Needs ceilings, door leaves and cell fronts, faces (resident faces started in `FacilityLivesView.cs`; staff need a hook), and textures of 512 px or more. Checks: no near-plane clipping in a 2 × 2.5 m cell at any yaw, and frame time measured on the devices.

## Phase 5 — security drill (optional)

Control a resident to find blind spots, only after doors and zones have access rules. It never releases anyone, and its output is a list of blind spots.
