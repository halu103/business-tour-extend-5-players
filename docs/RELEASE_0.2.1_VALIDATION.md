# Release 0.2.1 UI hotfix validation

Date: 12 October 2026, Asia/Saigon. Graph remains V3. No game launch,
live gameplay test or real Steam installation write was performed for this
hotfix. The user's screenshot is evidence of 0.2.0 regressions, not a test
of this new binary.

## Final artifact identity

- ZIP: `artifacts/BusinessTourFiveRealms-0.2.1-win-x64.zip`
- ZIP SHA256: `57233DBB7FE5D2AAAE3FC2A56FEF7638653D1A9B9D0841D198289AF4263A9E30`
- Plugin SHA256: `B197139D411733F6C56178F74CC3D0CC895A336342934806A4863AC3D261C035`
- Embedded payload SHA256: `8C5E16C5716F1CCCDCAD76AED9AD1AE045E4B29A82123B6CB2E0D056FB348D3C`
- Source, assembly/informational version, plugin release string and installer
  agree on 0.2.1. Manifest retains 40 cells, 5 players, 5 resorts, 3 Chance,
  2 Island, 2 Tax, maximum 2 traps and `BT5_FIVE_REALMS_V3`.

## Changes and native evidence

- Removed the near-camera placement and Characters/32767 sorting overrides.
  Captions retain native camera-axis depth, fitted bands and original text.
  Runtime checks require GameFieldOver to precede Characters.
- Rent alone can clone its matched native TMP Overlay material, retaining
  atlas/properties, at transparent queue 3000. Native font assets are not
  edited. Both per-instance TMP and renderer material references have exact
  identity/ownership restoration; failed cleanup keeps the clone alive and
  is retried by the persistent updater, including outside special-map mode.
- Local native font bundles contain Distance Field/Mobile Distance Field
  materials using default transparent queue. Native Overlay counterparts
  exist in resources. Skin03/Character01Color0 references the native
  GAF/GAFObjectsGroup shader, transparent queue, ZWrite0 and ZTest LEqual.
  This supports the scoped material policy, not a guarantee for every skin
  or runtime shader availability. Unknown/unavailable shaders retain native
  materials with a warning; opaque-house rent occlusion can remain then.
- CellService Chance wheels are baked into the ground sprite, unlike
  CellObjectsRenderer catalogue models. The native painted Chance surface
  now has a 14% inset; full-cell bed, labels, collider and route remain.
  Separate native tournament/crown sprites on Chance cells are also fitted.
  This closes a gap found during review of the initial internal candidate.
- FestivalView references a CellEffectView with a serialized native GAF clip.
  Its clip owner, fireworks and monopoly owner are reanchored, not internal
  GAF frame objects. Dynamic refresh discovers late owners, guards hierarchy
  and object generations, and avoids applying nested offsets twice.
- Added only a private build reference to the game's generated GAF interop
  assembly. No native game asset or GAF binary is added to the release ZIP.

## Passed checks for the final artifact

- Ten regression suites and Release compilation: 0 warnings, 0 errors.
- Layout 30,000; geometry 8,928; projection 340 plus 3,456 native anchors.
- Labels 124,416 footprint cases, 162 readability cases, 40 deeper tiles,
  27 native-depth projections, 108 camera frames, matched shader/fallback
  policy and material-ownership source contracts. Old near-camera tests
  were removed because that policy caused the reported regression.
- Artwork 46,656 complete bounds fits and 1,296 baked Chance insets across
  both windings, rotated/slanted cells and sizes; no enlargement/cumulative
  shrink, invalid input rejection and dynamic owner source contracts.
- Payment 1,369; gameplay HUD 35,422; fifth lobby 1,254.
- Topology preserves the approved inventory, original ownables/economics,
  20-step paired services and Chance gaps 10/9/21; service art checks pass.
- Native alias audit remains 49 targets / 48 native bodies, no unreviewed
  shared body and one previously reviewed MapSettings constructor alias.
  No new Harmony target was introduced by this UI hotfix.
- ZIP has exactly seven intended entries; every checksum, source/payload/dist
  document and archive sidecar matches. No diagnostics or game assets included.
- Final isolated installer cycle: initial/repeat installation, uninstall,
  reinstall after uninstall, second uninstall, free/busy/abandoned maintenance
  mutexes, actual upgrade 0.2.0 to 0.2.1 and uninstall after upgrade all pass.
  Owned payload hashes/versions match. Shared loader, unrelated sentinel and
  compatible native game files remain intact. Evidence: `work/test-logs`.
- `git diff --check` passes.

## Preservation and limits

Real Steam plugin remains 0.2.0 SHA256
`822FBE8857B97B69323E16D9A90255C1FCA4BC6AE79164FCDBB6480D25C9E0E1`.
The user must close the game and run the new setup to install this hotfix.

Frozen 0.2.0 ZIP still hashes
`37BA14D1CF6B96BEF1C252119E906494221186075AD1AACF5AB1ECFA30566FB1`;
preview.2 remains
`45A39EDA6A727AA41DBFC41CE6E1218B16940128F706BAB74BD4FCF05250231F`.
Previous dist and installer evidence are preserved at
`work/diagnostics/2026-10-12-release-0.2.1`. Packaging replaces only known
generated dist filenames and archives only the explicit release set.

The checks above do not execute native TMP/GAF rendering or prove visual
quality. Final label occlusion, Chance/crown proportions, flag alignment,
long service captions, native material swaps/restoration and all gameplay/
networking limits in [release notes](../RELEASE_NOTES.md) remain live checks.
No claim of certified online stability is made.
