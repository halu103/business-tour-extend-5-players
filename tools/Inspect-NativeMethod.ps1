[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][uint32]$Token,
    [int]$MaximumBytes=2048,
    [string]$GameRoot='D:\SteamLibrary\steamapps\common\Business Tour'
)

# Read-only diagnostic of the exact generated interop token and native RVA.
# Nothing is loaded into the game process, and no native code is executed.
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $projectRoot '.tools\ilspy\Mono.Cecil.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $projectRoot '.tools\ilspy\Iced.dll'))
$module=[Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $GameRoot 'BepInEx\interop\Assembly-CSharp.dll'))
$database=[IO.BinaryReader]::new([IO.File]::OpenRead((Join-Path $GameRoot 'BepInEx\interop\MethodAddressToToken.db')))
$native=[IO.BinaryReader]::new([IO.File]::OpenRead((Join-Path $GameRoot 'GameAssembly.dll')))
try {
    $method=$module.LookupToken([int]$Token)
    if ($method -isnot [Mono.Cecil.MethodDefinition]) { throw 'Token must name an interop method.' }
    $null=$database.ReadInt32(); $null=$database.ReadInt32()
    $assemblyCount=$database.ReadInt32(); $methodCount=$database.ReadInt32(); $dataOffset=$database.ReadInt32()
    $names=for($index=0;$index -lt $assemblyCount;$index++){ $database.ReadString() }
    $database.BaseStream.Position=$dataOffset
    $addresses=[long[]]::new($methodCount)
    for($index=0;$index -lt $methodCount;$index++){ $addresses[$index]=$database.ReadInt64() }
    $calls=@{}; $rva=$null
    for($index=0;$index -lt $methodCount;$index++){
        $candidateToken=$database.ReadInt32(); $assemblyIndex=$database.ReadInt32()
        if (($names[$assemblyIndex] -split ',')[0] -replace '\.dll$','' -ne 'Assembly-CSharp'){ continue }
        $calls[$addresses[$index]]=$candidateToken
        if($candidateToken -eq [int]$Token){ $rva=$addresses[$index] }
    }
    if($null -eq $rva){ throw 'No native RVA for this generated interop token.' }
    $native.BaseStream.Position=0x3C
    $pe=$native.ReadInt32()
    $native.BaseStream.Position=$pe+6; $sectionCount=$native.ReadUInt16()
    $native.BaseStream.Position=$pe+20; $optionalSize=$native.ReadUInt16()
    $offset=$null
    for($index=0;$index -lt $sectionCount;$index++){
        $native.BaseStream.Position=$pe+24+$optionalSize+40*$index+8
        $virtualSize=$native.ReadUInt32(); $virtualAddress=$native.ReadUInt32()
        $rawSize=$native.ReadUInt32(); $rawPointer=$native.ReadUInt32()
        if($rva -ge $virtualAddress -and $rva -lt $virtualAddress+$rawSize){
            $offset=$rawPointer+$rva-$virtualAddress
            $MaximumBytes=[Math]::Min($MaximumBytes,[int]($rawSize-($rva-$virtualAddress)))
            break
        }
    }
    if($null -eq $offset -or $MaximumBytes -lt 1 -or $MaximumBytes -gt 16384){ throw 'Invalid bounded native read.' }
    $native.BaseStream.Position=$offset
    $decoder=[Iced.Intel.Decoder]::Create(64,$native.ReadBytes($MaximumBytes),[uint64]$rva,[Iced.Intel.DecoderOptions]::None)
    $formatter=[Iced.Intel.IntelFormatter]::new()
    '{0} at RVA 0x{1:X}' -f $method.FullName,$rva
    while($decoder.IP -lt $rva+$MaximumBytes){
        $instruction=$decoder.Decode()
        $output=[Iced.Intel.StringOutput]::new()
        $formatter.Format([ref]$instruction,$output)
        $suffix=''
        if($instruction.Mnemonic -in [Iced.Intel.Mnemonic]::Call,[Iced.Intel.Mnemonic]::Jmp){
            $target=[long]$instruction.NearBranchTarget
            if($calls.ContainsKey($target)){
                $callee=$module.LookupToken($calls[$target])
                $suffix=' ; '+$callee.FullName
            }
        }
        '0x{0:X}: {1}{2}' -f $instruction.IP,$output.ToString(),$suffix
    }
}
finally { $native.Dispose(); $database.Dispose(); $module.Dispose() }
