[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$Version = '0.1.0'
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
$pluginProject = Join-Path $projectRoot 'src\BusinessTourFiveRealms\BusinessTourFiveRealms.csproj'
$pluginDll = Join-Path $projectRoot 'src\BusinessTourFiveRealms\bin\Release\net6.0\BusinessTourFiveRealms.dll'
$bepInExZip = Join-Path $projectRoot ('.tools\bepinex-788\BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.{0}+5b766a3.zip' -f $compat.bepInExBuild)
$dotnet = Join-Path $projectRoot '.tools\dotnet6\dotnet.exe'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

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

Reset-WorkspaceDirectory $workRoot
Reset-WorkspaceDirectory $outputRoot
New-Item -Path $artifactRoot -ItemType Directory -Force | Out-Null
New-Item -Path $payloadRoot -ItemType Directory -Force | Out-Null
New-Item -Path $generatedRoot -ItemType Directory -Force | Out-Null

$referenceRoot = Join-Path $GameRoot 'BepInEx'
if (-not (Test-Path -LiteralPath (Join-Path $referenceRoot 'interop\Assembly-CSharp.dll') -PathType Leaf)) {
    throw 'Open the compatible game with BepInEx once before building; current game interop references are required.'
}
& $dotnet build $pluginProject -c Release --nologo ("/p:BepInExRoot={0}" -f $referenceRoot)
if ($LASTEXITCODE -ne 0) { throw 'Plugin build failed.' }

# Audit the newly built patch targets against this exact game's native bodies.
# Unknown targets, unresolved addresses, and unreviewed aliases must stop packaging.
& (Join-Path $PSScriptRoot 'Audit-NativePatchAliases.ps1') `
    -GameRoot $GameRoot `
    -PluginSource (Join-Path $projectRoot 'src\BusinessTourFiveRealms\Plugin.cs') `
    -ModAssembly $pluginDll `
    -FailOnUnsafeAliases

$loaderRoot = Join-Path $workRoot 'loader'
Expand-Archive -LiteralPath $bepInExZip -DestinationPath $loaderRoot -Force
Copy-Tree $loaderRoot $payloadRoot

$modRoot = Join-Path $payloadRoot 'BepInEx\plugins\BusinessTourFiveRealms'
New-Item -Path $modRoot -ItemType Directory -Force | Out-Null
Copy-Item -LiteralPath $pluginDll -Destination (Join-Path $modRoot 'BusinessTourFiveRealms.dll') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination (Join-Path $modRoot 'README.md') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination (Join-Path $modRoot 'THIRD_PARTY_NOTICES.md') -Force

$modManifest = [ordered]@{
    modId = 'vn.businesstour.fiverealms'
    name = 'Business Tour Five Realms'
    version = $Version
    steamAppId = [string]$compat.steamAppId
    steamBuildId = [string]$compat.steamBuildId
    unityVersion = [string]$compat.unityVersion
    playerCount = 5
    mapId = 'BT5_FIVE_REALMS_V1'
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

Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $outputRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'THIRD_PARTY_NOTICES.md') -Destination $outputRoot -Force

$sumLines = @()
foreach ($file in Get-ChildItem -LiteralPath $outputRoot -File | Sort-Object Name) {
    if ($file.Name -ne 'SHA256SUMS.txt') {
        $sumLines += ('{0}  {1}' -f (Get-Sha256 $file.FullName), $file.Name)
    }
}
$sumLines | Set-Content -LiteralPath (Join-Path $outputRoot 'SHA256SUMS.txt') -Encoding ascii

$releaseZip = Join-Path $artifactRoot ("BusinessTourFiveRealms-{0}-win-x64.zip" -f $Version)
if (Test-Path -LiteralPath $releaseZip) { Remove-Item -LiteralPath $releaseZip -Force }
Compress-Archive -Path (Join-Path $outputRoot '*') -DestinationPath $releaseZip -CompressionLevel Optimal
(Get-Sha256 $releaseZip) | Set-Content -LiteralPath ($releaseZip + '.sha256') -Encoding ascii

Write-Host "Release ready: $releaseZip"
Write-Host "Embedded payload SHA-256: $payloadSha"

