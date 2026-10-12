# Release 0.2.2 English installer validation

Date: 12 October 2026, Asia/Saigon. The user is playing Business Tour and
requested only a build. No game close/launch, visible installer window,
install/uninstall operation or real Steam write was performed this turn.

## Final artifact identity

- ZIP: `artifacts/BusinessTourFiveRealms-0.2.2-win-x64.zip`
- ZIP SHA256: `C40D5E6FA2FA38C356F4C22FE52075BCFA512D2904BB77679281094F6449AA81`
- Plugin SHA256: `CB6295D7A3BE23162624138A21B3108674D882DD91FA9D3A0362D1D348AE23BC`
- Embedded payload SHA256: `4BCA1F5CEDDF62B546E1B6E7342C3BDD26EC6EA1C008497F3320EBC95A30F2E3`
- Plugin source strings, assembly/file/informational version, installer
  version and manifests agree on 0.2.2. V3 inventory remains 40 cells,
  5 players, 5 resorts, 3 Chance, 2 Island, 2 Tax and maximum 2 traps.

## Changes

- Setup/Uninstall headings, labels, buttons, folder instructions, progress,
  warnings, error headings, results and busy messages use simple English.
- Common loader/installation-record and restore-failure messages are clearer.
  Exact paths, hashes and underlying errors remain available for diagnosis.
- Uninstall confirmation and success explicitly include Five Realms settings;
  they do not imply that the shared loader or unrelated mods are removed.
- Check-only success says that no game files were changed, not that all
  possible conflicts or gameplay/network compatibility have been checked.
- Program entry points request English .NET UI resources without changing
  date/number formatting culture. Windows-owned dialog controls can still
  follow Windows language. There is no promise of fully English OS buttons.
- Engine control flow, validation, ownership, backups, restore-on-failure,
  single-instance guards and game behavior are unchanged. The plugin source
  change this turn is release metadata only. No unspecified functional
  install/uninstall failure is claimed fixed by translating its text.

## Passed checks

- All three installer source files are ASCII, and source contracts cover
  simple buttons, dialog instructions, confirmation, busy/result messages
  and UI-only culture selection.
- A separately compiled C#5 .NET Framework STA fixture loads both freshly
  built EXEs. It invokes only `--version`, before maintenance locks or
  payload handling, and checks 0.2.2 plus English UI culture from vi-VN.
  Date/number culture is unchanged.
- Both forms are constructed in memory and never shown. Titles, version,
  folder label, four English buttons, Accept/Cancel actions, control bounds,
  label/button clipping and button overlap pass at 760x520, 680x430 and
  back to 760x520. These are current-system layout measurements, not live
  screenshots or coverage of every Windows DPI setting.
- Ten existing game regression suites pass. Release compilation reports
  0 warnings and 0 errors. Native patch audit remains 49 targets / 48 native
  bodies, zero unreviewed shared bodies and one reviewed constructor alias.
- ZIP has exactly seven intended entries. Dist checksums, source/payload
  documentation, archive contents and sidecar checksum match.
- `git diff --check` passes.

Build and archive evidence is retained in
`work/diagnostics/2026-10-12-release-0.2.2/build.log` and `archive.log`.

## Preserved files and limits

Real Steam plugin hashes `B197139D411733F6C56178F74CC3D0CC895A336342934806A4863AC3D261C035`
before and after this build: the user's installed 0.2.1 was not replaced.

Frozen 0.2.1 ZIP remains
`57233DBB7FE5D2AAAE3FC2A56FEF7638653D1A9B9D0841D198289AF4263A9E30`;
0.2.0 remains
`37BA14D1CF6B96BEF1C252119E906494221186075AD1AACF5AB1ECFA30566FB1`;
preview.2 remains
`45A39EDA6A727AA41DBFC41CE6E1218B16940128F706BAB74BD4FCF05250231F`.
Prior dist and installer logs were copied under this release's diagnostics
folder. Packaging replaces only known generated filenames; no older ZIP
was overwritten or removed.

The isolated mutation cycle and actual 0.2.1-to-0.2.2 upgrade were deliberately
not run while the user plays. The previous release passed its own isolated
install/uninstall/upgrade tests; that is not an end-to-end result for 0.2.2.
No final game UI, gameplay, AFK or five-human/network verification is added.
All existing limits in [release notes](../RELEASE_NOTES.md) remain applicable.
