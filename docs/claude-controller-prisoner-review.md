# Claude controller and prisoner review — 2026-10-18

## Integration follow-up — 2026-10-19 UTC

Claude's published7a5a905/fb173ff fixes are now imported on this Mac. Source review confirms physical release before FREE, a forced Home/session boundary, development-only launch gates, the ClaudeBatch guard and LookCapture focus-setting restoration. The pad/inmate shared-runner exceptions are accepted. The original review below remains the historical findings, not a claim that those defects remain unfixed. Combined core now436/436 passes; the local construction/audio regression is documented in implementation-status.md. Dedicated new pad/inmate/Home save-failure coverage and physical device checks remain separate follow-on work. Management remains staff-only.


Reviewed published `fcc2b95` on `origin/codex/claude-overload`, including `60aad23`, `3a53ba0` and the merge of Codex `fe38faf`. This is a source review with an independent read-only critic. No incoming gameplay source was imported into the Mac checkout, no Unity run was started, and no new test results are claimed. Claude reports 430/430 core and passing pad/inmate/look reviews in its clone; those are attributed results, not a new local validation.

## Decisions

- Accept the controller corrections in source: `PadAimTarget` verifies candidates with the same closest-hit `PickAt` used by selection. Body-centre targeting is conservative when partly obscured; it is a polish limitation, not a reason to bypass occlusion.
- Accept the `pad` **and `inmate`** cases in exactly `PresentationPreview.FeatureMode`, `BeginFeature`, `FeatureRunning`, and `AbortFeature`. Both captures request the shared runner and use unique Temp output. This approves development review infrastructure, not player-facing prisoner-mode activation.
- Accept PadCapture's snapshot/restore of both global Input System focus settings on its finish and abort paths. Preserve this contract. Reuse it for LookCapture, which currently changes these settings without restoring them.
- Keep `ClaudeBatch` limited to Claude's clone. Add a runtime `Application.isBatchMode` guard before any request/session mutation so accidental invocation in an interactive editor cannot schedule an editor exit. A comment alone does not enforce that boundary.
- Hold live prisoner-mode integration. `docs/prisoner-mode-plan.md` explicitly says planned/not enabled and requires design approval. The user's current review request does not replace the earlier direction to keep direct management control staff-only and plan prisoner play later. Keep the implementation available for review behind an explicit development-only gate, including its public start entry point; do not delete the work. The separate fresh company/save and per-resident permission are useful foundations.

## Blocking prisoner-mode defects

### P1 — the completed run cannot return Home

At incoming `Runtime/GameInmate.cs:79`, `LeaveInmateRun` calls `CloseInmateSheet`, but line158 refuses to close when `Outcome` is nonempty. The ending sheet has already been brought to the front. `ShowHomeMenu` neither hides that sheet nor unloads the inmate world. Further, `InmateUpdate` lines38–42 continues reopening the ending sheet for that world. Thus the displayed **Back to Home** action leaves the ending overlay in place; simply force-closing it is insufficient.

Introduce an explicit leave/suspend-session boundary: save safely, clear direct input/cursor, dismiss the ending sheet without its ordinary Back restriction, and suppress inmate UI/control updates while Home is active. Cover unfinished Save and leave, completed Back to Home, Continue, and starting/loading another holding. The ordinary unfinished-control entry path already checks BlocksWorldInput; preserve that guard while fixing the unguarded completed-outcome path.

### P1 — FREE is declared before actual release

Incoming `Core/InmateRun.cs:490` calls `EndRun` for `Release processing`. Existing `Core/Justice.cs:926–932` deliberately keeps that person in the facility until their physical walk reaches the sally port; a blocked path must leave them awaiting release. `InmateUpdate` then freezes time on the premature outcome, preventing that remaining release walk. The outcome text claims the person walked out without the physical release having happened.

Keep following during release processing; finish on actual released/former state. Preserve the existing blocked-release rule and allow the facility to progress until completion. Review both clear and obstructed exit routes, including save/reload during processing.

### Evidence gap

Incoming `Editor/InmateCapture.cs:174–183` checks the outcome and captures FREE, then finishes. It never presses Back to Home and does not assert that the resident physically reached release. Extend the review through the complete transition rather than treating the existing pass as evidence for those unexercised paths.

## Controller follow-on

Keep Claude's controller-navigation scope. Convert actionable ClickEvent-only HUD cards to semantic focusable controls or provide an explicit controller activation contract; do not synthesize arbitrary screen clicks. Prioritize Home/Save Library, workspace navigation, character selection and charter choices. Verify modal boundaries and closing the phone do not also trigger world actions. Partial-body reticle anchors can follow after correctness, provided each proposed anchor still passes `PickAt` visibility.

## Handoff and integration boundary

Claude should acknowledge its new branch/SHA, retain the accepted shared dispatch cases, fix the two lifecycle blockers and batch guard, restore LookCapture settings, and keep prisoner launch development-only pending explicit user approval. Its exact additional shared-file exception is only the development gate around the new Do time charter row/start path and its own prisoner session lifecycle. Existing management, save-library and character input contracts remain reserved; propose further changes in coordination first.

Publish the bounded fixes on Claude's branch and report the evidence paths/source SHA. Codex will then review and run the accepted combination in the sole Mac editor under its lock. The Mac remains on the prior validated gameplay source; unrelated recovery/settings files and actual saves were preserved. This review does not authorize a push to main.
