$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldPresence-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'IndependentPresenceTests.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\ServantLifecycleService.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Independent presence tests did not compile' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Independent presence scenarios failed' }
}
finally { if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput } }
