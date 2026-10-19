# HUD and game-control redesign (draft for critique)

Claude, 2026-10-19. Prompted by the first iPhone 17 Pro run: "the HUD is in the way the entire time."

## Evidence (baseline)

`Editor/HudAudit.cs` (batch, Claude clone) renders 11 HUD states on 4 devices with real safe-area insets and measures
HUD coverage (UI alpha > 50%), centre coverage (middle 60% × 60%), touch targets under 44 pt, overlapping buttons and
buttons outside the safe area. Baseline: `Builds/HudAudit/report.txt`, `audit.json`, PNGs.

| State (iPhone 17 Pro, 874×402 landscape) | Screen covered | Centre covered |
| --- | --- | --- |
| Facility (idle management) | 62% | 73% |
| Build | 66% | 82% |
| People / inspect resident | 58% | 66% |
| Company / Control / Operations | 62% | 73%, 28–32 targets under 44 pt |
| Officer, 3rd / 1st person | 32% | 21% |
| Officer phone | 50% | 62% |
| Officer actions | 75% | 100% |

Portrait (402×874) is worse (76–82% covered, centre 95–100%) and its header overlaps itself (cash under the brand
mark, speed buttons under Save). The idle management view stacks four layers over the world: header (brand, cash,
clock+speeds, Save/Settings/Menu), metrics strip (capacity/stress/risk), alert card + gesture-hint card, and the
six-tab dock; any tab then opens a right panel (~300 pt) as well.

## Principles (from research, see bottom)

1. **World first.** Management games are played by looking at the facility. Idle HUD must be a frame, not a layer.
2. **Show only what is frequent, urgent, or actionable now** (context-driven visibility). Removing information is
   not the goal — moving it to one tap away is (Wandr "minimal HUD trap").
3. **One thing open at a time.** Panels are sheets with detents, never stacked with other panels.
4. **Thumbs own the bottom corners**, eyes own the top. Frequent controls low and at the edges; status high.
5. **Respect the device:** safe areas (Dynamic Island 62 pt landscape sides, home bar), 44 pt minimum targets with
   spacing, no hover, no text walls; controllers hide touch controls.
6. **Speed of action first** (RimWorld Console): every common command ≤ 2 taps from idle; no ornamental animation.

## Proposal

### A. Management idle frame (phone)
- **Status pill, top-left:** cash · day/time · speed (one tap cycles pause → 1× → 2× → 4×; long-press for picker).
  One row, ~40 pt high. Brand mark, "AVAILABLE CASH" labels and Save are removed from the HUD (autosave; Save lives in
  the menu).
- **Pulse chip, beside it:** the three facility metrics collapse into one coloured chip showing the worst one
  ("Stress 32"); tap expands the strip for 5 s.
- **Alerts:** one alert chip, top-centre, with a count. New alerts show as a toast for ~4 s then fold into the chip.
  Tap → the alerts sheet.
- **Menu button, top-right** (settings, save, holdings, guide).
- **Dock, bottom:** six icon tabs, icon-only on phone (label under the selected tab only), ≤ 52 pt tall, centred,
  inside the home-bar inset. Camera/Guide move out of the dock (camera presets into the menu or a long-press on the
  dock; Guide into the menu).
- **Gesture hints** ("drag to pan, pinch to zoom…") appear only in the first session / the Guide, never permanently.
- **Get out of the way while touching the world:** while a pan/pinch/orbit gesture is active the frame fades to
  ~25% and ignores input; it returns on release (Apple Maps style).

Target (audit): idle landscape ≤ 15% covered and ≤ 3% centre; portrait ≤ 20% and ≤ 5%.

### B. Workspaces as sheets
- Tapping a tab opens its workspace as a **sheet**: landscape = right side sheet (≤ 42% width, ≤ 360 pt), portrait =
  bottom sheet. Three detents: **peek** (title + the one most important line/action), **half**, **full**. Swipe or tap
  the world to drop a detent; tap the active tab to close.
- Content inside sheets is re-flowed for 44 pt targets (the Company/Operations 34–40 pt chips become full rows or
  segmented controls).
- When a sheet is open the world centre recentres into the visible area (existing `WorldViewCenter`).

Target: sheet half detent ≤ 45% covered landscape; 0 targets under 44 pt; 0 overlaps.

### C. Build mode
- Catalog as a **bottom tray** of item cards (horizontal scroll, category chips above) in landscape; bottom sheet in
  portrait.
- While placing: the tray drops to a slim bar; **contextual placement buttons** float near the right thumb
  (rotate, snap, confirm, cancel) and the quote is a compact card top-left that expands on tap.

### D. Character (officer) mode
- Top bar becomes a **back chip** top-left (‹ Management) + the name in small type; **Pause** and a "⋯" overflow
  (perspective, phone, settings) top-right. Remove the explanatory subtitle line.
- **Context-sensitive action cluster:** only actions that are valid for the aimed target are shown; one large primary
  contextual button (the best action, e.g. "Pat down" / "Talk") above the run button, with secondary actions in a
  small arc that appears only when there is a target. No greyed-out buttons.
- Prompts (aim hints) become a short top-centre chip that auto-hides after ~3 s or when the player moves.
- Perspective toggle moves to a small button above the look stick.
- Sticks: keep floating MOVE (left) and LOOK (right) but render only rings at rest (no filled discs) to cut cover.
- Actions menu: instead of a full-screen modal, a **radial or right-side action list** anchored near the right thumb,
  ≤ 45% of the screen, world still visible, time still paused while open.

Target: officer idle ≤ 18% covered, ≤ 5% centre; actions open ≤ 45%.

### E. Phone (in-world device)
- Landscape: a phone-shaped panel on the right half; the world stays visible on the left. Portrait: bottom sheet.

### F. Settings & adaptability
- HUD size (existing interface scale), **HUD density: Auto / Compact / Full**, **left-handed** (mirror thumb
  controls), hint visibility, action-cluster opacity.
- Controller connected → touch controls hide, button glyphs show (existing GameGamepad).
- iPad/desktop keep the fuller layout (they audit at 37–42% / ≤ 14% centre already) but share the same components.

## Implementation notes (for the build)
- `GameHud.Layout()` already computes safe insets and a `WorldViewCenter`; the new frame and sheets plug into it.
- HudAudit gains assertions for the targets above so it becomes the regression gate (machine-checked fits).
- Ownership: GameHud.cs / GameController.cs are Claude's; the command dock, home/save shell, operations/security HUD,
  character controls/phone/staff interactions are Codex contracts — needs the user's go-ahead and a COORDINATION
  entry before editing them.

## Research sources
- Mobile game UI (thumb zones, HUD economy, context visibility): wandr.studio/blog/mobile-game-ui-design,
  wandr.studio/blog/game-hud-design, appnality.com/blog/guide-to-mobile-game-ui-ux-design
- Dynamic HUD (RDR2 context meters), diegetic/minimal trade-offs: en.wikipedia.org/wiki/HUD_(video_games)
- Prison Architect: Mobile — every UI aspect reworked for touch, interface shrunk while inputs enlarged, big undo and
  pause: pocketgamer.biz "Breaking out: the making of Prison Architect: Mobile", touchtapplay review
- RimWorld Console Edition — new HUD, speed of action first, context-sensitive menus, few presses to any menu:
  blog.playstation.com 2022-06-29, dualshockers interview
- Apple: 44 pt targets, safe areas, GCVirtualController / hide touch controls with a hardware controller
  (developer.apple.com GameController docs, WWDC21 10081)
- Thumb travel: put the most-pressed controls nearest the thumb rest (keymap.io Genshin control analysis)

## Critique outcome (independent critic agent, same day) — accepted changes

Verified against the code before accepting:
- **Input, not just pixels:** `IsOverUI` treats any picked element as UI, so every card blocks world drags/pinches.
  New audit metric "takes touches over N%" (6 pt grid of `panel.Pick`). Read-only cards become `pickingMode = Ignore`.
- **Orientation:** `GameController.Awake` hard-locked LandscapeLeft. Now landscape auto-rotate (left/right), never
  portrait on iPhone. The audit drops iPhone portrait and adds iPad portrait instead.
- **iOS edges:** system gestures were not deferred (`deferSystemGesturesMode 0`). IosBuild now defers top + bottom
  edges and auto-hides the home bar; signing team set for future exports.
- **Officer entry was frozen:** a paused facility made every action read "Paused · resume time to act". Taking
  direct control now resumes at 1× (restored on return). The actions menu must not rely on pause either.
- **"Idle" baseline had a panel open.** True idle is now its own state: 47% covered / 40% touch-blocking before the
  frame redesign.

Revised specifics:
- Status pill: dedicated Pause/Play 44 pt + one speed button cycling 1×/2×/4× (not a single tap-cycle).
- Metrics chip: worst metric with icon + number, changes only on threshold, tap toggles (no timed collapse).
- Alerts: routine toast (~3 s, folds into the count) vs urgent sticky banner with "Go" (jumps camera, drops sheets).
- No fade-while-touching; a world pan drops an open sheet instead. One Close per sheet, swipe right to dismiss.
- Landscape sheets use widths (peek 220 / standard 360 / full) not height detents; dock hides while a sheet is open
  (tabs move into the sheet header). Sheet-open target ≤ 50%.
- Build: catalog in the side sheet (not a bottom tray); while placing, confirm/cancel bottom-right, rotate/snap
  bottom-left, one-line quote chip, offset placement reticle; global Undo.
- Officer: floating MOVE stick (left 40%), LOOK stick off by default (swipe right half), sprint by pushing past
  the ring (RUN button removed), one 72–80 pt contextual primary button at the right thumb with press-drag radial,
  satellites only when valid, drags starting on buttons become look, tap a person to target, world-anchored target
  ring, haptics (native plugin). Back chip + Pause + ⋯ top bar.
- Management gestures: long-press (~350 ms) to lift a selected item, otherwise one-finger drag always pans; orbit
  only after ~15° twist; tap slop ~10 pt.
- Settings: Compact interface size below 1.0, sprint hold/toggle, haptics, auto-pause on urgent alerts, later a
  layout editor. Audit runs at interface scale 1.0 and 1.3.

Acceptance (phone landscape, enforced by HudAudit): true idle ≤ 12% covered, 0% centre, ≤ 12% touch-blocking;
sheet open ≤ 50%; officer idle ≤ 12% / 0% centre; officer actions ≤ 40%; every state 0 targets under 44 pt and 0
overlaps (sticks and action pad included).
