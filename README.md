# Business Tour — Five Realms

**0.2.0-preview.2 — experimental pre-release** for Business Tour - Online Multiplayer Board Game (Steam app `397900`, Unity IL2CPP).

This is a test build, not a stable release. Use a private, no-bet room with bots first. Do not use it in ranked games, tournaments or paid rooms.

## Included in this preview

- A selectable **Five Realms — 5 Players** map. Only this map requests a five-player room; original maps retain their native four-player limit.
- Five native player cards in a compact left-hand rail, a matching five-card Pause screen, and visible native toolbar buttons.
- A pointed, isometric pentagon with shared tile boundaries and a uniform base, replacing the earlier distorted square-prefab outline.
- Five seeded visual regions drawn from the game's three native cell-skin families: `Classic`, `Fantasy` and `Wonderland`. Repeated families are expected.
- At most **two generated/static Rogue Trap cells** in total, including any traps already present in the base map. Generated traps use different regions when suitable cells exist.
- A correction for loading-screen names being covered by portraits. This latest correction has compiled but has not yet been verified in a live game.
- Native purchase/repurchase/escape dialogs enlarged up to 15%, capped to fit the viewport without replacing native buttons or their animations.
- Rent/debt captions and money amounts given a 15% font-size ceiling with safe autosizing. Tightly bounded labels may retain their native size; the full-screen sale-selection view is not enlarged.
- Native tile names aligned with the camera and fitted inside their own tile. Names and rent text use separate inner/outer bands. The math is tested; the new live appearance is not yet verified and long names can become small.
- Trap type transitions normalized to native Service/NoBuilding metadata instead of leaving a trap in its former city's suit.
- Background connection settings and an attempted client-side AFK fix. **AFK protection is not reliable in this preview:** the latest bot test still exited after three missed rolls with `PreventAfkTurnKick=true`.

The map still uses the native **32-cell route** and native movement, purchase, money and turn rules. It does **not** yet expand four resorts/beaches to five or rebalance all player-count-dependent rules. The agreed next topology is **40 cells: five sides of eight**, preserving the original 32 cells. That expansion is not enabled in this preview. A pentagonal appearance and a fifth player do not, by themselves, implement a complete five-player ruleset.

## Installation

1. Close Business Tour and any Five Realms setup/uninstall window.
2. Extract `BusinessTourFiveRealms-0.2.0-preview.2-win-x64.zip`.
3. Run `BusinessTourFiveRealms-Setup.exe`.
4. Confirm the detected game folder and choose **Install / Cài**.
5. Start the game through Steam. Create a private **no-bet** room and select **Five Realms — 5 Players**. For an initial test, add four bots.

Install the **same pre-release package** on every participating client. Do not mix vanilla clients, 0.1.0, or different previews in a Five Realms room. The standalone installer follows the project's existing WinForms setup workflow.

To remove the mod, close the game and run `BusinessTourFiveRealms-Uninstall.exe`. The uninstaller removes this project's owned plugin files and retains the shared BepInEx loader and unrelated plugins. It does not uninstall the Steam game.

The first start may take longer while BepInEx generates its IL2CPP cache. If the screen stays blank after the window title returns to `BusinessTour`, close the game and inspect `BepInEx\LogOutput.log` instead of waiting indefinitely. Do not publish logs without checking them for personal data.

## Compatibility and limits

The installer is pinned to Steam build **25392206**, game **2.21**, Unity **6000.0.58f2**, and BepInEx IL2CPP x64 build **788**. It verifies exact game-file hashes and refuses an incompatible installation.

This is a client mod; it does not modify Business Tour's servers. Five independent human accounts, Steam invites, reconnects and host migration have not been verified. Server maintenance, server-side room limits and administrative kicks cannot be prevented by this client patch.

Original maps are intentionally excluded from the special board/HUD changes. Background/AFK settings are global, as requested earlier. Their configuration is in `BepInEx\config\vn.businesstour.fiverealms.cfg`; disable `PreventAfkTurnKick` to request native AFK behavior, but enabling it is not a guarantee of protection.

## Verification status

- Live Steam test: one human plus four bots entered Five Realms; five player cards and moving pawns were observed.
- Live Steam test: the canonical pentagon rendered successfully. Pause displayed five matching cards and closed back to gameplay.
- Automated checks cover trap selection, board geometry, native tile clipping, camera-aligned label footprints and payment-layout sizing. The release build runs these checks and audits native patch targets before packaging.
- Installer checks run against an isolated copy of compatible game files, not the real Steam folder. They verify the exact packaged DLL/version/hash and preservation of game files, unrelated plugins and the shared loader.
- **Not complete:** live verification of the newest loading-name/label/payment changes and trap metadata correction, repeated Pause lifecycle, normal-map regression after five-player play, five human clients, long AFK/background sessions and a full game through bankruptcy/endgame.

See [PRE_RELEASE_NOTES.md](PRE_RELEASE_NOTES.md) for known issues and follow-up work. Passing build/installer tests is not evidence that the online five-player mode is stable.

## Building and testing

Requirements: the exact compatible Steam installation, generated BepInEx interop references, and the project's cached BepInEx package and portable .NET SDK.

```powershell
pwsh -NoProfile -File .\tools\Build-Release.ps1 -Version 0.2.0-preview.2
pwsh -NoProfile -File .\tools\Test-Release.ps1 -Version 0.2.0-preview.2
```

Packaging and isolated installer tests do not write into the real Steam installation. Assembly/BepInEx numeric version `0.2.0` is retained for loader compatibility; the DLL informational version, startup message, installer, manifests and archive identify `0.2.0-preview.2`.
