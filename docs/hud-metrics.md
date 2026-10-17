# Numbered meters and inspector navigation

## Player controls

- **People → select a name** opens that person's profile. The fixed **Profile / Risk / Record / Roster** row stays available while scrolling. The name remains above it, including in Risk and Record.
- **Risk** opens escape details. Red bars extend right from zero for factors that add risk; green bars extend left for protective factors. The number inside is **risk points**, not a probability. The top bar is the final 0–100 risk score.
- The initial factor list includes up to three leading danger factors and three protective factors, then fills any free places with the next strongest factors. **Show all** exposes the remaining factors. All factors use the same symmetric scale, shown above the list.
- **Control → Switches / CCTV / Team / Power / Reports** jumps directly to the relevant section. Normal scrolling still works, and the section highlight follows manual scrolling. Navigation and action buttons retain at least a 44-pixel target in the reviewed layouts.
- Resident needs, staff condition, personality, equipment condition and construction now have readable numbered tracks. Needs and conduct scores are out of 100. Personality uses neutral blue; its effects belong in the actual risk explanation. Electrical load shows kW demand/capacity; CCTV shows a current count of observed/present residents.
- Green means a healthy score or protection, amber needs attention, red indicates danger. Higher food/safety/belonging/integrity/morale/condition is better; higher stress/pressure/conduct risk is worse. Zero scores retain a colored critical border when appropriate. Escape color thresholds match the simulation exactly: Elevated at 35, High at 55.

## Integration contract for Claude

The later [Home/save/settings/command-shell pass](home-save-and-settings.md) adds the visible main dock, header controls and capacity/stress/risk indicators. Its exact existing-file ownership exception is in COORDINATION.md; the historical file-preservation statement below describes the earlier numbered-meter milestone. Keep these meter hooks in the new shell.

This is additive to the reserved main HUD/catalog work. `GameHud.cs`, `Cellblock.uss`, the personnel/lives/pressure files and save schema are not edited by this pass.

- `Runtime/StatMeter.cs`: reusable `VisualElement`, engine presentation only. `SetScore` receives meaning, actual readout/unit, optional help and threshold overrides. `SetFactor` receives signed points and the shared scale. Number badges are readable even when the fill is narrow or zero. Gradient fills use four UI vertices and retain no textures/material instances.
- `Runtime/GameMetricsHud.cs`: owns the meter pools, exact data mapping and fixed section navigator. `BuildProfileMeters` is called once at the end of `BuildJustice` after legal/personnel fields exist. `RefreshProfileMeters` runs before the no-resident early return, so staff updates too. `RefreshRiskMeters` runs before collapsed-details return.
- `GameOperationsHud.cs`: owns stable section anchors and the existing CCTV observation query, reused for the new overview bars. `GameSecurityHud.cs` builds/refreshes the overview and condition meters after Control exists. Existing project/trait/need fields remain valid for the old refresh contract; their old tracks are hidden in favor of numbered meters.
- `Resources/Metrics.uss`: scoped additive styling. The navigator is inserted into the ScrollView's **outer hierarchy**, before the viewport. It must never be added to its scrolling content. Deferred content-relative jumps are canceled on a changed company/person/workspace and reapplied after a viewport resize. Keep the header outside scrolling content and ensure its buttons block world input.
- New partials can access GameHud private fields without editing its reserved source. If replacing the inspector or main navigation later, preserve these build/refresh hooks, the toolbar's stable children, the context name, all unit meanings and phone bounds. Coordinate in `COORDINATION.md` before moving ownership.

## Verification

The `metrics` request in `Temp/CellblockPresentation.request` runs the disposable Play review in the sole installed editor. It checks signs/directions and exact risk thresholds, fixed navigator bounds and world-input blocking, section jumps, subject-change state, readable numbers, three widths, and a real approved construction project. Final focused review passed 20 actual Game-view captures; the existing general regression passed another 16 (36 total), with no runtime errors. The unchanged core passed a fresh 215/215 checks. The first phone resize failure was fixed and rerun; only the final report is the current evidence. Full images belong in ignored `Builds/MetricsReview`; small final reports belong in `docs/reviews`.

Phone-sized editor layouts do not establish actual touch behavior or device performance.

## Next work

Claude retains main workspace navigation, building/catalog controls and its active resident-lives/personnel/economy systems. Codex follow-ons from Claude's interface list remain bounded failed-Walk retries, contractual justice fees after termination, staff appearance/counsel/escape hooks, and physical intake/scanning fixtures coordinated with their consumers. No decorative substitute equipment or new core effects were introduced by this HUD pass.
