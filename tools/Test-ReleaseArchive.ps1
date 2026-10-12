[CmdletBinding()]
param([string]$Version = '0.2.2')

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$distRoot = Join-Path $projectRoot 'dist'
$archivePath = Join-Path $projectRoot ("artifacts\BusinessTourFiveRealms-{0}-win-x64.zip" -f $Version)
$sumPath = Join-Path $distRoot 'SHA256SUMS.txt'
$expected = @{}
foreach ($line in Get-Content -LiteralPath $sumPath) {
    if ($line -notmatch '^([0-9A-Fa-f]{64})  ([^\\/]+)$') { throw "Invalid checksum row: $line" }
    if ($expected.ContainsKey($Matches[2])) { throw 'Duplicate checksum filename.' }
    $expected[$Matches[2]] = $Matches[1].ToUpperInvariant()
}
$expected['SHA256SUMS.txt'] = (Get-FileHash -LiteralPath $sumPath -Algorithm SHA256).Hash
$required = @('BusinessTourFiveRealms-Setup.exe', 'BusinessTourFiveRealms-Uninstall.exe',
    'README.md', 'RELEASE_NOTES.md', 'PRE_RELEASE_NOTES.md', 'THIRD_PARTY_NOTICES.md', 'SHA256SUMS.txt')
if ($expected.Count -ne $required.Count) { throw 'Unexpected release file count.' }
foreach ($name in $required) {
    if (-not $expected.ContainsKey($name)) { throw "Required release file is missing: $name" }
    $current = (Get-FileHash -LiteralPath (Join-Path $distRoot $name) -Algorithm SHA256).Hash
    if ($current -ne $expected[$name]) { throw "Stale dist checksum: $name" }
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
try {
    if ($archive.Entries.Count -ne $expected.Count) { throw 'Unexpected ZIP entry count.' }
    $seen = @{}
    foreach ($entry in $archive.Entries) {
        $name = $entry.FullName
        if ($seen.ContainsKey($name) -or -not $expected.ContainsKey($name)) {
            throw "Duplicate, nested or unexpected ZIP entry: $name"
        }
        $seen[$name] = $true
        $stream = $entry.Open()
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($hash -ne $expected[$name]) { throw "ZIP checksum mismatch: $name" }
    }
}
finally { $archive.Dispose() }
foreach ($name in @('README.md', 'RELEASE_NOTES.md', 'PRE_RELEASE_NOTES.md', 'THIRD_PARTY_NOTICES.md')) {
    $sourceHash = (Get-FileHash -LiteralPath (Join-Path $projectRoot $name) -Algorithm SHA256).Hash
    $payloadPath = Join-Path $projectRoot ("work\release\payload\BepInEx\plugins\BusinessTourFiveRealms\{0}" -f $name)
    if ($sourceHash -ne $expected[$name] -or
        $sourceHash -ne (Get-FileHash -LiteralPath $payloadPath -Algorithm SHA256).Hash) {
        throw "Package/source documentation mismatch: $name"
    }
}
$archiveHash = (Get-FileHash -LiteralPath $archivePath -Algorithm SHA256).Hash
if ($archiveHash -ne (Get-Content -LiteralPath ($archivePath + '.sha256') -Raw).Trim()) {
    throw 'Release ZIP sidecar checksum mismatch.'
}
Write-Host "PASS: all $($expected.Count) release ZIP entries, dist checksums, source/payload documents and archive sidecar match."
Write-Host "Release ZIP SHA256: $archiveHash"
