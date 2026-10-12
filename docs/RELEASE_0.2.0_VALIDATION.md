# Release0.2.0 validation

Date:12 October2026, Asia/Saigon. Packaging channel:release. Graph:V3.
User requested no game restart for the final changes; no final live test or
fully stable gameplay claim is made.

## Artifact identity

- ZIP: `artifacts/BusinessTourFiveRealms-0.2.0-win-x64.zip`
- ZIP SHA256: `37BA14D1CF6B96BEF1C252119E906494221186075AD1AACF5AB1ECFA30566FB1`
- Plugin SHA256: `822FBE8857B97B69323E16D9A90255C1FCA4BC6AE79164FCDBB6480D25C9E0E1`
- Embedded payload SHA256: `102DBC3FD15418C64415963E4F9E85BE403EB2C274AD5AB72CF18C7B3B1543F9`
- Source, DLL informational version, plugin release string, installer and
  payload metadata agree on0.2.0. Mod manifest:40 cells,5 players,5 resorts,
  3 Chance,2 Lost Island,2 Tax,max2 traps,`BT5_FIVE_REALMS_V3`.

## Passed automated checks

- Release compilation:0 warnings,0 errors; local cached dependencies only.
- Layout:30,000; geometry:8,928; tile projection:340 plus3,456 anchors.
- Labels:124,416 footprint cases,162 readability projections,40 deeper-tile
  checks,27 depth projections,288 foreground checks,108 camera frames.
- Payment:1,369 sizing/source-contract checks. HUD:35,422. Fifth lobby:1,254.
- Topology:exact nine-kind inventory totaling40, complete selected-template
  contracts, original ownable preservation, all paired services20 steps apart,
  Chance gaps10/9/21, independent suit90, forty two-lap walks and rejection of
  unsupported sources. This pure suite does not execute native IL2CPP effects.
- Service artwork:compatible Classic service/beach family resolution and
  exclusion of Wonderland AliceRoom15 for Resort2.
- Native-body alias audit:49 targets/48 bodies,0 unreviewed shared bodies,
  1 previously reviewed compatible MapSettings constructor alias. The resort
  FillResortView target has a unique body.
- ZIP:exact seven-entry set, every file's SHA256, all source/payload/dist
  documents and archive sidecar match. No logs or game assets included.
- Isolated installer:initial install, repeat install, uninstall, install
  after uninstall, second uninstall, genuine busy/free/abandoned mutexes,
  and actual upgrade0.2.0-preview.2→0.2.0 all passed. Owned payload hashes and
  versions match; game files, unrelated mod sentinel and shared loader remain.
  Evidence logs:`work/test-logs`. No real Steam installation was modified.
- `git diff --check` passed.

## Preserved files and limits

Published preview.2 ZIP remains unchanged:
`45A39EDA6A727AA41DBFC41CE6E1218B16940128F706BAB74BD4FCF05250231F`.
The previous generated dist files and RAR are recoverable at
`work/diagnostics/2026-10-12-release-0.2.0/dist-before-release`.
The RAR is restored in dist separately, not included in the release ZIP.

Real Steam plugin remains the prior9A1F47D7 development candidate:
`9A1F47D79F885D805383D42402CC4B4F5CC444FEB93C0D468836294ADE1E5E97`.
The user must close the game and run the new setup to install this release.

No final V3 live purchase, duplicated Island/Tax effect, beach appearance,
World Championships readability, money transfer, four-player restoration,
AFK duration, complete match or five-human networking is certified. Previous
V2 one-human/four-bot observations are not substituted for these checks.
See[release notes](../RELEASE_NOTES.md).
