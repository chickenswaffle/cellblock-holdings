# Looking around in direct control (Claude, 2026-10-18)

User request: first person is good, but looking around should be easier and make more sense, in the spirit of
MDickie's Hard Time III, and it has to work for people on a computer, a controller or a phone.

## Every device

| | Mouse & keyboard | Controller | Touch |
| --- | --- | --- | --- |
| Move | WASD / arrows | Left stick | MOVE stick |
| Look | Mouse, no button held (cursor captured) | Right stick (dead zone + curve) | Swipe the right side, or the LOOK stick |
| Strike / grab | F or click / G or right-click | X / B (Square / Circle) | Round buttons |
| Pick up / use | Q / E | Y / A (Triangle / Cross) | Round buttons |
| Run | Hold Shift, R toggles | Hold LT, L3 toggles | RUN |
| Camera behind you | C | R3 | Double-tap the look side |
| Face the nearest prisoner | (aim with the mouse) | Hold LB | — |
| First / third person | V | RB | Top bar |
| Phone / pause / back | P / Space / Esc | Select / Start / D-pad left | Top bar |

- **Cursor capture.** A desktop session starts with the mouse captured for looking. Esc (or holding Alt) frees it for the
  on-screen buttons; a click on the world takes it back; a second Esc returns to management. The phone, menus, pause and
  leaving the view always free it.
- **Device switching** is automatic: whatever you last touched (a key or the mouse, a controller, the screen) decides the
  help line, the key captions on the action pad and top bar, and whether the touch sticks show. The touch sticks hide
  while a controller or the captured mouse is in use.
- **Third-person follow.** With a stick or touch, walking sideways swings the camera round behind you (up to 42°/s);
  walking straight on or back toward the camera leaves it alone, and any manual look pauses it for 0.8 s. The mouse never
  fights you. Settings → Controls → Third-person camera: Follow / Free.
- **Chase view** is lower and closer than before (pitch 20°, boom 4.6 m, was 29° / 5.6 m), so it shows what is ahead.
- **Aiming dot** in eye view; it turns amber when someone is in reach.
- **Settings → Controls → Playing as staff:** look speed (Gentle / Standard / Fast), vertical look (Normal / Inverted),
  third-person camera (Follow / Free). New preference fields default safely for older preference files.
- **Touch swipes are in points** (pixels ÷ dpi/160), so a 3x iPhone and a 1x iPad or monitor turn the same amount.

## Engineering

- Controllers use the Input System package (1.20.0, bundled with the editor); `activeInputHandler` is **Both**, so all the
  existing `UnityEngine.Input` touch, mouse and keyboard code is unchanged. `ProjectSetup` now enforces Both instead of
  legacy-only. Gamepad code is behind `ENABLE_INPUT_SYSTEM`.
- New `Runtime/GameLookInput.cs`: `LookResponse` (pure stick/swipe/follow maths), device tracking, cursor capture,
  controller buttons, recenter, face-nearest and the help text. Hooks into `CharacterInputUpdate` are listed in
  COORDINATION.md.
- Review: `Editor/LookCapture.cs` drives a virtual controller through the Input System in Play mode (right stick look,
  left stick walk, X strike, LB face-nearest, third-person follow), checks the response maths, the cursor capture and
  release, and frames keyboard, controller, 900×420 touch and the Controls settings page into `Builds/LookReview/`.

## Not yet

- Controllers do not drive the management game or menus yet (only direct control).
- Real-device checks: an actual controller on Mac/iPhone, and touch feel on an actual phone.
- The interaction context line (Codex's `CharacterInteractionHint`) still says "E / Interact" on a controller.
