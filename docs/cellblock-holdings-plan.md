# CELLBLOCK HOLDINGS — Build Plan

A tile-based prison management sim where you don't play a warden — you play the **franchise owner**. Build prisons, staff them, keep the population from tearing itself apart, and answer to a board that only reads spreadsheets.

**Stack:** Godot 4.4 · GDScript · 3D, angled orthogonal camera (procedurally generated low-poly geometry — no external 3D assets, everything code-authored) · no external deps. Save via Godot JSON. Tests via GUT (Godot Unit Test) run headless.

**Rendering pivot (post-M1):** M0/M1 shipped as flat top-down 2D (`TileMapLayer` + `Node2D`), per the original stack line below. After playing it, the visual direction changed to true 3D — a Cities Skylines-ish angled camera, procedurally generated terrain/walls/objects, real-time lighting — while keeping the same principle: everything stays code-authored (procedural meshes/shaders as text), no binary 3D asset pipeline. The sim layer was untouched by this; only `scripts/view/` was rewritten. Every "2D"/`TileMapLayer`/`Node2D`/`Camera2D` reference below is historical (M0/M1's original rendering) — current view-layer code is 3D (`MeshInstance3D`, `MultiMeshInstance3D`, `Camera3D`, `.gdshader` files in `assets/shaders/`). See the repo's `CLAUDE.md` for the current architecture.

**Why Godot over Unity/web:** for an agent-built project the axis that matters is *how much of the work is text the agent can author vs. clicks in an editor it can't touch*. Godot scenes (`.tscn`) and resources (`.tres`) are **plain text** — Claude Code authors and diffs them directly. It runs fully headless for CI. That removes the single biggest friction point (Unity's binary scenes) while still being a real engine with a scene tree and tooling. The whole simulation is engine-agnostic pure GDScript anyway, so the engine only owns the thin rendering layer.

---

## 0. How to use this doc with Claude Code

Read this first. It's the difference between a good session and a bad one.

1. **Everything is text.** `.tscn`, `.tres`, `.gd`, `project.godot` — all plain text Claude Code can write and diff. No binary asset traps. This is why we're on Godot.
2. **Sim layer is pure GDScript with zero engine calls.** No `Node`, no `get_tree()`, no `_process`, no `randf()`, no Godot signals. The sim is plain `RefCounted` classes with its own seeded RNG. This keeps it deterministic, testable headless, and trivially savable. **Enforce it:** if a file under `sim/` references a Node, a signal, or `Engine.`, reject it.
3. **The sim must be testable headless.** Every milestone has GUT tests. Claude Code runs them itself:
   ```
   godot --headless --path . -s addons/gut/gut_cmdln.gd -gexit -gdir=res://tests
   ```
   Put that in `run-tests.sh`. Tell Claude Code to run it after every change. This is the single highest-leverage instruction in this document.
4. **Prefer code-built scenes over the editor.** The main scene is one root node with `bootstrap.gd` that instantiates cameras, renderers, and UI at runtime. Small reusable visual scenes (an agent sprite, a tool cursor) can be authored as `.tscn` since Godot's are text — but don't build large hand-wired scene graphs. Keep wiring in code.
5. **Use `class_name` everywhere.** Stable type references across files without scene wiring, and clean tests.
6. **Content is `.tres` Resources or JSON**, loaded at runtime — not hardcoded. Object defs, faction templates, contracts live in `content/`.
7. **Work one milestone per session.** Paste the milestone + the architecture section. Don't paste the whole doc every time.

---

## 1. The pitch

Prison Architect asks "can you run a prison?" This asks **"can you run twelve of them, on a contract that pays per head per night, while the board asks why Site 4's incident rate is up?"**

You start with one leased facility and a state contract. You expand to a portfolio. Every prison you own is a live tile-based sim you can drop into and micromanage — but you can only be in one place at a time, and the ones you're not watching keep simulating.

**The hook:** the two layers fight each other. The franchise layer rewards throughput, occupancy, and cost-per-inmate. The floor layer rewards patience, space, staffing, and programs. Corporate metrics are *legible*; the reasons your prison is about to explode are *not*. That gap is the game.

### Design pillars
1. **Two clocks.** The floor runs in minutes. The franchise runs in quarters. Decisions made on one clock detonate on the other.
2. **Tension is a resource you spend.** You can always squeeze more margin out of a facility. It always costs you something you can't see on the dashboard.
3. **Legibility is the enemy.** The board sees numbers. You see people. Never let the UI collapse those into the same view.
4. **No villain framing, no lecture.** The system's incentives are the antagonist. Let the player discover the math themselves and decide how they feel about it.

---

## 2. Core loop

**Floor loop (seconds–minutes):**
zone & build → assign staff → prisoners run their schedule → needs decay → grievances form → incident or no incident → adjust

**Franchise loop (10–20 min per quarter):**
take contract → allocate capital → set facility policy → quarterly report → board reaction → new pressure → repeat

**Session loop (1–2 hrs):**
acquire site → stabilize it → make it profitable → get pushed to over-extend → something breaks somewhere else in the portfolio

---

## 3. Systems

### 3.1 Grid & building
- World is a `Grid` holding a flat array of `Tile`, one tile ≈ 1m. Default site: 100×100, expandable by land purchase.
- `Tile { floor_type, wall_flags, room_id, is_outdoor }`
- Walls live **on tile edges**, not on tiles. `wall_flags` is a 4-bit N/E/S/W mask. Makes doors, sightlines, and room sealing sane.
- **Rooms are discovered, not declared.** Flood-fill from any tile, stopping at walls/doors → contiguous region. Player then *zones* a region as Cell / Canteen / Yard / Workshop / Solitary / Medical / Staff Room / Visitation. A zone is valid only if the region is sealed and contains required objects (a Cell needs a bed + toilet).
- Objects (`bed`, `toilet`, `table`, `bench`, `phone`, `weight_bench`, `sewing_station`, `cctv`, `metal_detector`) occupy tiles and carry usage slots.
- Build actions go into a `ConstructionQueue` executed by worker agents over time — not instantly. Money is deducted on completion, not order.

Rendering note: use `TileMapLayer` for floors/walls (Godot 4.3+ replaced the old `TileMap`). Agents are pooled `Node2D`s driven by sim positions, **not** physics bodies.

### 3.2 Pathfinding
- A* on the tile grid, 8-way, with edge-wall blocking (can't cut corners through a wall seam).
- **Roll your own A\*** over the sim grid — do *not* use `AStarGrid2D`. Pathfinding must live in the pure sim layer and stay deterministic and testable; the engine class would drag `Node`/RNG into `sim/`.
- Cost modifiers: doors cost more, `Restricted` zones impassable to prisoners unless escorted.
- **Cache aggressively.** Precompute a room-connectivity graph; path between rooms hierarchically, then locally. With 300 agents you cannot brute-force full-grid A* per agent per tick.
- Invalidate paths on wall/door change via a `grid_version` counter — don't event-spam.

### 3.3 Prisoners
```gdscript
class_name Prisoner extends RefCounted
var id: int
var pname: String
var age: int
var needs: Needs            # hunger, sleep, hygiene, social, recreation, safety, dignity
var traits: int             # bitflags: VOLATILE, CUNNING, INSTITUTIONALIZED, FRAIL, CONNECTED, PENITENT
var faction_id: int = -1    # -1 = unaffiliated
var sentence_days: int
var reform: float = 0.0     # 0..1
var grievance: float = 0.0  # 0..1  <- the important one
var cell_id: int
var grudges: Array[int]     # prisoner ids
var contraband: Array
```
- **Needs** decay per tick, satisfied by objects/rooms during the right schedule block. Unmet needs feed `grievance`.
- **Grievance is the master variable.** Rises from unmet needs, humiliation (searched, cell-tossed, solitary), witnessing violence, and grudges. Decays slowly with satisfied needs and programs. It's *invisible on the corporate dashboard* — the player reads it from body language / an optional overlay.
- **Reform** rises in programs (education, workshop, therapy), falls in solitary and high-tension blocks. Reform reduces recidivism → a long-term franchise stat nobody on the board cares about until an inspector does.

### 3.4 Schedule
A 24-slot day, per-facility, editable: `Sleep / Eat / Work / Yard / Free / Shower / Lockup / Program`.
Every prisoner runs a small **utility-scored action selection** within their block — not a rigid FSM. Score candidate actions by `need_deficit × availability × distance`, pick top with a little noise. Gives emergent queuing and "prisoner wanders off to do something dumb" behavior for free.

### 3.5 Staff
- **Guards** — patrol routes, respond to incidents, escort, search. Have `fatigue` and `nerve`. Overworked guards escalate faster and search more aggressively → more grievance. Intentional feedback loop.
- **Workers** — execute `ConstructionQueue`, repairs, cleaning.
- **Support** — cook, doctor, teacher, counselor, janitor.
- **Wardens** (franchise layer) — one per facility, an NPC with traits (`Hardliner`, `Reformer`, `Grifter`, `Competent`). They run the site autonomously per their traits + your policy when you're away. **A Hardliner left alone drives grievance up and incidents down — until it inverts.**
- Staff cost salary per day. Salary is the biggest lever on margin, which is the whole trap.

### 3.6 Conflict — the centerpiece

**Factions.** 3–5 per facility, procedurally named, with `strength`, `heat`, `territory` (roomIds), and `relations` toward each other (−1..1). Prisoners join based on traits + vulnerability + time served. Unaffiliated prisoners are prey; factions offer `safety` but demand participation in contraband and violence.

**Tension model.** Every room has `tension = f(overcrowding, avg grievance, faction adjacency, guard presence, recent violence)`, diffusing to adjacent rooms per tick like heat propagation. A **Tension overlay** toggle is the most important screen in the game.

**Incident ladder**, escalating, each step feeding the next:
```
Grudge → Verbal → Shove → Fight (2) → Brawl (n) → Weapon → Stabbing
       → Faction War → Block Riot → Facility Riot → Hostage → Lockdown
```
Resolution options with real trade-offs:
- **Separate & transfer** — cheap, fast, kicks the problem to another facility in *your own portfolio*.
- **Solitary** — kills it now, spikes grievance, destroys reform, generates an oversight flag if overused.
- **Negotiate** — costs time, needs a counselor, lowers grievance for real.
- **Force** — guards wade in. Fast, injuries, lawsuits, facility-wide grievance spike.
- **Concede** — give the faction what it wants (better food, more yard). Works. Board notices the cost line.

**Contraband.** Enters via visitation, deliveries, corrupt staff. Flows along a supply graph. Feeds faction `strength` and weapon availability. Searches suppress it and raise grievance. No search rate is both safe and calm — that's the point.

**Snitches.** Recruit an informant: reveals contraband + upcoming incidents. Discovery probability rises with use; if caught they're killed and their faction's `heat` explodes. High-information, high-cost.

### 3.7 Franchise layer
- **HQ map** — regional map; sites you own or can acquire (lease, buy, build-from-dirt), each an at-a-glance card: occupancy, margin, incident rate, oversight risk.
- **Contracts** — the state pays `per_diem × occupancy`. Contracts specify min occupancy (penalized for *empty beds* — the single most incentive-corrupting rule in the game), max incident rate, inspection schedule. Specialized: max-security (higher per diem, worse population), immigration detention, work-release (low per diem, sell workshop output).
- **The Board** — quarterly objectives (`raise margin 4%`, `open a site in Region 3`, `cut incident rate 15%`). Meeting them grants capital & confidence; missing costs confidence. Confidence hits zero → you're out. The board *never* asks about reform, until:
- **Oversight** — a hidden `scrutiny` stat rising with deaths, solitary overuse, lawsuits, journalist attention. High scrutiny → surprise inspection → fines, contract loss, or a **consent decree** that hard-caps your policy levers. The counterweight to pure optimization.
- **Background sim.** Facilities you're not in run on an abstracted model (~1/50th cost) driven by warden traits + policy + staffing ratios. **Rule: abstraction must be calibrated against the full sim.** Test: run both for 30 sim-days on the same seed, assert outcomes within tolerance. If they diverge, the game lies to the player and the meta-layer collapses.
- **Events** — journalist investigation, staff union action, bad food batch, celebrity inmate, state budget cut halving per-diem.

---

## 4. Architecture

```
res://
  project.godot
  main.tscn                       # one root node + bootstrap.gd
  scripts/
    sim/                          # PURE GDScript. RefCounted only. No Node, no engine RNG, no signals.
      core/    sim_world.gd  sim_clock.gd  rng.gd (seeded xorshift)  event_bus.gd
      grid/    grid.gd  tile.gd  wall_flags.gd  room_detector.gd  pathfinder.gd
      agents/  prisoner.gd  staff.gd  warden.gd  need_system.gd  schedule_system.gd  utility_ai.gd
      conflict/ faction.gd  tension_field.gd  incident_system.gd  contraband_system.gd  snitch.gd
      build/   build_order.gd  construction_queue.gd  object_def.gd  zone_validator.gd
      economy/ ledger.gd  contract.gd  payroll.gd
      franchise/ portfolio.gd  facility.gd  abstract_sim.gd  board.gd  oversight.gd  event_deck.gd
      save/    save_game.gd
    view/                         # Presentation only. Reads sim, never writes.
      bootstrap.gd  camera_rig.gd  tilemap_renderer.gd  agent_renderer.gd  overlay_renderer.gd
      input/   build_tool.gd  select_tool.gd  zone_tool.gd
      ui/      hud.gd  facility_panel.gd  prisoner_panel.gd  hq_screen.gd  board_screen.gd
  content/                        # .tres / .json defs
  tests/                          # GUT test scripts — the real deliverable each milestone
  addons/gut/
```

**Non-negotiables:**
- `SimWorld.tick()` is the only mutation entry point. The view calls it; nothing else mutates state.
- Fixed timestep: **10 ticks/sec sim time**. Speed controls (pause/1×/3×/10×) change *ticks per frame* in the view, never tick size. Determinism depends on this. Drive it from `bootstrap.gd`'s `_process`, accumulating real time into fixed sim ticks — the sim never sees `delta`.
- All randomness through `SimWorld.rng` (your own seeded xorshift), seeded from the save. `randf()`/`randi()`/`RandomNumberGenerator` are **banned in `sim/`**.
- Communication out of the sim is via `event_bus.gd` — a plain observer list, **not** Godot signals (signals need Nodes). View subscribes; renderers react. No polling every agent every frame.
- Save = serialize `SimWorld` to a Dictionary → JSON. If it isn't reachable from `SimWorld`, it doesn't exist. Falls out for free if rule #1 holds.

---

## 5. Milestones

Each is one Claude Code session. Each ends green tests + something visible on screen.

### M0 — Skeleton *(the foundation; do not rush)*
- Godot 4.4 project, GUT installed under `addons/`, folder structure above.
- `sim_world.gd` + `sim_clock.gd` + seeded `rng.gd` + `event_bus.gd`.
- `grid.gd`/`tile.gd`; `bootstrap.gd` spawning a `Camera2D` with pan/zoom/edge-scroll.
- `tilemap_renderer.gd` drawing a flat grid via `TileMapLayer` with placeholder tiles.
- `run-tests.sh` + headless GUT working.
- **DoD:** `SimWorld` ticks 1000× headless, deterministically (same seed → identical state hash). A grid renders. `./run-tests.sh` runs green from CLI.

### M1 — Build mode
- Wall/floor/door placement w/ drag rectangles, edge-based walls, cost preview.
- `room_detector.gd` flood fill → room_ids, re-run on change.
- Zone tool + `zone_validator.gd` (required objects per zone type).
- Object placement, `ledger.gd` with balance & transactions.
- **DoD:** Draw a sealed box with a door → detected as one room → zone it a Cell → bed + toilet makes it valid. Tests cover flood-fill against 5 fixture layouts, including nested rooms and a door-in-corner edge case.

### M2 — Prisoners
- `prisoner.gd` + `need_system.gd` decay + trait generation.
- A* with edge-wall blocking + room-graph hierarchy.
- Schedule system + utility action selection.
- Intake: prisoners arrive by bus, get assigned cells.
- Prisoner inspection panel.
- **DoD:** 50 prisoners run a full day/night cycle — sleep in beds, eat in canteen, don't clip walls. Perf: 200 agents at 10 ticks/sec under 8ms/tick (measure headless).

**M2 shipped 2026-07-18.** Measured 0.75ms/tick average with 200 agents (10x under budget) with a straightforward single-level A* — the room-graph hierarchical pathfinding this section originally called for wasn't needed at this scale; revisit only if a later milestone's profiling says otherwise. Scope simplifications, deliberate: WORK and PROGRAM schedule blocks are stand-ins for SOCIAL/RECREATION/HYGIENE (same as FREE) since job assignment and reform programs are M3/M5 territory; SHOWER restores hygiene while in-cell since no dedicated shower object/zone is defined anywhere in the doc. Both are easy to special-case later without touching the schedule system itself.

### M3 — Staff & jobs
- Guards (patrol, respond, escort, search), Workers (build queue), Support roles.
- `payroll.gd`, hiring UI, fatigue.
- Construction consumes worker-time.
- **DoD:** Queued buildings get built by workers over time. Guards patrol routes. Payroll debits daily.

**M3 shipped 2026-07-19.** All three DoD clauses have dedicated tests (`test_staff_construction_dod.gd`, `test_guard_patrol.gd`, `test_payroll.gd`). Notes on what was built versus what this section asked for:

- `ConstructionQueue` stopped being a FIFO countdown and became a **claimable work pool** measured in worker-ticks. Orders are still handed out oldest-first (the player queues in the order they want things built), but N workers burn down N orders concurrently. An order nobody can path to is parked under a `BLOCKED_CLAIM` sentinel and freed again the next time the grid changes, so workers don't spin on unreachable sites.
- Movement moved up into a shared `SimAgent` base (pos/path/`step_along_path`/tile-center convention); `Prisoner` and `Staff` both extend it, which is what stopped M2's tile-center bug from being reintroduced a second time in the staff renderer.
- **Guard scope, deliberate:** patrol only. Respond/escort/search are all reactions to incidents, and there are no incidents until M4 — building them now would mean building them against an imagined interface. What M4 needs from M3 is already exposed: `SimWorld.guard_presence(room)` and `Staff.effective_nerve()` (base nerve eroded by fatigue).
- **Patrol routes are derived, not authored** — one waypoint per sealed, zoned room in `StaffAI.PATROL_ZONES` priority order, re-derived on any layout change. A patrol editor is UI work that belongs with M7's UI pass, and an auto-route is honest in the meantime.
- **Support staff** needed a real reason to exist rather than an inert third hire button, so they work the canteen: a canteen with a support staffer in it serves meals 1.6× faster (`NeedSystem.STAFFED_CANTEEN_MULTIPLIER`). Medical/programs work is M5's.
- **Payroll is all-or-nothing** — it never part-pays and never overdraws. A missed day banks an unpaid day on every staffer; three of those and they quit. That's the intended teeth on the "salary is the biggest lever on margin" trap.
- Fatigue is the shared feedback loop: it slows movement and build rate, erodes nerve, and sends anyone past 85% to a staff room. A site with no staff room still lets them rest, just at half the recovery rate — the fix is to build one.

### M4 — Conflict *(the game shows up here)*
- Factions + recruitment + territory + relations.
- `tension_field.gd` w/ diffusion + the tension overlay.
- Full incident ladder + resolution options.
- Contraband supply graph + searches + snitches. Lockdown.
- **DoD:** An understaffed, overcrowded prison reliably riots within 5 sim-days; a well-run one doesn't. Both true across 20 seeds — write that as a test.

**M4 shipped 2026-07-19.** DoD holds across all 20 seeds in both directions (`tests/slow/test_riot_dod.gd`), with a third test asserting the two scenarios diverge *for the right reason* — the failing prison must actually get tense and aggrieved, or "the well-run one doesn't riot" would pass for free.

The causal chain, in the fixed order it runs each sim minute: conditions → `GrievanceSystem` → `TensionField` → `IncidentSystem`. Changing that order changes the game, not just the frame something lands on.

Notes on what the build taught us:

- **The tension field's sink bug is the one to remember.** Diffusion originally ran across every adjacency, including the unsealed outdoors — which touches nearly every room and holds no tension of its own. Every room drained into it and the whole field pinned itself at `RESPONSE_RATE / DIFFUSION_RATE` (~0.09) no matter how catastrophic conditions got. Local pressure was reading 0.54 while actual tension sat at 0.087, and *nothing* ever sparked. Tension now only moves between built, sealed rooms. The ratio of those two constants is also what sets how far apart connected rooms can sit (~0.37 with current values); raise diffusion and the facility homogenises into one meaningless number.
- **The ladder is a graph, not a line.** The design doc draws it on two rows and that turns out to be load-bearing. Gating the weapon rungs behind contraband made riots impossible in any facility without a visitation room — which would have silently broken this DoD. A brawl with no knives in the block now escalates straight to faction war or block riot; the weapon rungs are a *worse detour* taken when contraband is around, not a toll gate.
- **Room ids are not stable.** `RoomDetector` renumbers in scan order, so building one wall renumbers half the map. Anything persisting per-room state (tension, faction territory, contraband stashes) keys off `RoomInfo.key()` — the room's topmost-leftmost tile — instead. A room that genuinely splits or merges gets a new key, which is the honest answer.
- **Guard presence is the DoD's load-bearing number.** `TensionField.GUARD_CALM` is what makes staffing buy calm; without it "understaffed prisons riot" is a coin flip rather than a mechanic. Guards also damp escalation multiplicatively and drive de-escalation, so the same lever reads at three points on the chain.
- **Support staff got the negotiation monopoly.** Negotiating is the only resolution that lowers grievance for real, and it requires a support staffer on duty — so it's unavailable exactly when the player has cut costs to the bone. That's the intended trap, and it also gives the M3 support role a second reason to exist.
- **Deferred deliberately:** hostage situations exist as a rung but nothing drives them yet (they want a negotiation minigame, which is UI work); "escort" and "search" guard behaviours are player/automatic actions rather than autonomous guard AI; and the incident UI acts on the *worst* open incident because there's no selection UI until M7.
- **Perf:** the conflict layer is cheap (tension ~70µs/sim-minute, incidents ~8µs); the cost of the DoD test is M2's A*, which scales with map area. Hence the compact purpose-built facility in that test, and `tests/slow/`.

### M5 — Franchise
- `portfolio.gd`, HQ map, site acquisition, contracts.
- `abstract_sim.gd` for background facilities + **the calibration test** (§3.7).
- Wardens with traits running sites autonomously.
- Facility policy sliders (food quality, search rate, cell density, program budget).
- **DoD:** Own 3 facilities, be inside 1, time-skip 30 days, the other 2 produce plausible outcomes matching their warden's traits. Calibration test within tolerance.

### M6 — Board & oversight
- Quarterly report screen, board objectives, confidence.
- `scrutiny`, inspections, fines, lawsuits, consent decrees. `event_deck.gd`.
- **DoD:** A full quarter resolves with a report. A max-scrutiny run triggers an inspection and a consent decree that measurably restricts a policy lever.

### M7 — Save/load + UI pass
- Full serialize/deserialize round-trip. Autosave.
- UI pass on every screen, tooltips, overlays (tension / needs / faction / staffing).
- **DoD:** Save mid-riot, quit, load → identical state, riot resumes, and 100 further ticks hash-match an uninterrupted run. This test finds every bug you've been ignoring.

### M8 — Content, balance, juice
- 3 starting scenarios: *The Lease* (one site, tutorial), *The Turnaround* (inherit a failing site), *The Portfolio* (5 sites, hard).
- Real art swap, SFX, ambient audio, screen shake on riots, particle smoke.
- Balance pass against telemetry from your own playtests.

---

## 6. Cut list (say no to these)
- Multi-floor. Kills pathfinding, doubles UI, adds ~nothing at prototype stage.
- Escape tunnels / digging. Beloved but it's a whole second game.
- Deep individual prisoner backstories. Traits + emergence carry it.
- Multiplayer. Obviously.
- Modding / Workshop. Post-1.0 or never.

## 7. Known risks

| Risk | Mitigation |
|---|---|
| Pathfinding perf collapses at 300 agents | Hierarchical A* from M2, not bolted on later. Profile at M2's DoD. |
| Abstract sim doesn't match real sim → meta-layer feels fake | Calibration test in M5. Non-negotiable. |
| Tension model is a black box the player can't read | The overlay ships *with* the model in M4, not after. |
| Franchise layer is just a menu between the real game | M5 must include a decision that makes you *lose* on the floor. If it doesn't, cut the layer. |
| GDScript static-typing gaps let bugs through | `class_name` + typed vars everywhere; run tests every change. |
| Claude reaches for Godot 3 APIs | Pin to 4.4: `TileMapLayer` not `TileMap`, own A* not `AStarGrid2D`, observer list not signals in sim. |

## 8. First session prompt

> Read `cellblock-holdings-plan.md`. Implement **M0 only**. Do not start M1.
> Target Godot 4.4, GDScript. Constraints: everything under `sim/` is pure GDScript (`RefCounted`, no `Node`, no Godot RNG, no signals — use `event_bus.gd`'s observer list). All randomness via `SimWorld.rng`. The sim never sees `delta`.
> Install GUT under `addons/`. Finish by running `./run-tests.sh` and showing me green, including the determinism test (same seed × 1000 ticks → identical state hash).
