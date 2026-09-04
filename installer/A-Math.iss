; A-Math Windows installer (Inno Setup 6.5+).
; Compiled from Unity after a Windows player build:
;   ISCC /DSourceDir=<player folder> /DOutputDir=<out> /DAppVersion=<ver> A-Math.iss
; Do not append a zip payload to a custom Setup.exe — Defender treats that as a dropper.

#ifndef SourceDir
  #error SourceDir must be defined. Build the Windows player from Unity (A-Math / Windows / Build Player and Setup.exe).
#endif
#ifndef OutputDir
  #define OutputDir "."
#endif
#ifndef AppVersion
  #define AppVersion "1.0"
#endif

#define MyAppName "A-Math"
#define MyAppPublisher "A-Math"
#define MyAppExeName "A-Math.exe"

[Setup]
AppId={{82D10FA5-8317-1644-0AA6-06EC151887BC}
AppName={#MyAppName}
AppVersion={#AppVersion}
AppVerName={#MyAppName} {#AppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
OutputDir={#OutputDir}
OutputBaseFilename=Setup
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} Setup
VersionInfoProductName={#MyAppName}
VersionInfoVersion={#AppVersion}
VersionInfoTextVersion={#AppVersion}
MinVersion=6.1sp1
CloseApplications=yes
RestartApplications=no
LanguageDetectionMethod=uilanguage
ShowLanguageDialog=no
AllowNoIcons=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "thai"; MessagesFile: "Thai.isl"

[CustomMessages]
english.DeleteUserData=Also delete saves, accounts, match history, and settings?
thai.DeleteUserData=ลบเซฟเกม บัญชีผู้ใช้ ประวัติการแข่งขัน และการตั้งค่าด้วยหรือไม่?

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "Setup.exe,Setup.exe.sha256,*DoNotShip*,*DontShipItWithYourGame*"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    if MsgBox(CustomMessage('DeleteUserData'), mbConfirmation, MB_YESNO) = IDYES then
    begin
      DataDir := ExpandConstant('{%USERPROFILE}\AppData\LocalLow\DefaultCompany\A-Math');
      DelTree(DataDir, True, True, True);
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\DefaultCompany\A-Math');
    end;
  end;
end;
