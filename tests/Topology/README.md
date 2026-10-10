# Unwired five-player topology candidate

Run the pure tests with:

```powershell
& .\.tools\dotnet6\dotnet.exe run --project tests\Topology\Topology.csproj -c Release --nologo
```

This test project does not load Unity/IL2CPP, change the installed mod, or claim
that the game can already play a 40-cell map. `IsRuntimeReady` is deliberately
false, and the runtime-materialization guard always rejects the candidate.

## Baseline evidence

On 2026-10-11 the locally installed UnityFS bundle
`BusinessTour_Data/StreamingAssets/configs/gameplay/maps/classic_bundle.unity3d`
was inspected without modifying it. Its `ClassicMap` has 32 serialized
`RawCellData` records. IDs are 0–31, every `_nextCellsArray` contains one ID,
and the next ID is `(id + 1) % 32`. `_lineId` is `id / 8 + 1`.

| Native side | Corner | City count | Resort ID | Other cells |
| --- | --- | ---: | ---: | --- |
| 1, IDs 0–7 | Start, ID 0 | 6 | 4 | None |
| 2, IDs 8–15 | Island, ID 8 | 5 | 14 | Chance, ID 12 |
| 3, IDs 16–23 | Tournament, ID 16 | 5 | 18 | Chance, ID 20 |
| 4, IDs 24–31 | TravelNextTurn, ID 24 | 4 | 25 | Chance, ID 28; Tax, ID 30 |

The total native city count is **20**. There are four resorts and three Chance
cells; the sides are not otherwise identical.

All four inspected resorts have the same native economy: purchase price
200,000, base rent 25,000, repurchase multiplier 2 and rent-control multiplier
2. The serialized rent levels are 25,000 / 50,000 / 100,000 / 200,000. Native
`CellData.GetControllBonusLevelSettings` sizes generated levels from the actual
filtered resort count. A five-resort tier would therefore be 400,000 using the
same native multiplier; the live UI/calculation path still needs verification.
The planner does not invent or reconstruct these settings: it requires a
complete native clone of resort ID 14.

`RawCellData` also contains `_metaPosition`. In the inspected bundle its grid
uses x positions `10 + 198 * (id % 8)`, y positions 10 / 346 / 628 / 910, width
168 and height 252 (Start height 306). These are existing editor metadata,
not a proven five-sided gameplay placement API. The planner intentionally
does not guess new visual/editor coordinates.

The complete cell clone must retain localization/name settings, pass/land
rewards, level settings, repurchase/rent multipliers, trap/tax/tournament/travel
settings, control bonuses and the disable-after-turn flag. The test inventory
contains only identity and graph metadata and cannot replace that clone.

## Candidate added side

| New ID | Full native template to clone | Planned role |
| ---: | ---: | --- |
| 32 | 12 | Corner Chance |
| 33 | 9 | New city, independent three-city group |
| 34 | 10 | New city, same new group |
| 35 | 11 | New city, same new group |
| 36 | 12 | Chance |
| 37 | 13 | Convert only this new clone to RogueTrap |
| 38 | 14 | Fifth resort, valid existing suit 3 / decoration 11 |
| 39 | 15 | Convert only this new clone to RogueTrap |

All 32 original kinds, native suits, decorations and line IDs remain unchanged.
The only old graph edit is `31 -> 0` becoming `31 -> 32`; new ID 39 closes to
0. There are five logical lines with eight cells each. No additional Start,
Island, Tournament, Tax or TravelNextTurn is created. Traps belong only to new
clones, so no existing ownable city group is sacrificed.

The candidate has five resorts, 23 cities, five Chance cells and two traps.
Placing both traps on the added side is a **candidate**, not a finished random
trap-distribution definition.

## Concrete blockers before runtime use

The three new cities need an independent logical group. Reusing an old city
suit would enlarge that old monopoly and alter its completion requirement.
The planner therefore records their native suit as unresolved rather than
casting an unsupported enum value.

An auditable proposal is a new map-specific gameplay suit value 90 with a
render-only alias to an existing city skin and a supported native city
decoration. This is not implemented or validated. The actual Classic
`CellSkinConfig` has city lookups only for suits 10–80, and native
`CellSkinConfig.GetSkinName`, token `0x06006B19`, returns null on a missing
type/suit lookup. Any alias must cover the relevant skin/config consumers,
be scoped to this map, and be restored afterward. It must not rewrite gameplay
suit 90 to an existing suit, and must not treat resorts as cities.

Native `CellViewFactory.GetCellPreinitData`, token `0x06006D17`, explicitly
divides `cellsCount` by four when routing cell placement. Passing 40 cells
therefore gives four groups of ten before any custom presentation, not five
groups of eight. Cell/corner creation and placement must be audited and
adapted before this candidate can be wired into the game.

Other outstanding checks include full native clone serialization, fixed
cell-count assumptions, card/travel references, resort tier UI, group/line
victory predicates, deterministic synchronization between clients and
unchanged ordinary four-player maps.
