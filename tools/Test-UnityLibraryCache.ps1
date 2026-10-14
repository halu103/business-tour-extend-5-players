[CmdletBinding()]
param(
    [string]$Version = '0.2.3',
    [string]$SetupExe,
    [string]$OfficialArchive
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'Use a three-part version with an optional pre-release suffix.'
}
if ([string]::IsNullOrWhiteSpace($SetupExe)) {
    $SetupExe = Join-Path $projectRoot 'dist\BusinessTourFiveRealms-Setup.exe'
}
$compatibility = Get-Content -LiteralPath (Join-Path $projectRoot 'config\compatibility.json') -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($OfficialArchive)) {
    $OfficialArchive = Join-Path $projectRoot ('.sandbox\Business Tour\BepInEx\unity-libs\{0}.zip' -f $compatibility.unityLibrariesVersion)
}
$setupFull = [System.IO.Path]::GetFullPath($SetupExe)
$archiveFull = [System.IO.Path]::GetFullPath($OfficialArchive)
foreach ($path in @($setupFull, $archiveFull)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The fixture input is missing: $path"
    }
}
$expectedHash = [string]$compatibility.unityLibrariesSha256
if ($expectedHash -notmatch '^[0-9A-Fa-f]{64}$' -or
    (Get-FileHash -LiteralPath $archiveFull -Algorithm SHA256).Hash -ne $expectedHash) {
    throw 'The fixture needs the known-good official archive recorded in compatibility.json.'
}
$frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $frameworkCompiler -PathType Leaf)) {
    throw "The .NET Framework C# compiler is missing: $frameworkCompiler"
}

# No download or game launch occurs. Only the injected helper downloader writes
# deterministic payloads beneath this unique, validated fixture directory.
$testWorkRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'work\unity-cache-checks'))
$fixtureRoot = [System.IO.Path]::GetFullPath((Join-Path $testWorkRoot ([guid]::NewGuid().ToString('N'))))
if (-not $fixtureRoot.StartsWith($testWorkRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The fixture output must stay inside work/unity-cache-checks.'
}
New-Item -Path $fixtureRoot -ItemType Directory -Force | Out-Null
try {
    $fixtureExe = Join-Path $fixtureRoot 'UnityLibraryCacheSmoke.exe'
    $fixtureSource = Join-Path $projectRoot 'tests\UnityLibraryCacheSmoke.cs'
    $compileArguments = @('/nologo', '/target:exe', '/platform:x64', '/langversion:5',
        "/out:$fixtureExe", $fixtureSource)
    & $frameworkCompiler @compileArguments
    if ($LASTEXITCODE -ne 0) { throw 'The isolated Unity library cache fixture did not compile.' }
    $stdoutPath = Join-Path $fixtureRoot 'fixture.stdout'
    $stderrPath = Join-Path $fixtureRoot 'fixture.stderr'
    $process = Start-Process -FilePath $fixtureExe -WindowStyle Hidden -Wait -PassThru `
        -ArgumentList @(('"{0}"' -f $setupFull), ('"{0}"' -f $fixtureRoot),
            ('"{0}"' -f $archiveFull), $Version, $expectedHash) `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $outputText = [System.IO.File]::ReadAllText($stdoutPath).Trim()
    if ($outputText.Length -gt 0) { Write-Host $outputText }
    if ($process.ExitCode -ne 0) {
        $errorText = [System.IO.File]::ReadAllText($stderrPath).Trim()
        throw "The isolated Unity library cache fixture failed: $errorText"
    }
}
finally {
    # Resolve and check the exact fixture root again before recursive cleanup.
    $cleanupTarget = [System.IO.Path]::GetFullPath($fixtureRoot)
    if ($cleanupTarget.StartsWith($testWorkRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $cleanupTarget -PathType Container)) {
        Remove-Item -LiteralPath $cleanupTarget -Recurse -Force
    }
}
