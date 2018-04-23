#include <idp.iss>

#define MyAppName "NordInvasion Launcher"
#define MyAppVersion "3.1.3"
#define MyAppPublisher "NordInvasion"
#define MyAppURL "https://nordinvasion.com"
#define MyAppExeName "NordInvasion_Launcher.exe"
#define RepoPath "C:\Users\andy\source\repos"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{F8906AF0-A0EE-4C47-A499-01F305FF0AA4}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={pf}\{#MyAppName}
DisableProgramGroupPage=yes
LicenseFile={#RepoPath}\gpl-3.0.txt
InfoAfterFile={#RepoPath}\postinstall.txt
OutputDir={#RepoPath}
OutputBaseFilename=ni_launcher_{#MyAppVersion}
VersionInfoVersion={#MyAppVersion}
SetupIconFile={#RepoPath}\cs-updater\cs-updater\ni-badge.ico
Compression=lzma
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\NordInvasion_Launcher.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Microsoft.WindowsAPICodePack.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Microsoft.WindowsAPICodePack.Shell.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Microsoft.WindowsAPICodePack.Shell.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Microsoft.WindowsAPICodePack.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Newtonsoft.Json.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Newtonsoft.Json.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\NLog.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\NLog.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\NLog.xml"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\NordInvasion_Launcher.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\NordInvasion_Launcher.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\updater-lib.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\updater-lib.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\updater-permissions.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\updater-permissions.exe.config"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\updater-permissions.pdb"; DestDir: "{app}"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Lang\en-GB\MainWindow.en-GB.xaml"; DestDir: "{app}\Lang\en-GB\"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Lang\en-GB\NotificationWindow.en-GB.xaml"; DestDir: "{app}\Lang\en-GB\"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Lang\en-GB\OptionsHelp.en-GB.xaml"; DestDir: "{app}\Lang\en-GB\"; Flags: ignoreversion
Source: "C:\Users\andy\source\repos\cs-updater\cs-updater\bin\Release\Lang\en-GB\OptionsWindow.en-GB.xaml"; DestDir: "{app}\Lang\en-GB\"; Flags: ignoreversion
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{commonprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function Framework461IsNotInstalled(): Boolean;
var
  bSuccess: Boolean;
  regVersion: Cardinal;
begin
  Result := True;

  bSuccess := RegQueryDWordValue(HKLM, 'Software\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', regVersion);
  if (True = bSuccess) and (regVersion >= 394254) then begin
    Result := False;
  end;
end;

procedure InitializeWizard;
begin
  if Framework461IsNotInstalled() then
  begin
    idpAddFile('https://www.microsoft.com/net/download/thank-you/net461', ExpandConstant('{tmp}\NetFrameworkInstaller.exe'));
    idpDownloadAfter(wpReady);
  end;
end;

procedure InstallFramework;
var
  StatusText: string;
  ResultCode: Integer;
begin
  StatusText := WizardForm.StatusLabel.Caption;
  WizardForm.StatusLabel.Caption := 'Installing .NET Framework 4.5.2. This might take a few minutes...';
  WizardForm.ProgressGauge.Style := npbstMarquee;
  try
    if not Exec(ExpandConstant('{tmp}\NetFrameworkInstaller.exe'), '/passive /norestart', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      MsgBox('.NET installation failed with code: ' + IntToStr(ResultCode) + '.', mbError, MB_OK);
    end;
  finally
    WizardForm.StatusLabel.Caption := StatusText;
    WizardForm.ProgressGauge.Style := npbstNormal;

    DeleteFile(ExpandConstant('{tmp}\NetFrameworkInstaller.exe'));
  end;
end;
