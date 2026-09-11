$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $env:TEMP 'MoonWorldHolyGrailEndingTests.exe'
$source = Join-Path $root 'Source\Ending\HolyGrailEndingPolicy.cs'
$test = Join-Path $PSScriptRoot 'HolyGrailEndingTests.cs'
$compiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'

& $compiler /nologo /target:exe /out:$out $source $test
if ($LASTEXITCODE -ne 0) { throw 'Holy Grail ending policy tests did not compile' }
& $out
if ($LASTEXITCODE -ne 0) { throw "Holy Grail ending policy tests failed with exit code $LASTEXITCODE" }
