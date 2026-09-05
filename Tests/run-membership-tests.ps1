$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
$testOutput = Join-Path ([IO.Path]::GetTempPath()) ('MoonWorldMembership-' + [Guid]::NewGuid().ToString('N') + '.exe')
try {
    & $compiler /nologo /target:exe /out:$testOutput `
        (Join-Path $PSScriptRoot 'MembershipTests.cs') `
        (Join-Path $projectRoot 'Source\Lifecycle\ServantColonyMembership.cs') `
        (Join-Path $projectRoot 'Source\Integration\Harmony_ServantColonyMembership.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Membership test compilation failed' }
    & $testOutput
    if ($LASTEXITCODE -ne 0) { throw 'Membership scenarios failed' }
}
finally {
    if (Test-Path -LiteralPath $testOutput) { Remove-Item -LiteralPath $testOutput }
}
