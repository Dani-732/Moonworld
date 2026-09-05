$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldSpiritMelee-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'SpiritMeleeTests.cs') `
        (Join-Path $projectRoot 'Source\Integration\Harmony_ServantSpiritMelee.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Spirit melee tests did not compile' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Spirit melee tests failed' }
}
finally { if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput } }
