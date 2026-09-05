$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldAbility-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'NoblePhantasmTests.cs') `
        (Join-Path $projectRoot 'Source\Abilities\Ability_NoblePhantasm.cs') `
        (Join-Path $projectRoot 'Source\Presentation\Command_NoblePhantasm.cs') `
        (Join-Path $projectRoot 'Source\Abilities\NoblePhantasmService.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Ability test compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Ability scenarios failed' }
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
