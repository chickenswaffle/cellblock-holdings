# Continue on the Mac

Continue the Unity implementation in `chickenswaffle/cellblock-holdings` using the installed editor. The user has authorized local development tools, launching Unity, fixing compilation/import problems, and running the game.

## Get the current source

For the active parallel handoff use `origin/codex/live-operations-handoff`; read `COORDINATION.md` and `docs/claude-coordination-update.md` first. The Unity reboot began on `origin/main`. Fetch remote updates from `https://github.com/chickenswaffle/cellblock-holdings.git`. Inspect the local checkout and its changes before updating it; preserve unrelated user work. Do not reset a dirty checkout or assume the cloud machine can synchronize the Mac automatically.

The previously identified local project location is `/Users/trading/Documents/ChatGPT/Cellblock Holdings`; inspect the actual folders. The root `Assets`, `Packages`, and `ProjectSettings` form the Unity project. This is a complete Unity reboot; do not restore the old engine implementation or an archive of it.

Read `README.md`, `CLAUDE.md`, `docs/reboot-vision.md`, and `docs/implementation-status.md` before changing the design. They contain the accepted direction, implementation rules, and current limits.

## First local milestone

1. Inspect the installed Unity editor version. The source targets Unity 6/C# with URP 17.0.4.
2. From the repository root, run `bash Tools/open-unity-mac.sh --batch-check`. Resolve actual compilation, package, import, and project-preparation errors. Do not claim this check ran until it has.
3. Run `bash Tools/open-unity-mac.sh`, open the generated main scene, and enter Play mode. Inspect the output and controls; fix observed problems. Continue through a working local playable milestone.
4. Use an independent critic subagent for focused design and implementation reviews, as requested by the user. Start from the existing implementation instead of restarting brainstorming. Ask only questions that materially block progress.

The initial cloud milestone passed 27 core checks. The continued local milestone now passes 202 core checks and runs in the installed Unity 6000.3.24f1 editor on this Intel Mac. Project setup and Play mode have run locally. See `docs/implementation-status.md` for current evidence and limits. Native builds and actual phone/tablet performance remain unverified. Use **Cellblock → Open game and Play**, then choose or resume a holding; **Controls** explains camera input and **People** opens individual profiles; **Control** manages arrivals, staff, lighting, alarm, gates and live CCTV. Six prefab units have optional fitout packages. Builders physically collect/carry materials and use task-specific tools. One agent owns all Unity runs.

## Product direction

Build a polished, realistic prison owner simulator with equal emphasis on freeform construction and company management. iPhone and iPad are primary; Mac and Steam are considered from the beginning. Windows support depends on practical performance and delivery.

Players choose an opening government contract and budget, receive an editable starter facility, and grow the business. Support straight, diagonal, and curved layouts, editable connected prefabs, and hired construction crews. The longer-term design includes staff hiring/training, prisoner services, financing, investments, and public ownership. Simulation pauses when the app closes; play supports pause and faster speeds.

Initial hardware references are iPhone 17 Pro, an iPad Pro whose generation needs identification, and an M4 MacBook Air. The user described the current Unity-equipped computer as a Mac Pro; inspect it rather than assuming it is the Air. Use the existing local Apple/Xcode setup for later device builds.

## Current parallel continuation

Claude has a reserved prisoner-lives slice and has received opening-mode/HUD direction from the user. Read `docs/claude-lives-handoff.md` and `docs/claude-coordination-update.md`. Its separate personnel branch is preserved and must be reviewed before integration. Codex's current security/services mechanics and limitations are in `docs/security-and-services-status.md`. Use the latest shared branch, retaining Unity save/presence/typed construction fixes and physical officer response.

## Latest combined continuation

Personnel and the twelve-profile mode catalog through Claude `029e5d9` are integrated. Start a new holding through jurisdiction, institution and location; see docs/game-modes.md. The facility journal contains goals, actual inspection visits, demonstrations and capital-case records. Read docs/facility-stories.md and the latest COORDINATION.md before continuing. The resident-lives and drag/HUD work remains reserved for Claude; do not rebuild the integrated mode system.

## October 17 integration

Claude overload snapshot b470a10 is integrated with direct click-to-place doors and the material/character/lighting pass. Read `docs/direct-doors.md`, `docs/presentation-polish.md`, `docs/pressure-notes.md` and the latest shared log. Preserve current physical security assignments, explicit charter terms and authored-case migration. Undo history is runtime-only; drafts survive saves and company transitions clear history. The sole Unity runner remains Codex.
