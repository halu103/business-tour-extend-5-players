[CmdletBinding()]
param(
    [string]$GameRoot,
    [string]$Version = '0.1.0'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$testRoot = Join-Path $projectRoot 'work\installer-e2e-game'
$setup = Join-Path $projectRoot 'dist\BusinessTourFiveRealms-Setup.exe'
$uninstall = Join-Path $projectRoot 'dist\BusinessTourFiveRealms-Uninstall.exe'
$logRoot = Join-Path $projectRoot 'work\test-logs'

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

if (-not (Test-Path -LiteralPath $setup) -or -not (Test-Path -LiteralPath $uninstall)) {
    throw 'Build the release before running the end-to-end test.'
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
}

$unrelated = Join-Path $testRoot 'BepInEx\plugins\UnrelatedMod\keep-me.txt'
New-Item -Path (Split-Path -Parent $unrelated) -ItemType Directory -Force | Out-Null
'unrelated mod sentinel' | Set-Content -LiteralPath $unrelated -Encoding ascii

Invoke-Checked $setup @('--verify-only', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'setup-verify.log'))
Invoke-Checked $setup @('--install', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'setup-install.log'))

$plugin = Join-Path $testRoot 'BepInEx\plugins\BusinessTourFiveRealms\BusinessTourFiveRealms.dll'
$marker = Join-Path $testRoot 'BepInEx\plugins\BusinessTourFiveRealms\install-marker.json'
if (-not (Test-Path -LiteralPath $plugin) -or -not (Test-Path -LiteralPath $marker)) {
    throw 'Installer did not create the plugin and install marker.'
}

# An upgrade over an exact managed installation must also be safe.
Invoke-Checked $setup @('--install', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'setup-upgrade.log'))
Invoke-Checked $uninstall @('--verify-only', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'uninstall-verify.log'))
Invoke-Checked $uninstall @('--uninstall', '--game-root', $testRoot, '--log', (Join-Path $logRoot 'uninstall.log'))

if (Test-Path -LiteralPath $plugin) { throw 'Uninstaller left the owned plugin DLL behind.' }
if (Test-Path -LiteralPath $marker) { throw 'Uninstaller left the install marker behind.' }
if (-not (Test-Path -LiteralPath $unrelated)) { throw 'Uninstaller removed an unrelated plugin file.' }
if (-not (Test-Path -LiteralPath (Join-Path $testRoot 'BepInEx\core\BepInEx.Core.dll'))) {
    throw 'Uninstaller removed the shared BepInEx loader.'
}

Write-Host "Installer end-to-end test passed. Logs: $logRoot"

