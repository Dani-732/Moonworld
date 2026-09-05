$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldEnemyRetreat-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'EnemyRetreatTests.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\LordJob_EnemyWarParty.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\EnemyTargetingPolicy.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\JobGiver_EnemyServantAssault.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\SpiritFollowJobPolicy.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Enemy retreat tests did not compile' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Enemy retreat scenarios failed' }
}
finally { if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput } }
