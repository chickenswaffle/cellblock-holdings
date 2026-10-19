# Projects & crews

## Player flow

Open **Facility → Projects & crews**, or the same button under **Control → Arrivals / crew transport**.

- **Active** shows every approved unfinished project in approval order. There is no three-project limit here.
- **Needs attention** filters alarms, no hired crew, inbound crews held at the gate, reported blocked/held work and handover dependencies.
- **Completed** shows finished work with the newest approvals first. These projects are already operational.
- Choose a project to see its preparation, demolition, building/installation and handover bars. Green is progress; amber marks attention. Overall progress remains at99% until the simulation actually completes handover.
- **View work site** closes the desk and frames the project location. **View [crew]** frames the assigned crew's actual position.
- **View prerequisite** opens the first unfinished dependency. It does not change approval order or scheduler assignments.
- **Prison controls** opens the existing control panel to review alarms/gates. The desk never clears a security response automatically.
- **Hire crew** uses the existing hiring/transport rules. Failure feedback, including a full parking reservation, appears inside the desk. Crews cost$500 to hire plus their existing wages. Newly hired crews still arrive physically.
- **Pause / Resume ·1×** controls the game clock. Opening the desk alone leaves time running at its selected speed. Close/Back returns to management.

## Information hierarchy

The queue keeps each project's name, current stage, overall percentage and attention color. Detail is shown for one selection at a time. The selected panel separates current action/reason, work stages, committed material spend, remaining scheduled effort, prerequisites and real crew assignments. Landscape uses two independently scrolling panes; portrait stacks the queue above detail. Short landscape reduces decorative header space while retaining44px buttons.

The remaining effort is **crew-minutes**, not an arrival/completion ETA. It sums the actual remaining preparation, tasks and handover work. Travel, supply collection, access delays, dependencies and exclusive work fronts can extend elapsed game time. Materials displayed were paid at approval; the desk does not buy them again. Completed geometry and services remain controlled by safe handover.

## Ownership and implementation

New UI lives in `GameConstructionHud.cs` and `ConstructionDesk.uss`. GameHud has narrow initialize/refresh/layout/input/Back hooks and the Facility entry; GameOperationsHud has the Control entry. Projects, crews and dependencies are read from the existing World. Scheduler, navigation, authoritative construction, saved identities and save schema are unchanged. Manual crew reassignment, queue reordering and cancellation are not part of this pass.

Buttons are cached by saved project/crew identity rather than rebuilt every refresh. Actions validate the active World session. Closing and switching company clears old-world caches before the closed-state early return. Home, other modals, drafts and character sessions close the desk. The desk blocks world clicks but allows simulation updates. Successful hiring uses the existing save flow; ordinary viewing/filtering does not write a save.

Claude retains character/graphics-quality/audio ownership. Preserve this project's new hooks and the `projects` feature case when merging the upcoming human-model fixes.

## Evidence and limits

Independent critic reviewed data accuracy, lifecycle, input guards and stable controls, and inspected the authored gallery. Findings about hidden hiring feedback, old-world retention and short-landscape space were corrected. Installed Unity6000.3.24f1 compiled/imported the source. Final9-frame authored gallery has empty runtime errors; source fingerprint eef5adf847cb. See [report](reviews/2026-10-19-project-desk-art.json), [source](reviews/2026-10-19-project-desk-source.json), [landscape view](reviews/2026-10-19-project-desk-phone.png) and [prerequisite view](reviews/2026-10-19-project-desk-desktop.png).

The `projects` shared-runner feature stages disposable project data and captures actual UI compositions. These are authored layout images, not automated gameplay or scheduler tests. No new automated tests were added or run. Existing436-check core evidence belongs to the previous implementation; core files remain unchanged here. Native touch/controller operation, actual device performance, prolonged live completion/filter transitions and parking/hiring gameplay are not newly verified by this gallery.
