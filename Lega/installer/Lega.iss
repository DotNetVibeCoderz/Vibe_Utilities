; Installer Lega (Inno Setup 6).
; Bangun lewat installer\build.ps1, yang lebih dulu menjalankan dotnet publish.

#define AppName "Lega"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "Gravicode Studios"
#define AppUrl "https://github.com/DotNetVibeCoderz/Vibe_Utilities"
#define PublishDir "..\bin\Release\net10.0-windows\win-x64\publish"

[Setup]
AppId={{6E1C2B9A-4F3D-4C8E-9B57-5A1D0E7C3F21}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Pasang per pengguna tanpa UAC; pengguna bisa memilih "untuk semua pengguna" di dialog.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=Output
OutputBaseFilename=Lega-Setup-{#AppVersion}
SetupIconFile=..\Assets\lega.ico
UninstallDisplayIcon={app}\Lega.exe
UninstallDisplayName={#AppName} - pembersih drive C
AppCopyright=(c) 2026 Gravicode Studios, dipimpin oleh Kang Fadhil
VersionInfoCompany=Gravicode Studios
VersionInfoDescription=Installer Lega - pembersih drive C
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
WizardSizePercent=100

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.LaunchLega=Jalankan Lega sekarang
english.DesktopIcon=Buat ikon di desktop

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\Lega.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\Lega.exe"; Comment: "Pembersih drive C"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\Lega.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\Lega.exe"; Description: "{cm:LaunchLega}"; Flags: nowait postinstall skipifsilent
