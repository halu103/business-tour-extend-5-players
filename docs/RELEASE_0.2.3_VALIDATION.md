# Release 0.2.3 Unity library cache recovery

14 October 2026. Local package validation; no live game launch.

## Reported failure and source evidence

The supplied startup screenshot shows BepInEx IL2CPP be.788 with Unity
6000.0.58f2 failing to extract `BepInEx\unity-libs\6000.0.58.zip`:
`End of Central Directory record could not be found`. Interop generation
then fails, followed by missing `UnityEngine.CoreModule` and no loaded plugins.
The screenshot establishes invalid ZIP data, but does not establish whether
the cause was interruption, a network block page, or something else.

The exact loader's
[official source](https://github.com/BepInEx/BepInEx/blob/5b766a3b7f6c164d4798924a93f3acf4db769d06/Runtimes/Unity/BepInEx.Unity.IL2CPP/Il2CppInteropManager.cs)
reuses an existing ZIP with the same filename before considering a download.
Extraction failure leaves the invalid archive in place. Under default
settings, replacing the ZIP allows interop generation on the next launch.

The official library URL returned a valid 2,452,254-byte ZIP, 71 entries,
including `UnityEngine.CoreModule.dll`. Its SHA-256 matches the known-good
local cache and the new pin in `config/compatibility.json`:
`D981F87AF82975A83BC02DD440C976024B39C132EA51A79AD718B18251FECD5D`.

## Passed checks

- Release build: plugin, installer and uninstaller version 0.2.3; no compiler
  warnings/errors. All existing board, topology, HUD, payment, artwork and
  lobby checks passed. Native audit: 49 patch targets / 48 native bodies,
  zero unsafe aliases, one reviewed compatible alias.
- English installer audit and hidden form checks passed.
- Eight isolated cache scenarios passed against the built Setup executable:
  invalid HTML/truncated ZIP; healthy-cache zero downloads; blocked response
  followed by valid retry; three bad responses; interrupted partial writes;
  commit and real rollback for present/absent targets; tampered staged files;
  disabled-regeneration warning with custom configuration preserved.
- Real Setup/Uninstall operations passed on the isolated compatible game
  copy at `work/installer-e2e-game`. The real Steam installation was used
  only as a read-only source of compatible files and build references.
- A missing Unity ZIP was downloaded from the official server and verified.
  A deliberately cached HTML response was detected by read-only verification
  without changing it, then recovered by reinstalling. The backed-up corrupt
  cache and repaired ZIP are retained through the normal backup transaction.
- Install, reinstall, uninstall, install after uninstall, and upgrade from
  verified local release 0.2.1 to 0.2.3 passed. Original game hashes and an
  unrelated plugin sentinel were preserved; uninstall preserved the shared
  loader and verified Unity cache.
- All seven release ZIP entries, document copies, dist checksums and ZIP
  sidecar checksum matched.

Commands: `tools/Build-Release.ps1 -Version 0.2.3`,
`tools/Test-UnityLibraryCache.ps1`, `tools/Test-Release.ps1 -Version 0.2.3`
with the verified 0.2.1 previous release, and
`tools/Test-ReleaseArchive.ps1 -Version 0.2.3`.
Installer logs are in `work/test-logs`.

## Artifact and limits

`artifacts/BusinessTourFiveRealms-0.2.3-win-x64.zip`

SHA-256:
`BA3E5014E24C25C1EA357FD233E2AC8B47C341C579922FE434F98927D572152B`

Unity base libraries and generated game interop assemblies are not bundled.
Setup needs the official library server when no verified ZIP is already
cached. It aborts before installation changes if verification/download fails.
The reporting friend's network and first startup have not been tested.
Custom interop paths/library filenames or disabled regeneration require
separate configuration review. This release does not add gameplay changes.
