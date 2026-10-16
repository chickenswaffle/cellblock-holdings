# Cellblock Holdings — active development instructions

The active game is a Unity/C# reboot. Read `README.md` and `docs/reboot-vision.md` before changing the design. The root `Assets`, `Packages`, and `ProjectSettings` form the Unity project.

## Current milestone

Prove freeform, editable construction and company consequences on iPhone, iPad, and Mac. The user requires straight/diagonal/curved structures, editable connected prefabs, hired construction crews, contract-led starts, and equal builder/company importance. Windows is conditional on practical, measured support. Time pauses when the app closes; support pause and faster speeds during play.

## Implementation rules

- Keep engine-independent simulation under `Assets/Cellblock/Core`; it must compile without Unity references.
- Treat approved/completed geometry as authoritative. Drafts and construction plans must not become usable space before handover.
- Derive walls, openings, rooms, floors, and routing from the same building data. Keep door arc-length semantics consistent on curves.
- Preserve persistent identities and resident assignments when modifying prefabs or furnishings.
- Use integer cents and a reconciled ledger. Approval revalidates a quote; loading and resuming must not purchase materials twice.
- Keep the simulation on an explicit game clock. Never progress a closed/suspended game using elapsed real time.
- Preserve saves and user work. Do not overwrite another company's slot or editor assets blindly.
- Keep touch buttons stable during refresh. Respect safe areas and avoid interactions that place objects through UI.
- Retain the material/shader used by player builds; editor-only `Shader.Find` success is insufficient.
- Do not treat concept art or a desktop render as measured phone fidelity. Use actual device runs before stating population/frame-rate support.

## Validation

Run `bash Tools/check-core.sh` for relevant core changes. Unity source also has an optional Roslyn syntax check described in the README. A syntax parse does not compile Unity API calls.

Use an actual Unity 6 editor for imports, typechecking, preparation, and play checks. `bash Tools/open-unity-mac.sh --batch-check` prepares and validates generated assets. Do not claim that command ran if the task only has a Linux terminal and no editor.

Initial device references: iPhone 17 Pro, iPad Pro (generation still to identify), M4 MacBook Air. The user subsequently reports Unity installed on the current Mac, described as a Mac Pro; do not assume that is the M4 Air or that cloud tools can access it.

Use the existing checkout. A cloud task is already isolated; do not create a Git worktree unless the user requests one.
