$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldSeals-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'CommandSpellTests.cs') `
        (Join-Path $PSScriptRoot 'CommandSealSurgeryTests.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\CommandSealSurgery.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\CommandSpellService.cs') `
        (Join-Path $projectRoot 'Source\Presentation\MasterCommandSpells.cs') `
        (Join-Path $projectRoot 'Source\Integration\Harmony_CommandSpellHealth.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Command spell test compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Command spell scenarios failed' }
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
