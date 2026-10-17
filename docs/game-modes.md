# Choosing a holding

The opening screen now uses Claude's mode catalog through an integrated three-step charter: **jurisdiction → institution → location**. City, County, State and Federal express increasing oversight difficulty. The institution defines its population and operational problem; a federal camp is different from a federal supermax. Locations can be selected or drawn from the chosen jurisdiction. All places and rules are fictional game content.

The catalog has twelve institutions and eight locations. The final step shows acquisition, operating reserve, daily base income/commitments and the payment cycle. A charter is fixed at acquisition; a different contract starts a separate holding. Existing company slots and legacy terms are preserved. Saved holding labels show institution and location.

## What changes in play

- Seeded population traits, gang share, pretrial share, charge categories, sentence lengths and hearing cadence.
- Applicant quality/wages, starting warden/nurse/officers, staff pressure, regime and gate state.
- Actual acquisition and ongoing contract terms; location-adjusted land cost and court travel time. Existing saves use their original contract terms; only the new factory applies explicit catalog prices.
- Editable layouts: shared housing, two open dormitories, six secure double cells or twelve individual cells. Starting population follows the institution and spare beds remain usable capacity. Appropriate starts include medical furnishings and installed powered monitoring. The catalog's room names are guidance, not a complete functional room-zoning system.
- Supermax routine movement and authorized court/release movement need an assigned mobile officer within actual distance and line of sight. Officers visibly walk to their assigned resident and accompany movement. Escape attempts deliberately breach authorized movement rules.
- Open-campus starts hide the presentation boundary fence and have increased walk-away risk. Installed barriers remain separate editable equipment.
- More demanding jurisdictions require stronger inspection scores and more frequent oversight. Legacy companies retain their earlier inspection settings.

## Integration details for Claude

`World.StartMode(typeId, locationId)` performs acquisition, layout creation and ApplyMode as one opening operation. `State.Mode.ExplicitTerms` selects catalog financial terms; the three legacy `World.Offers` aliases remain for compatibility and existing checks. `GameController.StartModeCompany` creates a fresh save slot. `GameModesHud.ShowModePicker` hosts the wizard; it holds world input/clock. The charter row for re-chartering an operating facility is hidden. Explicit acquired charters reject ApplyMode so pricing cannot be switched for free.

Codex integrated your two personnel archive fixes from `029e5d9`. `Walk` now consumes a zero-distance first route point immediately, fixing moving-target watch/escort replans that previously stood still. Existing security duty assignments, actual CCTV monitoring/escort presence and modal input guards remain preserved. Warden and Nurse stay personnel posts; no role migration is needed in this pass.

The broader catalog dragging/placement/HUD redesign and new Lives files remain your handoff. Codex only changed existing menu creation, mode company creation, contract lookup/save labels and explicit new-world initialization for this integration. No ConstructionGuides or Cellblock.uss edit was made.

## Evidence and limits

Core checks enumerate every supported mode/location pairing, assert housing capacity, resident/service routes, chosen finances and save/load terms, and check actual nearby escorts and moving-target progress. Unity Play evidence covers all charter steps, desktop/phone layout, camp/supermax starts and Unity JSON. Native phone/tablet touch, sustained performance, advanced room zoning and long-run balance remain separate work. Review the final transcript/report rather than treating catalog descriptions as proof of every later feature.

For Lives integration: `ChooseResidentActivity` must be a pure target query. The secure-escort planner also calls it to assign officers before routine movement. Book programs, update memories and advance work only in `AdvanceLives`, once per explicit game minute and only after actual arrival/presence checks.

Claude’s catalog design and roadmap notes from b470a10 are preserved in [the design appendix](claude-game-modes-design.md). Current integration status above takes precedence over earlier wiring requests.
