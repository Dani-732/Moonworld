$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldEnemyPrana-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'EnemyPranaTests.cs') `
        (Join-Path $projectRoot 'Source\Core\EnemyContractUtility.cs') `
        (Join-Path $projectRoot 'Source\Prana\PranaCycle.cs') `
        (Join-Path $projectRoot 'Source\Prana\ServantSustainPolicy.cs') `
        (Join-Path $projectRoot 'Source\Prana\PranaLedger.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Enemy prana tests did not compile' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Enemy prana scenarios failed' }
}
finally { if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput } }
