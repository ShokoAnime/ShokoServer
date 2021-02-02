; Script generated by the Inno Setup Script Wizard.
; SEE THE DOCUMENTATION FOR DETAILS ON CREATING INNO SETUP SCRIPT FILES!

#define AppVer GetFileVersion('..\Shoko.Server\bin\Release\net5.0-windows\win10-x64\ShokoServer.exe')   
#define MyAppExeName "ShokoServer.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application.
; Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{0BA2D22B-A0B7-48F8-8AA1-BAAEFC2034CB}
AppName=Shoko Server
AppVersion=4.1.0.0
AppVerName=Shoko Server
AppPublisher=Shoko Team
AppPublisherURL=https://shokoanime.com/
AppSupportURL=https://github.com/ShokoAnime/
AppUpdatesURL=https://shokoanime.com/downloads/
DefaultDirName={pf}\Shoko\Shoko Server
DefaultGroupName=Shoko Server
AllowNoIcons=yes
OutputBaseFilename=Shoko_Server_Setup_{#AppVer}
UninstallDisplayIcon={app}\{#MyAppExeName}
SolidCompression=yes
Compression=lzma2/ultra64
LZMAUseSeparateProcess=yes
LZMADictionarySize=1048576
LZMANumFastBytes=273
PrivilegesRequired=admin
InternalCompressLevel=max

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "firewall"; Description: "Add Shoko Server to Windows Firewall Exception list"
Name: "StartMenuEntry"; Description: "Launch Shoko Server on startup" 
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 0,6.1

[Icons]
Name: "{group}\Shoko Server"; Filename: "{app}\ShokoServer.exe"
Name: "{group}\{cm:UninstallProgram,Shoko Server}"; Filename: "{uninstallexe}"
Name: "{commondesktop}\Shoko Server"; Filename: "{app}\ShokoServer.exe"; Tasks: desktopicon
Name: "{commonstartup}\Shoko Server"; Filename: "{app}\ShokoServer.exe"; Tasks: StartMenuEntry;

[Run]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall add rule name=""Shoko Server - Client Port"" dir=in action=allow protocol=TCP localport=8111"; Flags: runhidden; StatusMsg: "Open exception on firewall..."; Tasks: Firewall
Filename: "{app}\FixPermissions.bat";
Filename: "{app}\ShokoServer.exe"; Flags: nowait postinstall skipifsilent shellexec; Description: "{cm:LaunchProgram,Shoko Server}"
Filename: "https://docs.shokoanime.com/server/install/"; Flags: shellexec runasoriginaluser postinstall; Description: "Shoko Server Install Guide"
Filename: "https://shokoanime.com/blog/shoko-version-4-1-0-released/"; Flags: shellexec runasoriginaluser postinstall; Description: "View 4.1.0 Release Notes"

[UninstallRun]
Filename: "{sys}\netsh.exe"; Parameters: "advfirewall firewall delete rule name=""Shoko Server - Client Port"" protocol=TCP localport=8111"; Flags: runhidden; StatusMsg: "Closing exception on firewall..."; Tasks: Firewall
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerImage"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerBinary"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerMetro"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerMetroImage"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerPlex"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerKodi"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerREST"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8111/JMMServerStreaming"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";
Filename: "{sys}\netsh.exe"; Parameters: "http delete urlacl url=http://+:8112/JMMFilePort"; Flags: runhidden; StatusMsg: "Unregistering WCF Service...";

[Registry]
Root: HKLM; Subkey: "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run"; ValueName: "JMMServer"; ValueType: none; Flags: deletevalue;

[Dirs]
Name: "{app}"; Permissions: users-full
Name: "C:\ProgramData\ShokoServer"; Permissions: users-full

[Files]
Source: "..\..\ShokoServer\Installer\FixPermissions.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\Shoko.Server\bin\Release\net5.0-windows\win10-x64\*"; DestDir: "{app}"; Flags:  ignoreversion recursesubdirs createallsubdirs

[Code]

{ ///////////////////////////////////////////////////////////////////// }
function GetUninstallString(): String;
var
  sUnInstPath: String;
  sUnInstallString: String;
begin
  sUnInstPath := ExpandConstant('Software\Microsoft\Windows\CurrentVersion\Uninstall\{#emit SetupSetting("AppId")}_is1');
  sUnInstallString := '';
  if not RegQueryStringValue(HKLM, sUnInstPath, 'UninstallString', sUnInstallString) then
    RegQueryStringValue(HKCU, sUnInstPath, 'UninstallString', sUnInstallString);
  Result := sUnInstallString;
end;


{ ///////////////////////////////////////////////////////////////////// }
function IsUpgrade(): Boolean;
begin
  Result := (GetUninstallString() <> '');
end;


{ ///////////////////////////////////////////////////////////////////// }
function UnInstallOldVersion(): Integer;
var
  sUnInstallString: String;
  iResultCode: Integer;
begin
{ Return Values: }
{ 1 - uninstall string is empty }
{ 2 - error executing the UnInstallString }
{ 3 - successfully executed the UnInstallString }

  { default return value }
  Result := 0;

  { get the uninstall string of the old app }
  sUnInstallString := GetUninstallString();
  if sUnInstallString <> '' then begin
    sUnInstallString := RemoveQuotes(sUnInstallString);
    if Exec(sUnInstallString, '/VERYSILENT /NORESTART /SUPPRESSMSGBOXES','', SW_HIDE, ewWaitUntilTerminated, iResultCode) then
      Result := 3
    else
      Result := 2;
  end else
    Result := 1;
end;

{ ///////////////////////////////////////////////////////////////////// }
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if (CurStep=ssInstall) then
  begin
    if (IsUpgrade()) then
    begin
      UnInstallOldVersion();
    end;
  end;
end;

