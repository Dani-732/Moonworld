$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldUnbound-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'UnboundServantTests.cs') `
        (Join-Path $PSScriptRoot 'HolyGrailEndingTestShim.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\CompServantState.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\UnboundServantService.cs') `
        (Join-Path $projectRoot 'Source\Integration\Harmony_MasterDeath.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Unbound servant tests did not compile' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Unbound servant scenarios failed' }
}
finally { if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput } }
