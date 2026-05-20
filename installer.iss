; TAY System Optimizer - Inno Setup Script
; Version: 0.1.1.2 (synced by tools/set_version.ps1)

#define MyAppName "TAY System Optimizer"
#define MyAppVersion "0.1.1.2"
#define MyAppPublisher "Palm1ye"
#define MyAppURL "https://github.com/Palm1ye/TAY"
#define MyAppExeName "TAY.exe"

[Setup]
AppId={{B7A1D9E0-4F2C-4A8B-9C3E-1D5F6A7B8C90}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
DefaultDirName={autopf}\TAY
DefaultGroupName=TAY
OutputBaseFilename=TAY_Setup
SetupIconFile=Assets\tay.ico
UninstallDisplayIcon={app}\Assets\tay.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
AppMutex=TAY_System_Optimizer_Mutex
OutputDir=Output
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
LicenseFile=LICENSE

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Include the entire self-contained publish folder
Source: "bin\x64\Release\net8.0-windows10.0.19041.0\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\tay.ico"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\tay.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
