param([string]$RimWorldPath = 'G:\steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$managed = Join-Path $RimWorldPath 'RimWorldWin64_Data\Managed'
$harmonyDir = 'G:\steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldContracts-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput /reference:"$harmonyDir\0Harmony.dll" (Join-Path $PSScriptRoot 'RuntimeContractChecks.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Runtime contract check compilation failed' }
    & $testOutput (Join-Path $projectRoot '1.6\Assemblies') $managed $harmonyDir
    if ($LASTEXITCODE -ne 0) { throw 'Runtime contract checks failed' }

    $mwXmlFiles = Get-ChildItem (Join-Path $projectRoot '1.6') -Filter '*.xml' -Recurse
    foreach ($mwXmlFile in $mwXmlFiles) { [xml](Get-Content -LiteralPath $mwXmlFile.FullName -Raw) | Out-Null }
    [xml]$mwAbility = Get-Content (Join-Path $projectRoot '1.6\Defs\MW_NoblePhantasms.xml') -Raw
    $mwBurst = $mwAbility.SelectSingleNode('/Defs/AbilityDef[defName="MW_TestPranaBurst"]')
    if ($mwBurst.gizmoClass -or $mwBurst.displayGizmoWhileUndrafted -ne 'true' -or $mwBurst.disableGizmoWhileUndrafted -ne 'false') {
        throw 'Test ability must use the native command and remain available undrafted'
    }
    [xml]$mwBridge = Get-Content (Join-Path $projectRoot '1.6\Patches\MW_HolyGrailWarBridge.xml') -Raw
    $mwDefs = New-Object System.Xml.XmlDocument
    $mwDefs.LoadXml('<Defs />')
    foreach ($mwSourceFile in Get-ChildItem (Join-Path $RimWorldPath 'Mods\HolyGrailWarTest\1.6\Defs') -Filter '*.xml' -Recurse) {
        [xml]$mwSource = Get-Content -LiteralPath $mwSourceFile.FullName -Raw
        foreach ($mwNode in $mwSource.DocumentElement.ChildNodes) {
            if ($mwNode.NodeType -eq [System.Xml.XmlNodeType]::Element) {
                $mwDefs.DocumentElement.AppendChild($mwDefs.ImportNode($mwNode, $true)) | Out-Null
            }
        }
    }
    if ($mwDefs.SelectNodes($mwBridge.Patch.Operation.xpath).Count -ne 1) { throw 'Installed prototype bridge target missing or ambiguous' }
    if ($mwBridge.SelectNodes('//value/li[@Class="MoonWorld.CompProperties_ServantCommands"]').Count -ne 1) {
        throw 'Servant presentation component missing or duplicated'
    }
    Write-Host "$($mwXmlFiles.Count) XML files parsed; native ability configuration and installed prototype patch target checked."
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
