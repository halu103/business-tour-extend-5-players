# Fifth-seat lobby presentation regression

Run with the workspace .NET 6 runtime:

```powershell
.\.tools\dotnet6\dotnet.exe run --project tests\FifthLobby\FifthLobby.csproj -c Release
```

The pure checks cover every ordered palette of one to four of the build's six
native colors. They require an unused fifth color, preserve the original palette,
and prefer `Red2` (native coral/orange-red, light `#E44D37`) instead of `Blue2`
(cyan). Invalid or duplicate palettes fail closed.

Readiness checks cover ready, unready, and empty slots. Embedded source contracts
require the retained fifth `AvatarOrPortraitConfig` to use actual room readiness
after native identity/color/avatar initialization. They also guard the native
ready decorator refresh for an unchanged ready state, without adding another
decorator, permanent check, callback, or mutation to seats 1–4.

These checks do not confirm rendering or network delivery. In-game validation
must toggle a real fifth occupant between ready and unready, remove the occupant,
and verify the native avatar check and coral selector/title/HUD assets.
