[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$Version = '0.2.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $projectRoot 'work\installer-e2e-game'
$setup = Join-Path $projectRoot 'dist\BusinessTourFiveRealms-Setup.exe'
$uninstall = Join-Path $projectRoot 'dist\BusinessTourFiveRealms-Uninstall.exe'
$logRoot = Join-Path $projectRoot 'work\test-logs'
$payloadRoot = Join-Path $projectRoot 'work\release\payload'
$payloadZip = Join-Path $projectRoot 'work\release\BusinessTourFiveRealms.Payload.zip'
$payloadManifestPath = Join-Path $payloadRoot 'payload-manifest.json'
$pluginRelative = 'BepInEx\plugins\BusinessTourFiveRealms\BusinessTourFiveRealms.dll'
$modManifestRelative = 'BepInEx\plugins\BusinessTourFiveRealms\mod-manifest.json'
$builtPlugin = Join-Path $projectRoot 'src\BusinessTourFiveRealms\bin\Release\net6.0\BusinessTourFiveRealms.dll'
$gameHashes = @{}

function Assert-WorkspaceChild([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    $prefix = $projectRoot.TrimEnd('\') + '\'
    if (-not $full.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing a test path outside the workspace: $full"
    }
    return $full
}

function Reset-TestDirectory([string]$Path) {
    $full = Assert-WorkspaceChild $Path
    if (Test-Path -LiteralPath $full) { Remove-Item -LiteralPath $full -Recurse -Force }
    New-Item -Path $full -ItemType Directory -Force | Out-Null
}

function Invoke-Checked([string]$File, [string[]]$Arguments) {
    $process = Start-Process -FilePath $File -ArgumentList $Arguments -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 0) {
        throw "Process failed with exit code $($process.ExitCode): $File $($Arguments -join ' ')"
    }
}

function Get-Sha256([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Required file is missing: $Path" }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
}

function Assert-Hash([string]$Path, [string]$Expected) {
    if ((Get-Sha256 $Path) -ne $Expected) { throw "Unexpected file content: $Path" }
}

function Resolve-TestFile([string]$Relative) {
    if ([System.IO.Path]::IsPathRooted($Relative)) { throw "Rooted manifest path: $Relative" }
    $full = [System.IO.Path]::GetFullPath((Join-Path $testRoot $Relative))
    if (-not $full.StartsWith($testRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Manifest path escapes the isolated test directory: $Relative"
    }
    return $full
}

function Assert-GamePreserved {
    foreach ($relative in $gameHashes.Keys) { Assert-Hash (Resolve-TestFile $relative) $gameHashes[$relative] }
    Assert-Hash $unrelated $unrelatedHash
}

function Assert-ManagedInstall([bool]$ExpectedLoaderInstalled, [string]$Stage) {
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { throw "$Stage did not create the install marker." }
    $installed = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
    if ($installed.ModId -ne 'vn.businesstour.fiverealms' -or $installed.ModVersion -ne $Version) {
        throw "$Stage installed an unexpected mod identity/version: $($installed.ModId) $($installed.ModVersion)"
    }
    if ($installed.PayloadSha256 -ne $expectedPayloadHash -or
        $installed.LoaderInstalledByThisRun -ne $ExpectedLoaderInstalled) {
        throw "$Stage marker does not identify this exact payload and loader operation."
    }
    $installedFiles = @($installed.Files)
    if ($installedFiles.Count -ne $expectedFiles.Count) { throw "$Stage marker has an unexpected file count." }
    $seen = @{}
    foreach ($file in $installedFiles) {
        if ($seen.ContainsKey($file.Path) -or -not $expectedFiles.ContainsKey($file.Path)) {
            throw "$Stage marker contains a duplicate or unrecognized path: $($file.Path)"
        }
        $seen[$file.Path] = $true
        $expected = $expectedFiles[$file.Path]
        if ($file.Sha256 -ne $expected.Sha256 -or [long]$file.Length -ne [long]$expected.Length -or
            $file.Scope -ne $expected.Scope) { throw "$Stage marker differs from the packaged file: $($file.Path)" }
        $destination = Resolve-TestFile $file.Path
        Assert-Hash $destination $expected.Sha256
        if ((Get-Item -LiteralPath $destination).Length -ne [long]$expected.Length) {
            throw "$Stage installed an unexpected file length: $($file.Path)"
        }
    }
    Assert-Hash $plugin $expectedPluginHash
    $installedVersion = [System.Reflection.AssemblyName]::GetAssemblyName($plugin).Version.ToString(3)
    if ($installedVersion -ne $Version) { throw "$Stage DLL assembly version is $installedVersion, expected $Version." }
    $modManifest = Get-Content -LiteralPath (Resolve-TestFile $modManifestRelative) -Raw | ConvertFrom-Json
    if ($modManifest.modId -ne $installed.ModId -or $modManifest.version -ne $Version -or
        $modManifest.playerCount -ne 5 -or $modManifest.mapId -ne 'BT5_FIVE_REALMS_V1') {
        throw "$Stage mod manifest does not describe this five-player release."
    }
    Assert-GamePreserved
}

if (-not (Test-Path -LiteralPath $setup) -or -not (Test-Path -LiteralPath $uninstall)) {
    throw 'Build the release before running the end-to-end test.'
}
if (-not (Test-Path -LiteralPath $payloadManifestPath) -or -not (Test-Path -LiteralPath $payloadZip)) {
    throw 'The matching release payload is missing; build the release before testing.'
}
$expectedManifest = Get-Content -LiteralPath $payloadManifestPath -Raw | ConvertFrom-Json
if ($expectedManifest.ModId -ne 'vn.businesstour.fiverealms' -or $expectedManifest.ModVersion -ne $Version) {
    throw "The packaged release is $($expectedManifest.ModVersion), expected $Version; stale packages must not pass this test."
}
$expectedFiles = @{}
foreach ($file in $expectedManifest.Files) {
    if ($expectedFiles.ContainsKey($file.Path)) { throw "Duplicate payload path: $($file.Path)" }
    $expectedFiles[$file.Path] = $file
}
if (-not $expectedFiles.ContainsKey($pluginRelative) -or $expectedFiles[$pluginRelative].Scope -ne 'mod') {
    throw 'The release payload does not contain the owned plugin DLL.'
}
$expectedPluginHash = $expectedFiles[$pluginRelative].Sha256
$expectedPayloadHash = Get-Sha256 $payloadZip
Assert-Hash (Join-Path $payloadRoot $pluginRelative) $expectedPluginHash
Assert-Hash $builtPlugin $expectedPluginHash

# The standalone executables must also be the exact published artifacts.
$sums = Get-Content -LiteralPath (Join-Path $projectRoot 'dist\SHA256SUMS.txt')
foreach ($file in @($setup, $uninstall)) {
    $name = [System.IO.Path]::GetFileName($file)
    $checksumRows = @($sums | ForEach-Object {
        $checksumMatch = [regex]::Match($_, '^(?<hash>[A-Fa-f0-9]{64})  (?<name>.+)$')
        if ($checksumMatch.Success -and $checksumMatch.Groups['name'].Value -eq $name) {
            $checksumMatch.Groups['hash'].Value
        }
    })
    if ($checksumRows.Count -ne 1) { throw "Release checksums do not identify exactly one $name." }
    Assert-Hash $file $checksumRows[0]
}
if ([string]::IsNullOrWhiteSpace($GameRoot)) {
    $candidate = 'D:\SteamLibrary\steamapps\common\Business Tour'
    if (Test-Path -LiteralPath (Join-Path $candidate 'BusinessTour.exe')) { $GameRoot = $candidate }
    else { throw 'Pass -GameRoot with a compatible Business Tour installation.' }
}

Reset-TestDirectory $testRoot
Reset-TestDirectory $logRoot
foreach ($relative in @(
    'BusinessTour.exe',
    'GameAssembly.dll',
    'UnityPlayer.dll',
    'BusinessTour_Data\app.info',
    'BusinessTour_Data\globalgamemanagers',
    'BusinessTour_Data\il2cpp_data\Metadata\global-metadata.dat')) {
    $source = Join-Path $GameRoot $relative
    $destination = Join-Path $testRoot $relative
    New-Item -Path (Split-Path -Parent $destination) -ItemType Directory -Force | Out-Null
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $gameHashes[$relative] = Get-Sha256 $source
}

$unrelated = Join-Path $testRoot 'BepInEx\plugins\UnrelatedMod\keep-me.txt'
New-Item -Path (Split-Path -Parent $unrelated) -ItemType Directory -Force | Out-Null
'unrelated mod sentinel' | Set-Content -LiteralPath $unrelated -Encoding ascii
$unrelatedHash = Get-Sha256 $unrelated

Invoke-Checked $setup @('--verify-only', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'setup-verify.log'))
Invoke-Checked $setup @('--install', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'setup-install.log'))

$plugin = Join-Path $testRoot 'BepInEx\plugins\BusinessTourFiveRealms\BusinessTourFiveRealms.dll'
$marker = Join-Path $testRoot 'BepInEx\plugins\BusinessTourFiveRealms\install-marker.json'
if (-not (Test-Path -LiteralPath $plugin) -or -not (Test-Path -LiteralPath $marker)) {
    throw 'Installer did not create the plugin and install marker.'
}
Assert-ManagedInstall $true 'Initial installation'

# An upgrade over an exact managed installation must also be safe.
Invoke-Checked $setup @('--install', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'setup-upgrade.log'))
Assert-ManagedInstall $false 'Reinstallation'
Invoke-Checked $uninstall @('--verify-only', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'uninstall-verify.log'))
Invoke-Checked $uninstall @('--uninstall', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'uninstall.log'))

if (Test-Path -LiteralPath $plugin) { throw 'Uninstaller left the owned plugin DLL behind.' }
if (Test-Path -LiteralPath $marker) { throw 'Uninstaller left the install marker behind.' }
if (-not (Test-Path -LiteralPath $unrelated)) { throw 'Uninstaller removed an unrelated plugin file.' }
if (-not (Test-Path -LiteralPath (Join-Path $testRoot 'BepInEx\core\BepInEx.Core.dll'))) {
    throw 'Uninstaller removed the shared BepInEx loader.'
}
foreach ($file in $expectedManifest.Files) {
    if ($file.Scope -eq 'loader') { Assert-Hash (Resolve-TestFile $file.Path) $file.Sha256 }
    elseif ($file.Scope -eq 'mod' -and (Test-Path -LiteralPath (Resolve-TestFile $file.Path))) {
        throw "Uninstaller left an owned mod payload file behind: $($file.Path)"
    }
}
Assert-GamePreserved

Write-Host "Installer $Version exact-payload end-to-end test passed. Logs: $logRoot"
