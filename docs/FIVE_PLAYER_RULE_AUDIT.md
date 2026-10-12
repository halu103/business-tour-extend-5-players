# Five-player rule and board audit

Audit updated: 2026-10-12. Target: Business Tour 2.21, local Steam installation.

## Release0.2.0 / V3 update

The current release uses40 cells:23 cities,5 resorts,3 Chance,2 Island,
2 Tax,2 traps and1 each Start/Tournament/Travel. Trap12/32, Island8/28,
Tax16/36 are20 route steps apart; Chance20/30/39 gaps are10/9/21.
Native effect templates are cloned in full; the Chance deck is unchanged.
Original ownables retain their position/economy/group. Some non-ownable
services relocate:16←Tax30,28←Island8,30←Chance12,Tournament16→37.
V3 marks this change separately from V2. The final redistribution, resort
caption binding and Classic beach artwork were packaged without another
live test, as requested. See[../RELEASE_NOTES.md](../RELEASE_NOTES.md).

The remainder below is the historical preview.2/V2 audit, not the current
inventory or final release verification status.

## Current scope

The following 32-cell scope describes the published preview.2 archive, which
has not been replaced. The preview.3 development source now includes the
transactional `NativeFivePlayerMapTopology` adapter: all32 old cell settings
are cloned intact except the closing graph edge and approved Chance12 trap
conversion, eight cells are appended,
and native clone/graph/skin/five-resort rent preflight precedes the sole array
commit. Three added cities use independent gameplay suit90 with render-only
suit30 aliases. Opposite traps12/32 use complete native Chance templates,
not conversions of ownable cities. Former traps37/39 become Chance clones.
The intended result is40 cells,23 cities,5 resorts,5 Chance and2 traps.
The two traps are20 graph steps apart in either direction. A pentagon is not
centrally symmetric; the selected tile centers are among the closest available
visual opposites, not a mathematically exact180-degree pair. The placement is
fixed, while region skins remain seeded. No original four-player map changes.
Live materialization/payment/endgame testing remains required; compile success
does not establish that this conversion is playable. See
[NATIVE_40_RUNTIME_AUDIT.md](NATIVE_40_RUNTIME_AUDIT.md).

The current Five Realms mode adds a fifth participant, five visual regions and
a pentagonal board presentation. It still uses the native 32-cell board graph.
Five visible sides do not yet mean five logical monopoly lines.

**A fifth resort/beach is not included in preview.2.** Neither a 40-cell topology
nor a general "add one to every four-player rule" transformation is included.
Ordinary four-player resources must remain unchanged.

`CompositeMapPatch` deep-copies native map data, selects five region skins and
converts at most two eligible city cells into static RogueTrap cells. This
changes the special map's city inventory; it is not the original map plus an
additional side. The current 32-cell region boundaries are
`[0, 7, 13, 20, 26, 32]`.

## Verified native constraints

These observations come from the local generated interop assembly and native
method disassembly, not from assumed Monopoly rules.

| Component | Verified behavior | Expansion implication |
| --- | --- | --- |
| `CellSuit` | Contains `ResortsSuit1` through `ResortsSuit4` (values 2–5), `Service` (1), and eight city suits. | A new resort suit cannot be invented without auditing every consumer. |
| `BuildingsGroup` | Contains four resort decorations and eight city decoration variants. | A fifth resort needs a supported existing decoration or a deliberate new asset path. |
| `CellData.SetCellType`, token `0x06006D7B` | Assigns resort suit/decoration only for logical lines 1–4. City suit assignment uses `CellId / 4` with eight handled groups. | The native editor transition does not fully initialize an arbitrary fifth line or additional city IDs. Complete cell metadata must be assigned explicitly. |
| `AllCellsOfTypesOwned_WinCondition.CheckMonopoly`, token `0x0600CA3C` | Compares the actual filtered cell count with the owned cell count. | Resort monopoly is data-driven; adding a valid fifth resort can change the required count without replacing a hard-coded four. |
| `CellData.GetControllBonusLevelSettings`, token `0x06006D76` | Creates levels from the actual filtered cell count and applies the native rent multiplier. | A fifth resort can require an additional rent tier. Its purchase/info UI and actual rent calculation still need verification. |
| `WinConditionsFactory.Create`, token `0x0600CA79` | Triple monopoly excludes Service and the four existing resort suits; its required group count is three. | A made-up resort suit value 6 would not be excluded automatically. The triple-monopoly threshold should not be raised merely because there are five players. |
| `MapPathFinder` | Exposes paths by cell ID and follows the map's cell connections; it caches computed paths. | Additional cells require a coherent graph and correct cache lifetime, not just extra visible tiles. This does not prove all other consumers accept an expanded graph. |

## Trap identity correction in the worktree

The earlier conversion assigned only `_cellType = RogueTrap`, leaving the
former city's suit and decoration attached to a non-city cell. That could
leave an unbuyable trap in a city monopoly group and retain city artwork.

The worktree now calls `CellData.SetCellType(RogueTrap)`. Its verified native
body assigns RogueTrap type, Service suit and NoBuilding decoration while
preserving cell ID, logical line ID and next-cell links. The mod still supplies
Fantasy skin and the configured trap settings. This is a narrow consistency
fix, not a new board or economic ruleset. It does not reset other cell data
that the native transition itself preserves.

This change was made after the `0.2.0-preview.1` packaging snapshot and is
included in `0.2.0-preview.2`. It still requires live regression testing.
Because suit metadata affects gameplay decisions, every participant must use
the same preview version; do not mix preview.2 with `0.2.0-preview.1`.

## Required work for five resorts and five logical sides

1. Capture the baseline native map inventory: cell types, suits, logical lines,
   IDs, links, prices, rents, rewards and control bonuses. Assert that ordinary
   four-player copies remain identical to this inventory.
2. The user selected40 cells: five eight-cell sides. The development adapter
   implements this inventory explicitly, not as an automatic blanket increment
   of every four-player rule.
3. Clone complete supported cell definitions. Assign unique IDs, next-cell
   links, logical lines and valid suit/decoration metadata. Do not initialize a
   resort by changing only its cell-type enum, and do not invent arbitrary
   prices or an unsupported suit value.
4. Audit city-group completion, line monopoly, triple monopoly, resort
   monopoly, resort rent tiers, control bonuses and their UI. Keep special
   Service cells outside ownable city groups.
5. Audit Start/lap rewards, world-travel destinations, chance-card cell
   references, tax/tournament cells, bot strategies, pawn positions, map
   previews and any fixed cell-count assumptions.
6. Validate deterministic map generation and synchronized decisions on all
   participating clients. A local one-human/four-bot test does not demonstrate
   compatibility between five independent accounts or with backend limits.
7. Test the full game lifecycle: turn order, repeated laps, purchases, rents,
   bankruptcy, every win condition, reconnect/room exit, and switching between
   five-player and ordinary four-player maps.

## Rules that should not receive a blanket increment

Player count, HUD slots and deliberately per-side board inventory may need
expansion. Dice rules, starting money, lap rewards, tax percentages, rent
multipliers, tournament modifiers, trap duration and triple-monopoly thresholds
are separate rules. Increasing them all by one would change the game's
economy and victory conditions, contrary to preserving normal gameplay.

Converting one of the existing 32 city cells into a fifth resort is possible
as a separate map design, but it removes that city and changes its group. It
must not be described as preserving the original board and adding one more
resort without tradeoffs.
