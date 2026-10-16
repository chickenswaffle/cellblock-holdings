# Continue on the Mac

Continue the Unity implementation in `chickenswaffle/cellblock-holdings` using the installed editor. The user has authorized local development tools, launching Unity, fixing compilation/import problems, and running the game.

## Get the current source

Use the latest `origin/main` from `https://github.com/chickenswaffle/cellblock-holdings.git`. Inspect the local checkout and its changes before updating it; preserve unrelated user work. Do not reset a dirty checkout or assume the cloud machine can synchronize the Mac automatically.

The previously identified local project location is `/Users/trading/Documents/ChatGPT/Cellblock Holdings`; inspect the actual folders. The root `Assets`, `Packages`, and `ProjectSettings` form the Unity project. This is a complete Unity reboot; do not restore the old engine implementation or an archive of it.

Read `README.md`, `CLAUDE.md`, `docs/reboot-vision.md`, and `docs/implementation-status.md` before changing the design. They contain the accepted direction, implementation rules, and current limits.

## First local milestone

1. Inspect the installed Unity editor version. The source targets Unity 6/C# with URP 17.0.4.
2. From the repository root, run `bash Tools/open-unity-mac.sh --batch-check`. Resolve actual compilation, package, import, and project-preparation errors. Do not claim this check ran until it has.
3. Run `bash Tools/open-unity-mac.sh`, open the generated main scene, and enter Play mode. Inspect the output and controls; fix observed problems. Continue through a working local playable milestone.
4. Use an independent critic subagent for focused design and implementation reviews, as requested by the user. Start from the existing implementation instead of restarting brainstorming. Ask only questions that materially block progress.

The engine-independent core passed 27 cloud checks, and seven C# source files passed a syntax parse. The cloud had no Unity editor: Unity API compilation, imports, Play mode, native builds, and hardware performance remain unverified. Current art is procedural prototype geometry; the concept image is a visual direction reference.

## Product direction

Build a polished, realistic prison owner simulator with equal emphasis on freeform construction and company management. iPhone and iPad are primary; Mac and Steam are considered from the beginning. Windows support depends on practical performance and delivery.

Players choose an opening government contract and budget, receive an editable starter facility, and grow the business. Support straight, diagonal, and curved layouts, editable connected prefabs, and hired construction crews. The longer-term design includes staff hiring/training, prisoner services, financing, investments, and public ownership. Simulation pauses when the app closes; play supports pause and faster speeds.

Initial hardware references are iPhone 17 Pro, an iPad Pro whose generation needs identification, and an M4 MacBook Air. The user described the current Unity-equipped computer as a Mac Pro; inspect it rather than assuming it is the Air. Use the existing local Apple/Xcode setup for later device builds.
