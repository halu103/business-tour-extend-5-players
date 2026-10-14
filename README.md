# Business Tour — Five Realms

**0.2.3 Unity cache recovery release package** for Business Tour - Online Multiplayer Board Game
(Steam app `397900`, Unity IL2CPP). Unofficial client mod.

This package is built at the user's request without reopening the game for
the final changes. **Release is a packaging channel, not a claim of fully
verified online stability.** Use a private, no-bet room with bots first.
Do not use ranked games, tournaments or wagers.

## Features

- Setup checks the Unity base-library ZIP before installation. A missing or
  damaged cache is downloaded from the official BepInEx server, verified
  against the supported SHA-256 and ZIP contents, and installed with a
  recoverable backup. Invalid or interrupted downloads are retried up to
  three times; if all fail, installation stops before changing game files.
- Setup and Uninstall use simple English for their own labels, buttons,
  progress, warnings and results. Uninstall clearly states that Five Realms
  settings are removed too, while BepInEx and other mods are kept.
  Windows-owned folder-picker and message-box buttons can still follow the
  Windows display language. This release retains the 0.2.1 game changes.
- Select **Five Realms — 5 Players** in the native map selector. Original maps
  remain scoped to their native four-player board, rules and UI.
- A native 40-cell route: five sides of eight cells, drawn as a pointed
  isometric pentagon. Inventory: 23 cities, 5 resorts/beaches, 3 Chance,
  2 Lost Island, 2 Tax, 2 Rogue Trap, 1 Start, 1 World Championships and
  1 World Tour. Three added cities form their own group.
- Original cities and resorts retain their positions, complete native
  settings, prices and groups. Some service positions move to distribute
  effects. Chance, Lost Island and Tax reuse complete native templates and
  their original behavior, not trap behavior. Chance card effects are not
  restricted to three types: there are three actual Chance cells.
- Traps at indices 12/32, Lost Island at 8/28, and Tax at 16/36. Each pair
  is 20 route steps apart. Chance at 20/30/39 has circular gaps of 10/9/21
  steps. These are fixed positions; only the five regional themes are seeded.
  A pentagon is not centrally symmetric: half a lap is not an exact
  180-degree geometric diameter. The selected traps are about 178.4 degrees
  apart through the center.
- Five seeded visual regions use Classic, Fantasy and Wonderland. Real
  Resort beaches, Lost Island, World Championships and World Tour use
  compatible Classic artwork. This avoids Wonderland's different Alice Room
  service art and missing regional service models. Cell rules and rents are
  not changed by this art selection.
- Five compact native player cards in the left rail, five Pause cards,
  a distinct native coral-orange fifth color, and a fifth lobby check based
  on actual readiness. Vertical right-hand controls and fitted native emotes.
- Native purchase/repurchase/escape window geometry, buttons and animations
  are retained. Missing Fantasy resort caption references are bound to
  existing native Text children. No hard-coded price or translated string
  is substituted. Rent/debt text has a bounded 15% autosizing allowance.
- A tile strip about 29% deeper than the earlier design, while keeping the
  board's outer frame. Names and rents use separate, camera-aligned bands;
  names no longer use the character foreground layer. Service captions without a rent view can use
  the full cell. Bounds-based targets are 20 pixels per name line and 22
  for rent. Long captions may still be smaller to avoid adjacent cells.
  A per-view native TMP Overlay clone keeps rent above opaque houses while
  sorting below characters, retaining the font atlas. Missing/unknown shader
  families keep their original material and log a warning instead; house
  occlusion can remain in that fallback. Chance artwork is shrink-to-fit,
  including a 14% inset for wheels baked into the tile texture and fitted
  separate crown indicators. The full cell, clicks, text and route are not
  shrunk. Flag animation owners are reanchored without flattening their art.
- Moving money piles retain native templates, with compact-HUD compensation
  targeting 130% of an uncompressed native pile; center money text is larger.
- Background connection settings and a local missed-roll AFK callback guard.
  This does **not** prevent server kicks, maintenance or network disconnects,
  and long unattended protection is not proven.

Native dice, starting money, lap rewards, tax amounts, rent multipliers,
trap duration and triple-monopoly threshold are not blindly incremented.
Map generation clones native data privately and validates graph, art lookup
and five resort rent tiers before committing the expanded array. A 32-cell
fallback is a failed expansion, not a successful five-sided game.

## Install or update

1. Close Business Tour and any Five Realms setup/uninstall window.
2. Extract `BusinessTourFiveRealms-0.2.2-win-x64.zip`.
3. Run `BusinessTourFiveRealms-Setup.exe`.
4. Confirm the game folder and choose **Install**. Use **Check files** to
   check compatibility without changing game files.
5. Start through Steam. Create a private **no-bet** room, select
   **Five Realms — 5 Players**, and add four bots for an initial test.

Use the **same exact package on every client**. Do not mix vanilla clients,
0.1.0, preview.1, preview.2, 0.2.0, 0.2.1 or the V2 development DLL with this release.
The graph/room marker is `BT5_FIVE_REALMS_V3`. It detects earlier graph
markers, but is not a complete peer-version handshake or backend guarantee.

The standalone setup follows the project's existing WinForms installer
workflow. To remove the mod, close the game and run
`BusinessTourFiveRealms-Uninstall.exe`. It removes Five Realms files and
settings, retaining the shared BepInEx loader, unrelated plugins and the
Steam game. Close any existing Setup or Uninstall window before trying again.

First startup can take longer while BepInEx generates its IL2CPP cache.
If no verified library ZIP is already cached, Setup requires access to
`https://unity.bepinex.dev/libraries/6000.0.58.zip`. A healthy cache avoids
this download. Unity base libraries and generated game interop assemblies
are not bundled in this release.
If the UI stays blank, close the game and inspect `BepInEx\LogOutput.log`.
Review logs for personal data before sharing. The native game may send its
own telemetry; the mod's local diagnostics are not a privacy guarantee.

### Failed ZIP extraction / missing UnityEngine.CoreModule

If startup reports `End of Central Directory record could not be found`
for `BepInEx\unity-libs\6000.0.58.zip`, followed by a missing
`UnityEngine.CoreModule` assembly, close the game and run **Install** from
the 0.2.3 Setup against that same game folder. Setup verifies and replaces
the broken archive while backing up its original bytes. BepInEx generates
the interop assemblies on the next launch; allow the first startup to finish.

For manual recovery, download the official ZIP linked above and verify its
SHA-256:
`D981F87AF82975A83BC02DD440C976024B39C132EA51A79AD718B18251FECD5D`.
With the game closed, move the old `6000.0.58.zip` to a backup folder and
place the verified replacement in `BepInEx\unity-libs`. Keep the ZIP intact;
do not copy its managed DLLs into `BepInEx\interop` or `BepInEx\core`.

The default `[IL2CPP] UpdateInteropAssemblies = true` in
`BepInEx\config\BepInEx.cfg` allows regeneration. Setup warns if it is
disabled and preserves that setting. Custom interop paths or a custom
library-source filename need to be checked separately.

## Compatibility and verification

Pinned to Steam build **25392206**, game **2.21**, Unity **6000.0.58f2**,
and BepInEx IL2CPP x64 **788**. Setup verifies exact compatible game hashes.

Earlier development tests reached native 40-cell private gameplay with one
human and four bots, restored the stadium/ship/island models, and displayed
five cards, coral-orange, visible rent prices and vertical controls. The
World Championships caption remained too small in that session.

The user's 0.2.0 screenshot shows the V3 board and five cards, but also labels
over pawns, oversized Chance artwork and displaced flags. This hotfix targets
those presentation regressions without changing the V3 route or economics.
Version 0.2.2 changes installer wording and language, not game logic.
Version 0.2.3 adds verified Unity cache recovery to Setup; gameplay remains
the same as 0.2.2.
It has **not** been retested in-game, as requested. Automated tests and
installer checks do not replace gameplay verification. See
[release notes and limitations](RELEASE_NOTES.md).

Still unverified: repeated ready/unready and Pause transitions; every
purchase/payment state; satisfactory animated money size; ordinary-map
restoration after Five Realms; all cards/bots and a complete game through
bankruptcy/endgame; long AFK sessions; five human accounts, invites,
reconnects and host migration. Stop if a dialog is blank or turn/money
state becomes incorrect. Do not treat this package as certified stable.

Background/AFK configuration is in
`BepInEx\config\vn.businesstour.fiverealms.cfg`. Background settings are global;
the five-player board/HUD changes are map-scoped. Disable
`PreventAfkTurnKick` to request native AFK behavior.

## Build and test

Requires the exact compatible Steam installation, generated BepInEx interop
references, cached BepInEx archive, portable .NET SDK and existing restore
assets. The release build uses those local dependencies without restoration.

```powershell
pwsh -NoProfile -File .\tools\Build-Release.ps1 -Version 0.2.3
pwsh -NoProfile -File .\tools\Test-InstallerEnglish.ps1 -Version 0.2.3 -SetupExe .\dist\BusinessTourFiveRealms-Setup.exe -UninstallExe .\dist\BusinessTourFiveRealms-Uninstall.exe
pwsh -NoProfile -File .\tools\Test-UnityLibraryCache.ps1
pwsh -NoProfile -File .\tools\Test-Release.ps1 -Version 0.2.3 -PreviousReleaseZip .\artifacts\BusinessTourFiveRealms-0.2.1-win-x64.zip
```

Regression suites cover layout, geometry, projection, labels, payment,
HUD, fifth lobby, topology, service art and artwork layout. A native-body alias audit rejects
unreviewed shared detours. Isolated installer tests do not change the real
Steam installation, but the game must be closed; the maintenance guard is
not bypassed. Previous prerelease archives remain separate and unchanged.
