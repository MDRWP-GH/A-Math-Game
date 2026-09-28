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
  #error AppVersion must be defined from the Windows player build version.
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
DisableDirPage=no
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
english.UninstallTitle=Remove A-Math?
english.UninstallBody=A-Math {#AppVersion} will be removed from this computer. Your player data is kept unless you explicitly choose to delete it below.
english.DeleteUserData=Also delete saves, accounts, match history, and settings
english.CancelUninstall=Cancel
english.RemoveGame=Remove Game
thai.UninstallTitle=ถอนการติดตั้ง A-Math?
thai.UninstallBody=A-Math {#AppVersion} จะถูกลบออกจากคอมพิวเตอร์ ข้อมูลผู้เล่นจะถูกเก็บไว้ เว้นแต่คุณเลือกให้ลบด้านล่าง
thai.DeleteUserData=ลบเซฟเกม บัญชีผู้ใช้ ประวัติการแข่งขัน และการตั้งค่าด้วย
thai.CancelUninstall=ยกเลิก
thai.RemoveGame=ถอนการติดตั้ง

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "save\*,Setup.exe,Setup.exe.sha256,*DoNotShip*,*DontShipItWithYourGame*"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Check: not WizardNoIcons

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[Code]
var
  DeleteUserData: Boolean;

function InitializeUninstall(): Boolean;
var
  Form: TSetupForm;
  TitleLabel, BodyLabel: TNewStaticText;
  DeleteCheck: TNewCheckBox;
  KeepButton, RemoveButton: TNewButton;
begin
  Form := CreateCustomForm(ScaleX(520), ScaleY(270), False, False);
  Form.Caption := '{#MyAppName}';
  Form.Position := poScreenCenter;

  TitleLabel := TNewStaticText.Create(Form);
  TitleLabel.Parent := Form;
  TitleLabel.Caption := CustomMessage('UninstallTitle');
  TitleLabel.Font.Style := [fsBold];
  TitleLabel.Font.Size := 14;
  TitleLabel.SetBounds(ScaleX(24), ScaleY(22), ScaleX(470), ScaleY(30));

  BodyLabel := TNewStaticText.Create(Form);
  BodyLabel.Parent := Form;
  BodyLabel.Caption := CustomMessage('UninstallBody');
  BodyLabel.AutoSize := False;
  BodyLabel.WordWrap := True;
  BodyLabel.SetBounds(ScaleX(24), ScaleY(62), ScaleX(470), ScaleY(62));

  DeleteCheck := TNewCheckBox.Create(Form);
  DeleteCheck.Parent := Form;
  DeleteCheck.Caption := CustomMessage('DeleteUserData');
  DeleteCheck.Checked := False;
  DeleteCheck.SetBounds(ScaleX(24), ScaleY(142), ScaleX(470), ScaleY(30));

  KeepButton := TNewButton.Create(Form);
  KeepButton.Parent := Form;
  KeepButton.Caption := CustomMessage('CancelUninstall');
  KeepButton.ModalResult := mrCancel;
  KeepButton.Cancel := True;
  KeepButton.Default := True;
  KeepButton.SetBounds(ScaleX(220), ScaleY(210), ScaleX(130), ScaleY(32));

  RemoveButton := TNewButton.Create(Form);
  RemoveButton.Parent := Form;
  RemoveButton.Caption := CustomMessage('RemoveGame');
  RemoveButton.ModalResult := mrOk;
  RemoveButton.SetBounds(ScaleX(362), ScaleY(210), ScaleX(130), ScaleY(32));

  Result := Form.ShowModal = mrOk;
  DeleteUserData := Result and DeleteCheck.Checked;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    if DeleteUserData then
    begin
      DelTree(ExpandConstant('{app}\save'), True, True, True);
      DataDir := ExpandConstant('{%USERPROFILE}\AppData\LocalLow\DefaultCompany\A-Math');
      DelTree(DataDir, True, True, True);
      RegDeleteKeyIncludingSubkeys(HKCU, 'Software\DefaultCompany\A-Math');
    end;
  end;
end;
