[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$Version = '0.2.3'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$compatPath = Join-Path $projectRoot 'config\compatibility.json'
$compat = Get-Content -LiteralPath $compatPath -Raw | ConvertFrom-Json
$workRoot = Join-Path $projectRoot 'work\release'
$payloadRoot = Join-Path $workRoot 'payload'
$generatedRoot = Join-Path $workRoot 'generated'
$outputRoot = Join-Path $projectRoot 'dist'
$artifactRoot = Join-Path $projectRoot 'artifacts'
$releaseFileNames = @('BusinessTourFiveRealms-Setup.exe', 'BusinessTourFiveRealms-Uninstall.exe',
    'README.md', 'RELEASE_NOTES.md', 'PRE_RELEASE_NOTES.md', 'THIRD_PARTY_NOTICES.md', 'SHA256SUMS.txt')
$pluginProject = Join-Path $projectRoot 'src\BusinessTourFiveRealms\BusinessTourFiveRealms.csproj'
$pluginDll = Join-Path $projectRoot 'src\BusinessTourFiveRealms\bin\Release\net6.0\BusinessTourFiveRealms.dll'
$bepInExZip = Join-Path $projectRoot ('.tools\bepinex-788\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.{0}+5b766a3.zip' -f $compat.bepInExBuild)
$dotnet = Join-Path $projectRoot '.tools\dotnet6\dotnet.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$versionMatch = [regex]::Match($Version, '^(?<numeric>\d+\.\d+\.\d+)(?:-[0-9A-Za-z.-]+)?$')
if (-not $versionMatch.Success) { throw 'Use a three-part version with an optional pre-release suffix.' }
$assemblyVersion = $versionMatch.Groups['numeric'].Value

function Assert-WorkspaceChild([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    $prefix = $projectRoot.TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing a path outside the workspace: $full"
    }
    return $full
}

function Reset-WorkspaceDirectory([string]$Path) {
    $full = Assert-WorkspaceChild $Path
    if (Test-Path -LiteralPath $full) {
        Remove-Item -LiteralPath $full -Recurse -Force
    }
    New-Item -Path $full -ItemType Directory -Force | Out-Null
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-Hash([string]$Path, [string]$Expected) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file is missing: $Path"
    }
    $actual = Get-Sha256 $Path
    if ($actual -ne $Expected.ToUpperInvariant()) {
        throw "Hash mismatch for $Path`nExpected $Expected`nActual   $actual"
    }
}

function Find-BusinessTourRoot {
    $roots = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($registryPath in @(
        'HKCU:\Software\Valve\Steam',
        'HKLM:\SOFTWARE\WOW6432Node\Valve\Steam',
        'HKLM:\SOFTWARE\Valve\Steam')) {
        if (-not (Test-Path -LiteralPath $registryPath)) { continue }
        $key = Get-ItemProperty -LiteralPath $registryPath
        foreach ($name in @('SteamPath', 'InstallPath')) {
            $property = $key.PSObject.Properties[$name]
            $value = if ($null -ne $property) { [string]$property.Value } else { $null }
            if ($value -and (Test-Path -LiteralPath $value -PathType Container)) {
                [void]$roots.Add([System.IO.Path]::GetFullPath($value))
            }
        }
    }

    $libraries = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($steamRoot in $roots) {
        [void]$libraries.Add($steamRoot)
        $vdf = Join-Path $steamRoot 'steamapps\libraryfolders.vdf'
        if (Test-Path -LiteralPath $vdf) {
            $text = Get-Content -LiteralPath $vdf -Raw
            foreach ($match in [regex]::Matches($text, '"path"\s+"([^"]+)"', 'IgnoreCase')) {
                $library = $match.Groups[1].Value.Replace('\\', '\')
                if (Test-Path -LiteralPath $library -PathType Container) {
                    [void]$libraries.Add([System.IO.Path]::GetFullPath($library))
                }
            }
        }
    }

    foreach ($library in $libraries) {
        $manifest = Join-Path $library ('steamapps\appmanifest_{0}.acf' -f $compat.steamAppId)
        if (-not (Test-Path -LiteralPath $manifest)) { continue }
        $manifestText = Get-Content -LiteralPath $manifest -Raw
        $match = [regex]::Match($manifestText, '"installdir"\s+"([^"]+)"', 'IgnoreCase')
        if (-not $match.Success) { continue }
        $candidate = Join-Path $library (Join-Path 'steamapps\common' $match.Groups[1].Value)
        if (Test-Path -LiteralPath (Join-Path $candidate 'BusinessTour.exe')) {
            return [System.IO.Path]::GetFullPath($candidate)
        }
    }
    throw 'Business Tour was not found. Pass -GameRoot with the folder containing BusinessTour.exe.'
}

function Assert-CompatibleGame([string]$Root) {
    $rootFull = [System.IO.Path]::GetFullPath($Root)
    $checks = [ordered]@{
        'BusinessTour.exe' = $compat.businessTourExeSha256
        'GameAssembly.dll' = $compat.gameAssemblySha256
        'BusinessTour_Data\il2cpp_data\Metadata\global-metadata.dat' = $compat.globalMetadataSha256
        'UnityPlayer.dll' = $compat.unityPlayerSha256
        'BusinessTour_Data\globalgamemanagers' = $compat.globalGameManagersSha256
    }
    foreach ($relative in $checks.Keys) {
        Assert-Hash (Join-Path $rootFull $relative) $checks[$relative]
    }
    $unityVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $rootFull 'UnityPlayer.dll')).ProductVersion
    if ($unityVersion -ne $compat.unityProductVersion) {
        throw "Unsupported Unity version: $unityVersion"
    }
    $appInfo = (Get-Content -LiteralPath (Join-Path $rootFull 'BusinessTour_Data\app.info') -Raw).Replace("`r", '').TrimEnd("`n")
    if ($appInfo -ne "8floor`nBusinessTour") {
        throw 'BusinessTour_Data\app.info does not identify the supported game.'
    }
    Write-Host "Compatible game verified: Steam build $($compat.steamBuildId), Unity $($compat.unityVersion)."
}

function Copy-Tree([string]$Source, [string]$Destination) {
    New-Item -Path $Destination -ItemType Directory -Force | Out-Null
    Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $Destination -Recurse -Force
}

function Escape-CSharp([string]$Value) {
    return $Value.Replace('\', '\\').Replace('"', '\"')
}

if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $GameRoot = Find-BusinessTourRoot
}
Assert-CompatibleGame $GameRoot
Assert-Hash $bepInExZip $compat.bepInExPackageSha256
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) { throw "Portable .NET SDK is missing: $dotnet" }
if (-not (Test-Path -LiteralPath $csc -PathType Leaf)) { throw "64-bit .NET Framework compiler is missing: $csc" }

& (Join-Path $PSScriptRoot 'Test-InstallerEnglish.ps1') -Version $Version -SourceOnly

# Test the exact pure generation policy compiled into the mod before clearing
# or replacing any existing release outputs.
& $dotnet run --project (Join-Path $projectRoot 'tests\LayoutRules\LayoutRules.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Two-trap generation regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\BoardGeometry\BoardGeometry.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Canonical board geometry regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\TileProjection\TileProjection.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Native tile clipping regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\BoardLabels\BoardLabels.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Native label footprint regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\BoardArtwork\BoardArtwork.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Native Chance artwork containment regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\PaymentLayout\PaymentLayout.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Payment UI sizing regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\GameplayHud\GameplayHud.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Gameplay toolbar, popup and money presentation regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\FifthLobby\FifthLobby.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Fifth-seat readiness and palette regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\Topology\Topology.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Forty-cell topology regression failed.' }
& $dotnet run --project (Join-Path $projectRoot 'tests\ServiceArt\ServiceArt.csproj') -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Native service artwork compatibility regression failed.' }

$referenceRoot = Join-Path $GameRoot 'BepInEx'
if (-not (Test-Path -LiteralPath (Join-Path $referenceRoot 'interop\Assembly-CSharp.dll') -PathType Leaf)) {
    throw 'Open the compatible game with BepInEx once before building; current game interop references are required.'
}
& $dotnet build $pluginProject -c Release --no-restore --nologo ("/p:BepInExRoot={0}" -f $referenceRoot) ("/p:Version={0}" -f $Version)
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }
$actualAssemblyVersion = [System.Reflection.AssemblyName]::GetAssemblyName($pluginDll).Version.ToString(3)
$actualReleaseVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($pluginDll).ProductVersion
$pluginSourceText = Get-Content -LiteralPath (Join-Path $projectRoot 'src\BusinessTourFiveRealms\Plugin.cs') -Raw
$pluginVersionMatch = [regex]::Match($pluginSourceText, 'public const string Version\s*=\s*"([^"]+)"')
$releaseVersionMatch = [regex]::Match($pluginSourceText, 'public const string ReleaseVersion\s*=\s*"([^"]+)"')
if ($actualAssemblyVersion -ne $assemblyVersion -or $actualReleaseVersion -ne $Version -or
    $pluginVersionMatch.Groups[1].Value -ne $assemblyVersion -or $releaseVersionMatch.Groups[1].Value -ne $Version) {
    throw "DLL/plugin/release version mismatch: assembly=$actualAssemblyVersion, release=$actualReleaseVersion, requested=$Version."
}

# Audit the newly built patch targets against this exact game's native bodies.
# Unknown targets, unresolved addresses, and unreviewed aliases must stop packaging.
& (Join-Path $PSScriptRoot 'Audit-NativePatchAliases.ps1') `
    -GameRoot $GameRoot `
    -PluginSource (Join-Path $projectRoot 'src\BusinessTourFiveRealms\Plugin.cs') `
    -ModAssembly $pluginDll `
    -FailOnUnsafeAliases

# Keep the previous release intact until compilation, metadata and native-patch
# checks succeed. Only these validated generated directories are replaced.
Reset-WorkspaceDirectory $workRoot
# Dist may contain the user's older RARs or other exports. Replace only our
# generated filenames and package the explicit release set, never clear it.
[void](Assert-WorkspaceChild $outputRoot)
New-Item -Path $outputRoot -ItemType Directory -Force | Out-Null
New-Item -Path $artifactRoot -ItemType Directory -Force | Out-Null
New-Item -Path $payloadRoot -ItemType Directory -Force | Out-Null
New-Item -Path $generatedRoot -ItemType Directory -Force | Out-Null

$loaderRoot = Join-Path $workRoot 'loader'
Expand-Archive -LiteralPath $bepInExZip -DestinationPath $loaderRoot -Force
Copy-Tree $loaderRoot $payloadRoot

$modRoot = Join-Path $payloadRoot 'BepInEx\plugins\BusinessTourFiveRealms'
New-Item -Path $modRoot -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination (Join-Path $modRoot 'BusinessTourFiveRealms.dll') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $modRoot 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $modRoot 'THIRD_PARTY_NOTICES.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'PRE_RELEASE_NOTES.md') -Destination (Join-Path $modRoot 'PRE_RELEASE_NOTES.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'RELEASE_NOTES.md') -Destination (Join-Path $modRoot 'RELEASE_NOTES.md') -Force

$modManifest = [ordered]@{
    modId = 'vn.businesstour.fiverealms'
    name = 'Business Tour Five Realms'
    version = $Version
    channel = if ($Version.Contains('-')) { 'pre-release' } else { 'release' }
    steamAppId = [string]$compat.steamAppId
    steamBuildId = [string]$compat.steamBuildId
    unityVersion = [string]$compat.unityVersion
    playerCount = 5
    boardCellCount = 40
    fifthResortImplemented = $true
    maximumStaticTraps = 2
    chanceCellCount = 3
    lostIslandCellCount = 2
    taxCellCount = 2
    resortCellCount = 5
    mapId = 'BT5_FIVE_REALMS_V3'
    boardShape = 'five-sided-pointed-pentagon'
}
$modManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $modRoot 'mod-manifest.json') -Encoding utf8NoBOM

$payloadFiles = @()
foreach ($file in Get-ChildItem -LiteralPath $payloadRoot -Recurse -File | Sort-Object FullName) {
    $relative = $file.FullName.Substring($payloadRoot.Length).TrimStart('\').Replace('/', '\')
    $scope = if ($relative.StartsWith('BepInEx\plugins\BusinessTourFiveRealms\', [System.StringComparison]::OrdinalIgnoreCase)) { 'mod' } else { 'loader' }
    $payloadFiles += [ordered]@{
        Path = $relative
        Sha256 = Get-Sha256 $file.FullName
        Length = [long]$file.Length
        Scope = $scope
    }
}
$payloadManifest = [ordered]@{
    ModId = 'vn.businesstour.fiverealms'
    ModVersion = $Version
    BepInExBuild = [string]$compat.bepInExBuild
    Files = $payloadFiles
}
$payloadManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $payloadRoot 'payload-manifest.json') -Encoding utf8NoBOM

$payloadZip = Join-Path $workRoot 'BusinessTourFiveRealms.Payload.zip'
Compress-Archive -Path (Join-Path $payloadRoot '*') -DestinationPath $payloadZip -CompressionLevel Optimal
$payloadSha = Get-Sha256 $payloadZip

$buildInfoPath = Join-Path $generatedRoot 'BuildInfo.cs'
$buildInfo = @"
namespace BusinessTourFiveRealmsInstaller
{
    internal static class BuildInfo
    {
        internal const string ModId = "vn.businesstour.fiverealms";
        internal const string ModVersion = "$(Escape-CSharp $Version)";
        internal const string BepInExBuild = "$(Escape-CSharp ([string]$compat.bepInExBuild))";
        internal const string SteamBuildId = "$(Escape-CSharp ([string]$compat.steamBuildId))";
        internal const string UnityVersion = "$(Escape-CSharp ([string]$compat.unityProductVersion))";
        internal const string UnityLibrariesVersion = "$(Escape-CSharp ([string]$compat.unityLibrariesVersion))";
        internal const string UnityLibrariesUrl = "$(Escape-CSharp ([string]$compat.unityLibrariesUrl))";
        internal const string UnityLibrariesSha256 = "$(Escape-CSharp ([string]$compat.unityLibrariesSha256))";
        internal const string BusinessTourExeSha256 = "$(Escape-CSharp ([string]$compat.businessTourExeSha256))";
        internal const string GameAssemblySha256 = "$(Escape-CSharp ([string]$compat.gameAssemblySha256))";
        internal const string GlobalMetadataSha256 = "$(Escape-CSharp ([string]$compat.globalMetadataSha256))";
        internal const string UnityPlayerSha256 = "$(Escape-CSharp ([string]$compat.unityPlayerSha256))";
        internal const string GlobalGameManagersSha256 = "$(Escape-CSharp ([string]$compat.globalGameManagersSha256))";
        internal const string PayloadResourceName = "BusinessTourFiveRealms.Payload.zip";
        internal const string PayloadSha256 = "$payloadSha";
    }
}
"@
$buildInfo | Set-Content -LiteralPath $buildInfoPath -Encoding utf8NoBOM

$framework = Split-Path -Parent $csc
$references = @(
    (Join-Path $framework 'System.dll'),
    (Join-Path $framework 'System.Core.dll'),
    (Join-Path $framework 'System.Drawing.dll'),
    (Join-Path $framework 'System.Windows.Forms.dll'),
    (Join-Path $framework 'System.IO.Compression.dll'),
    (Join-Path $framework 'System.IO.Compression.FileSystem.dll'),
    (Join-Path $framework 'System.Web.Extensions.dll')
)
$referenceArgs = @($references | ForEach-Object { '/reference:{0}' -f $_ })
$applicationManifest = Join-Path $projectRoot 'installer\app.manifest'
$commonArgs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/langversion:5', '/codepage:65001', ('/win32manifest:{0}' -f $applicationManifest)) + $referenceArgs

$setupExe = Join-Path $outputRoot 'BusinessTourFiveRealms-Setup.exe'
$setupArgs = $commonArgs + @(
    ('/out:{0}' -f $setupExe),
    '/main:BusinessTourFiveRealmsInstaller.SetupProgram',
    ('/resource:{0},BusinessTourFiveRealms.Payload.zip' -f $payloadZip),
    (Join-Path $projectRoot 'installer\Shared.cs'),
    (Join-Path $projectRoot 'installer\Installer.cs'),
    (Join-Path $projectRoot 'installer\UnityLibraryCache.cs'),
    $buildInfoPath)
& $csc @setupArgs
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }

$uninstallExe = Join-Path $outputRoot 'BusinessTourFiveRealms-Uninstall.exe'
$uninstallArgs = $commonArgs + @(
    ('/out:{0}' -f $uninstallExe),
    '/main:BusinessTourFiveRealmsInstaller.UninstallProgram',
    (Join-Path $projectRoot 'installer\Shared.cs'),
    (Join-Path $projectRoot 'installer\Uninstaller.cs'),
    $buildInfoPath)
& $csc @uninstallArgs
if ($LASTEXITCODE -ne 0) { throw 'Uninstaller build failed.' }

# Inspect fresh forms in memory without opening a window or touching game files.
& (Join-Path $PSScriptRoot 'Test-InstallerEnglish.ps1') -Version $Version `
    -SetupExe $setupExe -UninstallExe $uninstallExe

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $outputRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $outputRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'PRE_RELEASE_NOTES.md') -Destination $outputRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'RELEASE_NOTES.md') -Destination $outputRoot -Force

$sumLines = @()
foreach ($name in ($releaseFileNames | Sort-Object)) {
    if ($name -ne 'SHA256SUMS.txt') {
        $sumLines += ('{0}  {1}' -f (Get-Sha256 (Join-Path $outputRoot $name)), $name)
    }
}
$sumLines | Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding ascii

$releaseZip = Join-Path $artifactRoot ("BusinessTourFiveRealms-{0}-win-x64.zip" -f $Version)
if (Test-Path -LiteralPath $releaseZip) { Remove-Item -LiteralPath $releaseZip -Force }
$releasePaths = @($releaseFileNames | ForEach-Object { Join-Path $outputRoot $_ })
Compress-Archive -LiteralPath $releasePaths -DestinationPath $releaseZip -CompressionLevel Optimal
(Get-Sha256 $releaseZip) | Set-Content -LiteralPath ($releaseZip + '.sha256') -Encoding ascii

Write-Host "Release ready: $releaseZip"
Write-Host "Embedded payload SHA-256: $payloadSha"
