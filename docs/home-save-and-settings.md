# Home, Continue, autosave and the command HUD

## Player flow

The home screen uses original illustrated night-campus artwork, warm brass actions and dark ink surfaces. The first action is **Continue your holding**; during a session it becomes **Continue playing**, which returns directly to the current paused facility. The card identifies the holding, location, game day, money and saved/recovery status. **New Holding**, **Save Library** and **Settings** have their own clear routes.

The library lists separate holdings newest first, marks the last played holding, and exposes recoverable backups. Continue uses the last played usable save. If the pointer is missing, malformed or points at a missing/unusable holding, it falls back to the newest usable save. Unreadable entries stay visible with their files preserved. Loading starts paused and does not add elapsed wall-clock game time.

Autosave defaults to **30 seconds**. General Settings offers 30 seconds, 1 minute or 2 minutes. Saving also runs when opening Home, losing application focus, suspending, quitting, changing companies and approving construction. The header shows the last successful save; a failure leaves a persistent warning and a Retry save action. Keep the session open if saving fails. Unchanged automatic snapshots do not rotate the successful backup unnecessarily.

Settings are global to this installation and live separately from company saves:

- **General:** autosave frequency, save now, Home.
- **Display:** interface size, 30/60 frame-rate cap, main-camera cast shadows, reduced UI/camera motion.
- **Controls:** keyboard/mouse orbit sensitivity, camera easing and the actual camera controls reference.
- **Audio:** live master volume, including alarms; muting sound does not alter the facility's alarm state. Dragging volume applies live and debounces disk writes.

Escape/Back closes Settings or the charter overlay before returning from Home to the facility. Programmatic CloseOverlays keeps its existing close-all contract. The bottom command dock groups **Facility / Build / People / Company / Control / Operations** (Operations was previously labeled Pressure). Camera and Guide stay adjacent. The header puts Save, Settings and Menu together. Three live indicators remain visible without selecting a person: usable capacity, average stress, highest escape risk. Full capacity is amber; overcrowding is red. People retains its fixed profile/risk/record/roster navigation and signed risk meters.

## Data-safety contract

- Company names retain the existing strict GUID filename format. No delete/reset/migration-to-a-new-directory action is introduced.
- Enumeration validates a deserialized copy through World, trying the primary and then `.bak`. It never hydrates or writes the on-disk state while listing it.
- Resolve the requested load target **before** saving the current company: saving updates the pointer and must not redirect a requested load.
- Write a temporary snapshot and replace atomically. Normal saves retain the previous successful `.bak`. Update the pointer atomically.
- A recovered holding records that recovery. The first subsequent save archives the damaged primary as `.damaged-<timestamp>`, replaces the primary without touching the good backup, then resumes normal backup rotation.
- On load, update the pointer before swapping the active World/save identity. A pointer I/O failure cannot pair a new World with the previous holding's identifier.
- Quiet unchanged saves preserve their actual last-save time. Errors are concise in the game, with technical detail only in the development log.
- Preferences are validated/clamped and written separately by temporary-file replacement. No preference changes alter prison security mechanics, game time or the save schema.

## Implementation and collaboration

New partials: `GameHomeHud.cs`, `GameSaveFlow.cs`, `GamePreferences.cs`, `GameSettingsHud.cs`, `GameCommandHud.cs`. Theme/assets: `Frontend.uss`, `HomeNight.png` with an explicit 2D TextureImporter. Keep the title art in Resources for players, and retain its required resource check in ProjectSetup.

The user's direct request reserves the home/save/settings/command-shell parts of GameHud/GameController for this Codex pass. Construction/catalog logic, charter mechanics, Core/Simulation.cs and Claude's lives/personnel/economy files retain their prior owner. Preserve all new economy build/refresh hooks and the numbered-meter integration when changing the shell. Read COORDINATION.md for the exact exception and claims.

Claude's published economy/informants/kitchen/counsel/role snapshot `9c88d9e` was merged as `243441f`; its combined core passes 286 checks. One scoped runtime correction releases the former controlled role after a new ride-along target has been validated, preventing hidden decision cards from pausing the game.

## Local review

`frontend` in `Temp/CellblockPresentation.request` runs the sole editor review. It redirects both company saves and preferences to a unique Temp directory before writes. Actual player companies are never review fixtures. It exercises real atomic files, pointer fallback, chosen-slot identity, autosave, backup-only recovery, damaged-original retention, failure/retry, preference persistence/application, desktop/phone/narrow/large-interface layouts, and integrated economy/intelligence/counsel/kitchen/role flows. The kitchen review quotes and builds a real serving counter before staging the kitchen demo.

The first black-title-art and missing-kitchen-fixture runs were corrected; they are not current passing evidence. Final reports and exact counts are recorded in implementation-status.md and docs/reviews. The main artwork is title illustration, not a gameplay screenshot. Actual device gestures, native stripped builds and sustained device performance remain unmeasured.
