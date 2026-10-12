# Native 40-cell runtime audit

Target: local Business Tour 2.21, Steam build 25392206. Updated: 2026-10-12.
Evidence is the current generated interop assembly, native disassembly and
read-only inspection of the game's UnityFS bundles. This is not a live test.

## Graph, placement and movement

- `MapView.Initialize`, token `0x0600C07C`, RVA `0xADA3B0`, iterates actual
  `IMapConfig.CellsCount` and registers views by cell ID. No fixed 32 limit
  was found in that body.
- `CellViewFactory.Create`, token `0x06006D16`, RVA `0x8026B0`, selects
  prefabs by cell type, suit and skin, not by ordinal corner position.
- `GetCellPreinitData`, token `0x06006D17`, RVA `0x803200`, stages a 40-cell
  map in four directional bands using `cellsCount / 4`. Canonical presentation
  then remaps all 40 logical faces into five eight-cell sides. No patch of
  its non-blittable `ValueTuple` return was added. The reference-fit scale,
  new corner art and actual pawn placement still need visual verification.
- `MapPathFinder.GetPathByOffset`, token `0x06006D51`, RVA `0x8057C0`, follows
  `NextCellsArray` and resolves IDs through the current map, with a 1000-step
  guard. `CheckPathToCell`, token `0x06006D53`, RVA `0x805C10`, follows links
  with a visited-list cycle check. These bodies have no fixed 32 limit.
- Inspected travel commands and bot travel selection use actual filtered
  map/config cell arrays. The random-travel bot's 12-step lookahead is not
  a board-size limit. This does not establish every bot strategy is compatible.
- Inspected bundled card definitions contain deck/type/count data. StartOver
  and TouristTrip movement selects service cell types, not hard-coded old
  numeric IDs. Other cards, replay, reconnect and map caches remain live-test
  requirements.

## Ownership and art

- Original cities and resorts retain their positions, complete templates,
  groups and economy. Service replacements in V3 are12→trap,16←Tax30,
  28←Island8 and30←Chance12. Tournament16 relocates to37. Added cells36/39
  clone Tax30/Chance12;38 clones Resort14. Every final cell is compared to
  an independent complete native template clone normalized only for approved
  destination ID/line/edge, added-city suit and trap conversion. No original
  source cell is edited. The closing edge31→0 becomes31→32 and39 closes to0.
- Added cities33/34/35 use gameplay suit90. Native suit30 is borrowed ONLY by
  artwork lookup. This must not merge the added group with an old monopoly.
- `CellSkinConfig.GetSkinName`, token `0x06006B19`, RVA `0x7F82E0`, and
  `CellsConfigsCatalog.GetBasicCellSkinName`, token `0x06006B07`, RVA `0x7F7FE0`,
  both have unique bodies. The catalog inlines the skin lookup, so both are
  patched. Preflight resolves the registered game-owned provider through
  `Context.Get<ICellViewCompositeProvider>`, the same service as
  `Entity.CellViewProvider` (token `0x060025D6`, RVA `0xBC1F20`). This avoids
  a premature Unity resource scan that repeatedly saw no loaded catalog.
  Both patched native lookups must resolve the exact serialized Suit3 alias
  for every used skin before committing the array.
- The native line-monopoly predicate, token `0x06012E18`, excludes configured
  suits and compares the actual `LineId` with the captured line. No <=4 gate
  was found. Service1 excludes Chance/traps; line5 has three cities and its
  resort. Triple-monopoly threshold stays THREE, not four.
- Native resort ownership/rent uses actual filtered resort count. ID38 clones
  resort14's full valid metadata and economy (suit3, decoration11). Native
  generated rent tiers must validate as25k/50k/100k/200k/400k before commit.
  Boxed native LevelSetting results are read through their verified struct
  layout after IL2CPP unboxing, not a value-type interface invocation against
  the boxed address. Validation passed live for all five resorts; resort
  purchase/info UI and real payments remain unverified.
- V3 places traps12/32, Lost Island8/28 and Tax16/36, each20 graph steps
  apart. Chance20/30/39 has circular gaps10/9/21. Inventory:23 cities,
  5 resorts,3 Chance,2 traps,2 Island,2 Tax and1 Start/Tournament/Travel.
  A regular pentagon is not centrally symmetric, so a half-ring pair does not
  imply exact180-degree tile centers. Pure geometry checks establish this pair
  is among the closest visual opposites (about178.4 degrees for a regular
  pentagon) in both windings. Placement is fixed, not randomized. Its visual
  anchors and actual trap/card behavior still require a new live test.

## AFK correction evidence

`AfkHandler.Handle`, token `0x06002D79`, RVA `0x549B30`, increments the
missed-roll counter and invokes `OnKick` at three. The native
`PlayerBehaviour.OnDecisionMade`, token `0x060030DB`, RVA `0x565280`, contains
an inlined copy at `0x56589C`–`0x565919`, bypassing the earlier entry-point
patch. The new code guards only that handler's own callback. The unique
factory body `0x06002D84` / `0x549BF0` is invoked through `IFactory` by
`PlayerBehaviourFactory` (`0x060030F3` / `0x566040`). Cleanup restores owned
callbacks in `PlayerBehaviour.Release`. Server/network kicks are untouched.
Actual long-session protection still needs live verification.

## Release safety

The release graph uses map/room marker `BT5_FIVE_REALMS_V3`, separate from
the32-cell V1 preview and earlier40-cell V2 development graph. This is identification, not an enforced five-client
compatibility handshake. Every participant must use the same build.

Complete native clone/skin/economy preflight is transactional: array commit
is last. If preflight fails, the original resource remains intact and a
clear error is logged. A 32-cell fallback is NOT successful 40-cell gameplay.
Do not mark the conversion stable solely because pure tests/build/audit pass.

## Separate live follow-up

The 2026-10-11 C03E3789 development DLL reached native 40-cell materialization
and rendered a 40-cell pentagon with one human/four bots. Later development
tests included semantic building/service anchors, opposite traps12/32 and a
distinct native fifth color. The A462CF34 rent-foreground revision made prices
visible above opaque houses; its narrow name strip still made titles too small.
The latest source deepens the forty-cell annulus while preserving its outer
boundary, targets bounded per-line readability and resolves the exact Classic
Island3/Tournament5/TravelNextTurn6 artwork. Some regional catalogs lack those
exact service overrides; Travel9/14 are different services, not replacements.
Only the art skin is resolved: native cell types and service settings remain.
The9A1F47D7 V2 candidate subsequently displayed stadium, ship and palm-island
models, larger city names and readable rent prices; its Championships caption
remained too small. V3 service redistribution, full-face caption allowance,
resort binding and beach-art correction are not live-tested at the user's
request. Release limitations are recorded in[../RELEASE_NOTES.md](../RELEASE_NOTES.md).

## Final resort UI/art correction evidence

The current Fantasy buy-resort prefab serializes `_buyButtonText` and
`_rentText` as null, though native ButtonBuy/Text and Rent/Value children
contain valid BidirectionalText components. FillResortView generated token
0x0600A429/RVA0x9DF030 passes the null Text into Translate's dictionary,
reproducing ArgumentNullException(key). SetRent later dereferences the other
missing field. A unique Fill prefix binds only missing refs to those existing
children, preserving native actions/prices/localization and view geometry.
Classic already has both fields; its `_resortImage` null is safely guarded
and is not fabricated. Cleanup checks native pointer and instance identity.

Ground catalog keys for resort suits2..5 exist in all three skins, but
Wonderland BuildingsData decoration10..13 is AliceRoom type15, not Resort2.
V3 selects Classic beach art for all five Resort2 cells. This policy is used
only on the special private copy; native four-player resources are untouched.
