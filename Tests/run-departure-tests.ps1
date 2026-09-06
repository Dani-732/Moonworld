$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldDeparture-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'DeparturePolicyTests.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\ServantDepartureService.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\ServantTravelAutonomy.cs') `
        (Join-Path $projectRoot 'Source\Autonomy\SpiritFollowJobPolicy.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Departure test compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Departure scenarios failed' }
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
