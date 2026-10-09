<#
.SYNOPSIS
Read-only audit of IL2CPP native bodies shared by Harmony patch targets.
.DESCRIPTION
The default mode reads the active, non-diagnostic PatchTypes branch from
Plugin.cs, then resolves Harmony attributes from the already-built mod using
Cecil. Dynamic TargetMethod helpers have a small, explicit signature registry;
an unknown helper, unresolved method, or missing native address fails closed.

The token mode can inspect a proposed target before adding a patch. This tool
never starts the game, loads game assemblies into the CLR, or changes files.
MethodAddressToToken.db aliases are evidence of shared bodies, not proof that
every possible detour on those bodies is unsafe. In particular, a prefix that
casts a foreign `this` pointer to the target class needs careful review.
.EXAMPLE
./tools/Audit-NativePatchAliases.ps1 -FailOnUnsafeAliases
.EXAMPLE
./tools/Audit-NativePatchAliases.ps1 -Tokens 0x06009EE6,0x0600B829,0x06002A83
.EXAMPLE
./tools/Audit-NativePatchAliases.ps1 -AsJson
#>
[CmdletBinding(DefaultParameterSetName = 'Default')]
param(
    [string]$GameRoot = 'D:\SteamLibrary\steamapps\common\Business Tour',
    [Parameter(ParameterSetName = 'Default')]
    [string]$PluginSource = (Join-Path $PSScriptRoot '..\src\BusinessTourFiveRealms\Plugin.cs'),
    [Parameter(ParameterSetName = 'Default')]
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\src\BusinessTourFiveRealms\bin\Release\net6.0\BusinessTourFiveRealms.dll'),
    [Parameter(Mandatory = $true, ParameterSetName = 'Tokens')]
    [uint32[]]$Tokens,
    [Parameter(ParameterSetName = 'Tokens')]
    [string]$AssemblyName = 'Assembly-CSharp',
    [switch]$AsJson,
    [switch]$ShowAll,
    [switch]$FailOnUnsafeAliases
)

$ErrorActionPreference = 'Stop'
$interopRoot = Join-Path $GameRoot 'BepInEx\interop'
$coreRoot = Join-Path $GameRoot 'BepInEx\core'
[void][Reflection.Assembly]::LoadFrom((Join-Path $coreRoot 'Mono.Cecil.dll'))

$resolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
$resolver.AddSearchDirectory($coreRoot)
$resolver.AddSearchDirectory($interopRoot)
$readerParameters = [Mono.Cecil.ReaderParameters]::new()
$readerParameters.AssemblyResolver = $resolver
$modules = @{}
$modModule = $null

function Get-InteropModule([string]$SimpleName) {
    $name = ($SimpleName -split ',')[0].Trim()
    if ($name.EndsWith('.dll', [StringComparison]::OrdinalIgnoreCase)) {
        $name = $name.Substring(0, $name.Length - 4)
    }
    if (-not $modules.ContainsKey($name)) {
        $modules[$name] = [Mono.Cecil.ModuleDefinition]::ReadModule(
            (Join-Path $interopRoot ($name + '.dll')), $readerParameters)
    }
    return $modules[$name]
}

function New-TargetDescriptor([string]$Type, [string]$Method, [string[]]$Parameters,
    [string]$Assembly = 'Assembly-CSharp') {
    return [pscustomobject]@{ Type = $Type; Method = $Method; Parameters = $Parameters; Assembly = $Assembly }
}

# These helpers select targets through AccessTools rather than attributes.
# Their parameter types are full Cecil names, including nested '/' names.
# Validate their referenced target types/method strings below so changed helpers
# do not silently retain this reviewed registry.
$dynamicTargets = @{
    SpecialMapSelectionStatusPatch = @(
        (New-TargetDescriptor 'BusinessTour.UIMapSelectionController' 'GetStatus' @('System.String')))
    MapSelectionInitializationAuditPatch = @(
        (New-TargetDescriptor 'BusinessTour.UIMapSelectionController' 'Initialize' @(
            'BusinessTour.IUIBinder`1<BusinessTour.EventSource>',
            'Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray`1<Il2CppSystem.Object>')))
    SelectedMapTrySelectPatch = @(
        (New-TargetDescriptor 'BusinessTour.SelectedMapProvider' 'TrySelect' @('System.Int32', 'System.String')))
    StartupCapacityPatch = @(
        (New-TargetDescriptor 'BusinessTour.StartupManager' 'Initialize' @('System.Int32')))
    RoomManagementCapacityPatch = @(
        (New-TargetDescriptor 'BusinessTour.RoomManagement' 'CreateRoom' $null))
    PentagonalBoardPatch = @(
        (New-TargetDescriptor 'BusinessTour.MapView' 'BusinessTour_IMapView_Initialize' $null))
    PentagonalSpriteSyncPatch = @(
        (New-TargetDescriptor 'BusinessTour.CellRenderer' 'set_Sprite' $null),
        (New-TargetDescriptor 'BusinessTour.CellRenderer' 'BusinessTour_IBlackAndWhiteView_SetBlackAndWhite' $null),
        (New-TargetDescriptor 'BusinessTour.CellRenderer' 'BusinessTour_IOrderableView_SetOrder' $null))
}

try {
    $targets = [System.Collections.Generic.List[object]]::new()
    if ($PSCmdlet.ParameterSetName -eq 'Tokens') {
        $module = Get-InteropModule $AssemblyName
        foreach ($token in $Tokens) {
            $member = $module.LookupToken([int]$token)
            if ($member -isnot [Mono.Cecil.MethodDefinition]) {
                throw ('Token 0x{0:X8} in {1} is not a method.' -f $token, $AssemblyName)
            }
            $targets.Add([pscustomobject]@{
                Patch = 'Explicit token'; Assembly = $module.Assembly.Name.Name
                Token = [int]$token; Method = $member.FullName
            })
        }
    }
    else {
        $source = Get-Content -LiteralPath $PluginSource -Raw
        $arrayMatch = [regex]::Match($source, '(?ms)\bPatchTypes\s*=\s*\{(?<body>.*?)^\s*\};')
        if (-not $arrayMatch.Success) { throw 'Could not find PatchTypes in Plugin.cs.' }
        $defaultMatch = [regex]::Match($arrayMatch.Groups['body'].Value,
            '(?ms)^\s*#else\s*$(?<body>.*?)^\s*#endif\s*$')
        if (-not $defaultMatch.Success) { throw 'Could not find the default PatchTypes branch.' }
        $patchNames = @([regex]::Matches($defaultMatch.Groups['body'].Value,
            'typeof\s*\(\s*(?<name>\w+)\s*\)') | ForEach-Object { $_.Groups['name'].Value })
        if ($patchNames.Count -eq 0) { throw 'The default patch list is empty.' }
        $modModule = [Mono.Cecil.ModuleDefinition]::ReadModule($ModAssembly, $readerParameters)
        $gameModule = Get-InteropModule 'Assembly-CSharp'
        foreach ($patchName in $patchNames) {
            $patchType = @($modModule.Types | Where-Object Name -eq $patchName)
            if ($patchType.Count -ne 1) {
                throw "Patch $patchName is absent or ambiguous in the built mod. Build it again first."
            }
            $descriptors = [System.Collections.Generic.List[object]]::new()
            foreach ($attribute in ($patchType[0].CustomAttributes | Where-Object {
                $_.AttributeType.FullName -eq 'HarmonyLib.HarmonyPatch'
            })) {
                $declaringType = $null; $declaringAssembly = 'Assembly-CSharp'
                $methodName = $null; $parameterTypes = $null; $methodType = 0
                foreach ($argument in $attribute.ConstructorArguments) {
                    switch ($argument.Type.FullName) {
                        'System.Type' {
                            $declaringType = $argument.Value.FullName
                            $declaringAssembly = $argument.Value.Scope.Name
                        }
                        'System.String' { $methodName = [string]$argument.Value }
                        'System.Type[]' { $parameterTypes = @($argument.Value | ForEach-Object { $_.Value.FullName }) }
                        'HarmonyLib.MethodType' { $methodType = [int]$argument.Value }
                        default { throw "Unsupported Harmony target argument on $patchName`: $($argument.Type.FullName)." }
                    }
                }
                if ($declaringType) {
                    switch ($methodType) {
                        0 { }
                        1 { $methodName = 'get_' + $methodName }
                        2 { $methodName = 'set_' + $methodName }
                        3 { $methodName = '.ctor' }
                        default { throw "Unsupported Harmony method kind $methodType on $patchName." }
                    }
                    $descriptors.Add((New-TargetDescriptor $declaringType $methodName $parameterTypes $declaringAssembly))
                }
            }
            if ($descriptors.Count -eq 0) {
                if (-not $dynamicTargets.ContainsKey($patchName)) {
                    throw "No reviewed dynamic target registry entry exists for $patchName."
                }
                $helperInstructions = @($patchType[0].Methods | Where-Object Name -in 'TargetMethod', 'TargetMethods' |
                    ForEach-Object { $_.Body.Instructions })
                # Iterator TargetMethods bodies live in the generated MoveNext type.
                $helperInstructions += @($patchType[0].NestedTypes | ForEach-Object { $_.Methods } |
                    Where-Object Name -eq 'MoveNext' | ForEach-Object { $_.Body.Instructions })
                foreach ($descriptor in $dynamicTargets[$patchName]) {
                    $hasType = @($helperInstructions | Where-Object {
                        $_.OpCode.Name -eq 'ldtoken' -and $_.Operand.FullName -eq $descriptor.Type
                    }).Count -gt 0
                    $hasName = @($helperInstructions | Where-Object {
                        $_.OpCode.Name -eq 'ldstr' -and $_.Operand -eq $descriptor.Method
                    }).Count -gt 0
                    if (-not $hasType -or -not $hasName) {
                        throw "The dynamic helper for $patchName changed. Review its target registry entry."
                    }
                    $descriptors.Add($descriptor)
                }
            }
            foreach ($descriptor in $descriptors) {
                $targetModule = Get-InteropModule $descriptor.Assembly
                $type = @($targetModule.Types | Where-Object FullName -eq $descriptor.Type)
                if ($type.Count -ne 1) { throw "Target type $($descriptor.Type) could not be resolved." }
                $resolvedType = $type[0]
                $methods = @($resolvedType.Methods | Where-Object Name -eq $descriptor.Method)
                while ($methods.Count -eq 0 -and $null -ne $resolvedType.BaseType) {
                    $resolvedType = $resolvedType.BaseType.Resolve()
                    if ($null -eq $resolvedType) { break }
                    $methods = @($resolvedType.Methods | Where-Object Name -eq $descriptor.Method)
                }
                if ($null -ne $descriptor.Parameters) {
                    $expected = $descriptor.Parameters -join '|'
                    $methods = @($methods | Where-Object {
                        (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join '|') -eq $expected
                    })
                }
                if ($methods.Count -ne 1) {
                    throw "Target $($descriptor.Type).$($descriptor.Method) on $patchName resolves to $($methods.Count) methods."
                }
                $targets.Add([pscustomobject]@{
                    Patch = $patchName; Assembly = $methods[0].Module.Assembly.Name.Name
                    Token = [int]$methods[0].MetadataToken.ToInt32(); Method = $methods[0].FullName
                })
            }
        }
    }

    $stream = [IO.File]::OpenRead((Join-Path $interopRoot 'MethodAddressToToken.db'))
    $binary = [IO.BinaryReader]::new($stream)
    try {
        $null = $binary.ReadInt32(); $null = $binary.ReadInt32()
        $assemblyCount = $binary.ReadInt32(); $methodCount = $binary.ReadInt32(); $dataOffset = $binary.ReadInt32()
        if ($assemblyCount -lt 1 -or $methodCount -lt 1 -or $dataOffset -lt 20 -or $dataOffset -ge $stream.Length) {
            throw 'Invalid MethodAddressToToken.db header.'
        }
        $assemblyNames = for ($index = 0; $index -lt $assemblyCount; $index++) { $binary.ReadString() }
        $stream.Position = $dataOffset
        $addresses = [long[]]::new($methodCount)
        $methodTokens = [int[]]::new($methodCount)
        $assemblyIndexes = [int[]]::new($methodCount)
        for ($index = 0; $index -lt $methodCount; $index++) { $addresses[$index] = $binary.ReadInt64() }
        $tokenAddresses = @{}
        for ($index = 0; $index -lt $methodCount; $index++) {
            $methodTokens[$index] = $binary.ReadInt32(); $assemblyIndexes[$index] = $binary.ReadInt32()
            if ($assemblyIndexes[$index] -lt 0 -or $assemblyIndexes[$index] -ge $assemblyCount) {
                throw "Invalid assembly index at database method $index."
            }
            $simpleName = ($assemblyNames[$assemblyIndexes[$index]] -split ',')[0].Trim() -replace '\.dll$', ''
            $tokenAddresses[$simpleName + ':' + $methodTokens[$index]] = $addresses[$index]
        }
    }
    finally { $binary.Dispose(); $stream.Dispose() }

    $groups = @{}
    foreach ($target in $targets) {
        $key = $target.Assembly + ':' + $target.Token
        if (-not $tokenAddresses.ContainsKey($key)) {
            throw ('No native address exists for {0}, token 0x{1:X8}.' -f $target.Method, $target.Token)
        }
        $address = $tokenAddresses[$key]
        if (-not $groups.ContainsKey($address)) {
            $groups[$address] = [pscustomobject]@{
                Rva = ('0x{0:X}' -f $address); Targets = [System.Collections.Generic.List[object]]::new()
                Aliases = [System.Collections.Generic.List[object]]::new(); Classification = 'Unique body'
            }
        }
        $groups[$address].Targets.Add([pscustomobject]@{
            Patch = $target.Patch; Token = ('0x{0:X8}' -f $target.Token); Method = $target.Method
        })
    }
    for ($index = 0; $index -lt $methodCount; $index++) {
        if (-not $groups.ContainsKey($addresses[$index])) { continue }
        $module = Get-InteropModule $assemblyNames[$assemblyIndexes[$index]]
        $member = $module.LookupToken($methodTokens[$index])
        if ($member -isnot [Mono.Cecil.MethodDefinition]) { throw 'A native database alias is not a method.' }
        $groups[$addresses[$index]].Aliases.Add([pscustomobject]@{
            Assembly = $module.Assembly.Name.Name; Token = ('0x{0:X8}' -f $methodTokens[$index]); Method = $member.FullName
        })
    }

    $unsafeCount = 0; $compatibleCount = 0
    foreach ($group in $groups.Values) {
        if ($group.Aliases.Count -le 1) { continue }
        $group.Classification = 'Shared body: review required'
        # Exact reviewed exception only: both methods have the same class and
        # BaseMapDefinition argument; the installed prefix only consumes that
        # argument. Do not globally allow arbitrary same-class constructor aliases.
        $knownMethods = @(
            'System.Void BusinessTour.MapSettings::.ctor(BusinessTour.BaseMapDefinition)',
            'System.Void BusinessTour.MapSettings::UpdateMapSettings(BusinessTour.BaseMapDefinition)')
        $actualMethods = @($group.Aliases | ForEach-Object Method)
        if ($actualMethods.Count -eq 2 -and @($actualMethods | Where-Object { $_ -notin $knownMethods }).Count -eq 0) {
            $group.Classification = 'Reviewed compatible constructor alias'
            $compatibleCount++
        }
        else { $unsafeCount++ }
    }
    $report = [pscustomobject]@{
        Mode = $PSCmdlet.ParameterSetName; TargetCount = $targets.Count; NativeBodyCount = $groups.Count
        ReviewRequiredBodyCount = $unsafeCount; CompatibleAliasBodyCount = $compatibleCount
        Bodies = @($groups.Values | Sort-Object Rva)
    }
    if ($AsJson) { $report | ConvertTo-Json -Depth 8 }
    else {
        foreach ($group in $report.Bodies) {
            if (-not $ShowAll -and $group.Aliases.Count -le 1) { continue }
            '{0}: {1}' -f $group.Rva, $group.Classification
            foreach ($target in $group.Targets) { '  PATCH {0}: {1} ({2})' -f $target.Patch, $target.Method, $target.Token }
            foreach ($alias in $group.Aliases) { '  ALIAS {0}: {1} ({2})' -f $alias.Assembly, $alias.Method, $alias.Token }
        }
        'Audited {0} targets / {1} native bodies; {2} shared bodies require review; {3} reviewed compatible aliases.' -f `
            $targets.Count, $groups.Count, $unsafeCount, $compatibleCount
    }
    if ($FailOnUnsafeAliases -and $unsafeCount -gt 0) {
        throw "$unsafeCount patched native bodies have unreviewed aliases. Do not release until reviewed."
    }
}
finally {
    if ($null -ne $modModule) { $modModule.Dispose() }
    foreach ($module in $modules.Values) { $module.Dispose() }
    $resolver.Dispose()
}
