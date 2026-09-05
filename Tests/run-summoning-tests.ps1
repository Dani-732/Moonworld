$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldSummoning-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'SummoningTests.cs') `
        (Join-Path $projectRoot 'Source\Core\HolyGrailWarEntry.cs') `
        (Join-Path $projectRoot 'Source\Core\HolyGrailWarClass.cs') `
        (Join-Path $projectRoot 'Source\Core\WarState.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\HolyGrailWarEntryService.cs') `
        (Join-Path $projectRoot 'Source\Core\EnemyContractUtility.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyWarPartyService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyRestUtility.cs') `
        (Join-Path $projectRoot 'Source\Integration\IncidentWorker_EnemyServantRaid.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\ServantSummoningService.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Summoning test compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Summoning scenarios failed' }
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
