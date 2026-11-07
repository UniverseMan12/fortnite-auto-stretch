param([switch]$NoLaunch)
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'FortniteAutoStretch.exe'
if (!(Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot 'app\FortniteAutoStretch.exe' }
if (!(Test-Path -LiteralPath $source)) { $source = Join-Path $PSScriptRoot 'dist\FortniteAutoStretch.exe' }
if (!(Test-Path -LiteralPath $source)) { & (Join-Path $PSScriptRoot 'build.ps1') }
if (Get-Process FortniteAutoStretch -ErrorAction SilentlyContinue) { throw 'Bitte Fortnite Auto Stretch zuerst über das Tray-Symbol beenden.' }
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\FortniteAutoStretch'
New-Item -ItemType Directory -Force -Path $installDir | Out-Null
$exePath = Join-Path $installDir 'FortniteAutoStretch.exe'
Copy-Item -LiteralPath $source -Destination $exePath -Force
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -Path $runKey -Force | Out-Null
New-ItemProperty -Path $runKey -Name FortniteAutoStretch -Value ('"' + $exePath + '" --tray') -PropertyType String -Force | Out-Null
$shortcutPath = Join-Path ([Environment]::GetFolderPath('Desktop')) 'Fortnite Auto Stretch.lnk'
$shellObject = New-Object -ComObject WScript.Shell
$shortcut = $shellObject.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installDir
$shortcut.Description = '1720 x 1080 automatisch beim Fortnite-Start'
$shortcut.Save()
Write-Host "Installiert: $exePath"
Write-Host 'Autostart aktiviert. Desktop-Verknüpfung erstellt.'
if (!$NoLaunch) { Start-Process -FilePath $exePath -WindowStyle Hidden }
