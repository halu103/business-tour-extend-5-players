# Five-player topology planning and native adapter

Run the pure tests with:

```powershell
& .\.tools\dotnet6\dotnet.exe run --project tests\Topology\Topology.csproj -c Release --nologo
```

This test project does not load Unity/IL2CPP, change the installed mod, or claim
that the game can already play a 40-cell map. The pure candidate's
`IsRuntimeReady` is deliberately false, and its materialization guard always
rejects direct use. A separate native adapter is wired in the development
source, with native validation required before committing its array. Pure
tests do not establish that the expanded game is playable.

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
| 32 | 12 | RogueTrap, opposite trap 12 |
| 33 | 9 | New city, independent three-city group |
| 34 | 10 | New city, same new group |
| 35 | 11 | New city, same new group |
| 36 | 30 | Second native Tax, opposite Tax 16 |
| 37 | 16 | Original native Tournament relocated intact |
| 38 | 14 | Fifth resort, valid existing suit 3 / decoration 11 |
| 39 | 12 | Native Chance |

The approved service revision permits exactly these original-prefix changes:

| Destination ID | Original role | Full native template | New role |
| ---: | --- | ---: | --- |
| 12 | Chance | 12 | RogueTrap |
| 16 | Tournament | 30 | Native Tax |
| 28 | Chance | 8 | Native Island |
| 30 | Tax | 12 | Native Chance |

Every other original template and role is retained. All original destination
IDs and line IDs stay fixed. Every one of the 20 original cities and four
resorts keeps its exact position, suit, decoration and complete economy
template. The only old graph edit is `31 -> 0` becoming `31 -> 32`; new ID 39
closes to 0. There are five logical lines with eight cells each. Start 0 and
TravelNextTurn 24 stay intact; Tournament is moved to 37. Both traps derive
from complete native Chance 12 clones, so no existing ownable group is sacrificed.

The exact inventory is 3 Chance, 2 Island, 2 Tax, 5 Resort, 23 City, 2 RogueTrap,
1 Start, 1 Tournament and 1 TravelNextTurn, totaling 40 cells. Island 8/28,
Tax 16/36 and RogueTrap 12/32 are each twenty graph steps apart in either
direction. Chance 20/30/39 have circular gaps 10/9/21, including the last-to-first
gap; the minimum gap is nine, but they are not evenly spaced. Services keep
their complete native effect settings. Island, Tax, Tournament and Travel
receive no custom trap duration/release-cost settings. Traps are fixed; only
the five region skins are seeded. A regular pentagon is not centrally
symmetric: equally spaced tile centers are not exact antipodes. The trap pair is
among the closest available visually opposite half-ring pairs (approximately
178.4 degrees on a regular-pentagon model), tested for both traversal windings.
It is not a claim of an exact diameter or of balanced chance-card frequency.

## Concrete blockers before runtime use

The three new cities need an independent logical group. Reusing an old city
suit would enlarge that old monopoly and alter its completion requirement.
The planner therefore records their native suit as unresolved rather than
casting an unsupported enum value.

The development source uses a new map-specific gameplay suit value 90 with a
render-only alias to an existing city skin and a supported native city
decoration. Two scoped native lookup patches implement this artwork fallback;
loaded native aliases must pass preflight, and live rendering remains unverified. The actual Classic
`CellSkinConfig` has city lookups only for suits 10–80, and native
`CellSkinConfig.GetSkinName`, token `0x06006B19`, returns null on a missing
type/suit lookup. Any alias must cover the relevant skin/config consumers,
be scoped to this map, and be restored afterward. It must not rewrite gameplay
suit 90 to an existing suit, and must not treat resorts as cities.

Native `CellViewFactory.GetCellPreinitData`, token `0x06006D17`, explicitly
divides `cellsCount` by four when routing cell placement. Passing 40 cells
therefore gives four groups of ten before any custom presentation, not five
groups of eight. Audited MapView/CellViewFactory bodies accept actual cell count
and select prefabs by type/suit/skin, not ordinal corners. Canonical presentation
remaps that square staging into the pentagon; no non-blittable placement hook
was added. Actual reference fitting, pawn placement and labels need live tests.

Other outstanding checks include full native clone serialization, fixed
cell-count assumptions, card/travel references, resort tier UI, group/line
victory predicates, deterministic synchronization between clients and
unchanged ordinary four-player maps.

## Native adapter preparation

`NativeFivePlayerMapTopology.Prepare` prepares all 40 cells off-map and leaves
the source untouched. It mirrors native `MapConfig.CreateCopy`: full
`CellData` round-trip through the game's `JsonParser`, then a new `RawCellData`
wrapper with the template metadata. The game uses this parser rather than
Unity's `JsonUtility` for this operation.

The adapter requires the caller to establish the map-specific expansion-city
asset alias before assigning gameplay suit 90. It verifies strict Classic
inventory, independent cloned objects, all original ownables pinned to their
complete original templates, and only the explicitly approved service-prefix
replacements. Preserved template name/localization keys,
consistent resort economy and five tiers from the real native resort-level
calculation are validated too. All 40 prepared cells are compared with separate
complete native clones of their selected templates. Only destination ID,
line, next edge, appended-city suit 90 and the exact approved trap transition
are normalized on those expected clones. The prepared cells and source arrays
are never temporarily edited for this comparison. Raw editor metadata is
checked against the selected template, including relocated native services.
Thus tax/tournament/travel/control settings, rewards and all other serialized
native effect fields remain covered by the full serialization check.
Exact inventory, half-ring paired-service separation and circular Chance gaps
are asserted before commit. `Apply` performs a single final array assignment after preparation;
an enclosing generation transaction can use `Prepare` and commit later.

These adapter checks are native runtime assertions, not results of this pure
test project. The adapter has no placement or skin patch hooks of its own, and
the existing blockers still require integration. Reused city/resort template
names are deliberately preserved rather than inventing localization entries.

The pure suite also embeds the native adapter source and checks these cloning,
field-preservation and validation contracts. These source checks are not an
execution of IL2CPP, and cannot prove live card outcomes or Island/Tax effects.
