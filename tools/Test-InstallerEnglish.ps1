[CmdletBinding(DefaultParameterSetName = 'Compiled')]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true, ParameterSetName = 'Compiled')][string]$SetupExe,
    [Parameter(Mandatory = $true, ParameterSetName = 'Compiled')][string]$UninstallExe,
    [Parameter(Mandatory = $true, ParameterSetName = 'Source')][switch]$SourceOnly
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ($Version -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$') {
    throw 'Use a three-part version with an optional pre-release suffix.'
}

function Require-SourceText([string]$Source, [string]$Expected, [string]$File) {
    if (-not $Source.Contains($Expected)) {
        throw "Missing English UI contract in ${File}: $Expected"
    }
}

$sources = @{}
foreach ($name in @('Installer.cs', 'Uninstaller.cs', 'Shared.cs', 'UnityLibraryCache.cs')) {
    $path = Join-Path $projectRoot (Join-Path 'installer' $name)
    $source = [System.IO.File]::ReadAllText($path)
    $nonAscii = [regex]::Match($source, '[^\x00-\x7F]')
    if ($nonAscii.Success) {
        $line = 1 + [regex]::Matches($source.Substring(0, $nonAscii.Index), '\n').Count
        throw "Non-ASCII text remains in installer/${name}:$line."
    }
    $sources[$name] = $source
}

foreach ($name in @('Installer.cs', 'Uninstaller.cs')) {
    $source = $sources[$name]
    foreach ($required in @(
        'Text = "Game folder:"',
        'browseButton.Text = "Browse...";',
        'verifyButton.Text = "Check files";',
        'closeButton.Text = "Close";',
        'dialog.Description = "Choose the folder containing BusinessTour.exe.";',
        '"Choose the Business Tour game folder first."',
        '"Checks passed. No game files were changed."',
        '"Please wait for the current task to finish."',
        '"Five Realms Setup or Uninstall is already open. Close that window or wait for it to finish, then try again."',
        'Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("en");')) {
        Require-SourceText $source $required $name
    }
    if ([regex]::IsMatch($source, '(?:Thread\.CurrentThread\.)?CurrentCulture\s*=')) {
        throw "${name} must not change number/date formatting culture to translate the UI."
    }
}
Require-SourceText $sources['Installer.cs'] 'Text = "Business Tour Five Realms - Setup";' 'Installer.cs'
Require-SourceText $sources['Installer.cs'] 'installButton.Text = "Install";' 'Installer.cs'
Require-SourceText $sources['Uninstaller.cs'] 'Text = "Business Tour Five Realms - Uninstall";' 'Uninstaller.cs'
Require-SourceText $sources['Uninstaller.cs'] 'removeButton.Text = "Uninstall";' 'Uninstaller.cs'
Require-SourceText $sources['Uninstaller.cs'] '"Remove Five Realms and its settings? BepInEx and other mods will stay installed."' 'Uninstaller.cs'
Write-Host 'PASS: installer source is ASCII English, with simple controls, confirmation and UI-only culture selection.'
if ($SourceOnly) { return }

$setupFull = [System.IO.Path]::GetFullPath($SetupExe)
$uninstallFull = [System.IO.Path]::GetFullPath($UninstallExe)
foreach ($path in @($setupFull, $uninstallFull)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Installer executable is missing: $path"
    }
}
$frameworkCompiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $frameworkCompiler -PathType Leaf)) {
    throw "The .NET Framework C# compiler is missing: $frameworkCompiler"
}

# Only the fixture is compiled. Its output stays in a unique workspace folder;
# it never calls install/uninstall/verify, shows a form, or runs a message loop.
$testWorkRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'work\installer-english-checks'))
$fixtureRoot = [System.IO.Path]::GetFullPath((Join-Path $testWorkRoot ([guid]::NewGuid().ToString('N'))))
if (-not $fixtureRoot.StartsWith($testWorkRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'The fixture output must stay inside the installer English test folder.'
}
New-Item -Path $fixtureRoot -ItemType Directory -Force | Out-Null
try {
    $fixtureExe = Join-Path $fixtureRoot 'InstallerEnglishUiSmoke.exe'
    $fixtureSource = Join-Path $projectRoot 'tests\InstallerEnglishUiSmoke.cs'
    $compileArguments = @('/nologo', '/target:exe', '/platform:x64', '/langversion:5',
        '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll',
        "/out:$fixtureExe", $fixtureSource)
    & $frameworkCompiler @compileArguments
    if ($LASTEXITCODE -ne 0) { throw 'The hidden installer UI fixture did not compile.' }
    $stdoutPath = Join-Path $fixtureRoot 'fixture.stdout'
    $stderrPath = Join-Path $fixtureRoot 'fixture.stderr'
    $process = Start-Process -FilePath $fixtureExe -WindowStyle Hidden -Wait -PassThru `
        -ArgumentList @(('"{0}"' -f $setupFull), ('"{0}"' -f $uninstallFull), $Version) `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath
    $outputText = [System.IO.File]::ReadAllText($stdoutPath).Trim()
    if ($outputText.Length -gt 0) { Write-Host $outputText }
    if ($process.ExitCode -ne 0) {
        $errorText = [System.IO.File]::ReadAllText($stderrPath).Trim()
        throw "The hidden installer English/layout check failed: $errorText"
    }
}
finally {
    # This is the exact unique directory created above, not either supplied exe.
    if ($fixtureRoot.StartsWith($testWorkRoot.TrimEnd('\') + '\', [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $fixtureRoot -PathType Container)) {
        Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
    }
}
