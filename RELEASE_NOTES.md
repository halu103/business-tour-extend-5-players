# Five Realms 0.2.3

Unity library cache recovery - 14 October 2026. Unofficial client mod.

- Fix the installer gap behind `End of Central Directory record could not
  be found` on `BepInEx\unity-libs\6000.0.58.zip`. With default BepInEx
  settings, that failure prevents interop generation and then produces the
  missing `UnityEngine.CoreModule` error before Five Realms is loaded.
- Check the cached ZIP using the supported SHA-256 and required core-module
  entry. Download a missing or damaged archive from the official BepInEx
  library server into staging; retry failed verification up to three times.
- Complete all download checks before modifying game files. Replace the
  archive through the existing backup and rollback transaction. Keep a
  healthy cache without downloading it again.
- Warn when `[IL2CPP] UpdateInteropAssemblies = false` blocks normal
  regeneration. User configuration and custom interop paths are not rewritten.
- Uninstall keeps the shared Unity library cache alongside BepInEx. The
  release does not bundle Unity or generated game interop assemblies.
- Retain the English installer and 0.2.2 gameplay. Live recovery on the
  reporting friend's PC still requires running Setup and restarting there.

## Previous 0.2.2 installer changes

English installer release — 12 October 2026. Unofficial client mod.

Version 0.2.2 changes Setup and Uninstall text, not gameplay or the game UI.
The 0.2.1 presentation fixes below are retained. No game was closed or
reopened for this release. Automated checks do not prove online stability.

## Installer changes

- Simple English for setup/uninstall titles, labels, buttons, folder-picker
  instructions, progress, warnings, error headings and result messages.
- Clear instructions when Setup or Uninstall is already open: close the
  existing window or wait for it to finish before trying again.
- Uninstall confirmation explicitly includes Five Realms settings. The
  shared BepInEx loader, unrelated mods and Steam game files are kept.
- **Check files** reports that no game files were changed. It does not claim
  to test online compatibility or every possible mod conflict.
- .NET UI resources request English without changing date/number culture.
  Windows-owned message-box buttons and folder-picker controls can still
  follow the Windows display language.
- File validation, backups, restore-on-failure, single-instance guards and
  file ownership checks remain unchanged. No unspecified install/uninstall
  failure is claimed fixed by this wording update.

## Retained 0.2.1 presentation fixes

The previous hotfix targets the 0.2.0 screenshot regressions: tile captions drawn
over characters, Chance artwork spilling into adjacent cells, and shifted
festival/monopoly flags. It does not change the V3 graph or native economics.
It is packaged without reopening the game, as requested. Automated checks
are not a claim that its live appearance or online stability is verified.

- Return captions to board depth and a board-information sorting layer below
  characters, instead of moving every label close to the camera. Names and
  rent amounts keep separate fitted bands and native text.
- Rent uses a per-instance clone of its matching native TMP Overlay shader
  at transparent queue 3000, retaining the atlas and below-character sorting.
  This avoids opaque-house depth without moving rent close to the camera.
  Unknown/missing shader families keep their original material and report a
  warning; house occlusion may remain in that fallback. Shared font assets
  are never edited. Owned material references are restored before disposal,
  with deferred retry if a native setter temporarily fails.
- Fit separate native Chance/crown sprite bounds inside their own cell;
  retain their native rotation and proportions, shrinking only when needed.
  Basic CellService wheels are baked into the ground texture, not catalogued
  sprites: inset that painted surface by 14% over the unchanged full-cell bed.
  Labels, collider/clicks, route and other cell textures stay unchanged.
  Both paths use the original baseline, without cumulative shrink.
- Reanchor native festival/monopoly animation owners and discover late-created
  artwork through throttled refresh. Keep internal frame animation, scale and
  rotation native. Do not flatten upright flags or resize all service models.
- Guard artwork bindings by object generation and hierarchy, avoid applying
  parent/child offsets twice, and restore only still-owned overrides.

## Retained V3 rules

40 cells, five sides of eight: 23 cities, 5 resorts, 3 actual Chance,
2 Lost Island, 2 Tax, 2 Rogue Trap and one each of Start, World Championships
and World Tour. Chance card effects and native service behavior are unchanged.
Original ownable cells retain their economics and groups.

Traps 12/32, Lost Island 8/28 and Tax 16/36 are each 20 route steps apart.
Chance 20/30/39 has circular gaps 10/9/21, not equal spacing. The pentagon is
not centrally symmetric; half a lap is not an exact geometric diameter.
The map marker remains `BT5_FIVE_REALMS_V3`. It is not a full peer-version
handshake: use the same exact 0.2.2 package on every client.

Compatible Classic beach/island/stadium/ship artwork, Fantasy resort caption
binding, five player cards, coral-orange fifth color, native ready check,
vertical controls, emote fitting, money presentation and the local AFK guard
are retained. Four-player board/HUD changes remain outside the special-map
scope; background configuration remains global.

## Verification and limits

The release build runs ten regression suites and the native-body alias audit.
An additional check covers English installer text and in-memory form layout.
Packaging checks exact game hashes, plugin/version metadata, payload manifests,
archive entries and checksums. The previous 0.2.1 package passed isolated
install/uninstall/upgrade tests. Those mutation tests were not rerun for
0.2.2 because the user is playing and requested only the build. No real
Steam installation was changed. Results are recorded in the workspace's
0.2.2 validation report.

- Renderer/material behavior and final Chance/flag alignment still require
  a live check. Containment tests use mathematical bounds, not screenshots.
- World Championships and other long captions can remain small because
  they must fit their own cell; a readability target is not a guaranteed
  visible letter size.
- Purchase/payment states, native duplicated Island/Tax effects, five-resort
  monopoly, bot/card destination choices and full matches remain unverified.
- Money animation size, repeated ready/Pause transitions, ordinary-map
  restoration, long AFK, five humans, invites, reconnects and host migration
  still need gameplay regression.
- Trap captions can still say CHANCE because native text is retained. This
  does not increase the three actual Chance cells.
- Server/network kicks are not prevented. The native game may transmit
  telemetry; local diagnostics are not a no-network-reporting guarantee.

Use a private, no-bet bot room first, never ranked/tournament/wager matches.
Stop if purchase UI, money, movement or turns are incorrect.

Close the game, extract the ZIP and run `BusinessTourFiveRealms-Setup.exe`.
Uninstall removes Five Realms files and settings and retains the shared
loader and unrelated plugins. The 0.2.0, 0.2.1 and prerelease ZIPs remain
separate and unchanged.
See [README.md](README.md).
