# Membangun Lega.exe (self-contained, satu file) lalu installer Inno Setup.
# Pakai:  powershell -File installer\build.ps1 [-Version 1.0.0]
param([string]$Version = "1.0.0")
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

dotnet publish "$root\Lega.csproj" -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:Version=$Version
if ($LASTEXITCODE -ne 0) { throw "dotnet publish gagal" }

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "Inno Setup 6 tidak ditemukan. Pasang dengan: winget install JRSoftware.InnoSetup" }

& $iscc "/DAppVersion=$Version" "$PSScriptRoot\Lega.iss"
if ($LASTEXITCODE -ne 0) { throw "ISCC gagal" }
Get-Item "$PSScriptRoot\Output\Lega-Setup-$Version.exe"
