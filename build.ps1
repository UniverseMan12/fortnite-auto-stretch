$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler not found.' }
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $output | Out-Null
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /out:"$output\FortniteAutoStretch.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll "$PSScriptRoot\src\Display.cs" "$PSScriptRoot\src\Session.cs" "$PSScriptRoot\src\Program.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built $output\FortniteAutoStretch.exe"
