$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldSummoning-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'WarStartupHost.cs') `
        (Join-Path $PSScriptRoot 'HolyGrailEndingTestShim.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyWarPreparation.cs') `
        (Join-Path $projectRoot 'Source\Integration\Site_WarWorkshop.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarWorkshopService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WorkshopRebuildService.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\WorkshopRetreatPolicy.cs') `
        (Join-Path $PSScriptRoot 'WorkshopRetreatTests.cs') `
        (Join-Path $PSScriptRoot 'SummoningTests.cs') `
        (Join-Path $PSScriptRoot 'RecontractTests.cs') `
        (Join-Path $PSScriptRoot 'EnemyBattleTests.cs') `
        (Join-Path $PSScriptRoot 'EnemyChallengeTests.cs') `
        (Join-Path $projectRoot 'Source\Core\EnemyChallengeSession.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyChallengeService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarEncounterPolicy.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarEncounterSiteUtility.cs') `
        (Join-Path $projectRoot 'Source\Integration\Site_WarEncounter.cs') `
        (Join-Path $projectRoot 'Source\Integration\IncidentWorker_WarEncounter.cs') `
        (Join-Path $projectRoot 'Source\Core\EnemyBattleSession.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyBattleService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\ServantRecontractService.cs') `
        (Join-Path $projectRoot 'Source\Presentation\RecontractOffer.cs') `
        (Join-Path $projectRoot 'Source\Core\HolyGrailWarEntry.cs') `
        (Join-Path $projectRoot 'Source\Core\HolyGrailWarClass.cs') `
        (Join-Path $projectRoot 'Source\Core\HolyGrailWarClassDef.cs') `
        (Join-Path $projectRoot 'Source\Core\EnemyWarParticipant.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarRosterPreparation.cs') `
        (Join-Path $projectRoot 'Source\Core\ServantSummonPoolDef.cs') `
        (Join-Path $projectRoot 'Source\Core\WarState.cs') `
        (Join-Path $projectRoot 'Source\Core\WarReport.cs') `
        (Join-Path $projectRoot 'Source\Core\WarReconnaissance.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarRhythmPolicy.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarFinalBattleService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\WarOutcomeService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\HolyGrailWarEntryService.cs') `
        (Join-Path $projectRoot 'Source\Core\EnemyContractUtility.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyWarPartyService.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\EnemyRestUtility.cs') `
        (Join-Path $projectRoot 'Source\Integration\IncidentWorker_EnemyServantRaid.cs') `
        (Join-Path $projectRoot 'Source\Integration\IncidentWorker_WarFinalBattle.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\ServantSummoningService.cs') `
        (Join-Path $projectRoot 'Source\Quest\HolyGrailWarQuestPart.cs') `
        (Join-Path $projectRoot 'Source\Quest\HolyGrailWarQuestService.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Summoning test compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Summoning scenarios failed' }
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
