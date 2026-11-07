$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $compiler /nologo /target:exe /out:"$output\SessionTests.exe" /r:System.Core.dll "$PSScriptRoot\src\Session.cs" "$PSScriptRoot\src\Display.cs" "$PSScriptRoot\tests\SessionTests.cs"
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& "$output\SessionTests.exe"
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
