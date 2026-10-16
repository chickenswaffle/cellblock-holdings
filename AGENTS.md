# Shared agent workflow

Read `CLAUDE.md` and `COORDINATION.md` before any work. Re-read the coordination file before editing, before Unity/build runs, and before committing. Follow the ownership table and atomic scope-lock protocol there. This applies to Codex and any other coding agent in this checkout.

Keep progress, evidence, and local milestone commits in your own coordination log. Preserve other agents' changes. The `unity-editor` scope has exactly one runner. Never start a second editor or duplicate build against the checkout. Do not push without the user's request.

The user has requested an independent critic for focused reviews. Use a critic on substantive simulation/construction changes, with read-only review unless a separate scope is explicitly handed off.
