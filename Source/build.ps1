param(
    [string]$RimWorldPath = 'G:\steam\steamapps\common\RimWorld',
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $RimWorldPath 'RimWorldWin64_Data\Managed'
$output = Join-Path $projectRoot '1.6\Assemblies\MoonWorld.dll'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$harmony = 'G:\steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll'
$assemblyCSharp = Join-Path $managed 'Assembly-CSharp.dll'
$unityCore = Join-Path $managed 'UnityEngine.CoreModule.dll'
$unityImgui = Join-Path $managed 'UnityEngine.IMGUIModule.dll'
$unityText = Join-Path $managed 'UnityEngine.TextRenderingModule.dll'
$netstandard = Join-Path $managed 'netstandard.dll'
$sources = Get-ChildItem -Path $PSScriptRoot -Recurse -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName }

& $compiler /nologo /target:library /optimize+ /out:$output `
    /reference:$assemblyCSharp `
    /reference:$unityCore `
    /reference:$unityImgui `
    /reference:$unityText `
    /reference:$netstandard `
    /reference:$harmony `
    $sources

if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE"
}

Write-Host "Built $output"

if ($Deploy) {
    $deployPath = Join-Path $RimWorldPath 'Mods\MoonWorld'
    robocopy $projectRoot $deployPath /E /XD '.git' /XF '*.pdb' | Out-Null
    if ($LASTEXITCODE -gt 7) {
        throw "MoonWorld deployment failed with robocopy exit code $LASTEXITCODE"
    }
    Write-Host "Deployed $projectRoot to $deployPath"
}
