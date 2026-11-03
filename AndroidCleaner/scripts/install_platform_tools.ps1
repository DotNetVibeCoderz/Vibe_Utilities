# Downloads Google's Android platform-tools (adb) into .tools/platform-tools.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dest = Join-Path $root '.tools'
New-Item -ItemType Directory -Force $dest | Out-Null
$zip = Join-Path $dest 'platform-tools.zip'
Invoke-WebRequest 'https://dl.google.com/android/repository/platform-tools-latest-windows.zip' -OutFile $zip
Expand-Archive $zip -DestinationPath $dest -Force
Remove-Item $zip
& (Join-Path $dest 'platform-tools\adb.exe') version
