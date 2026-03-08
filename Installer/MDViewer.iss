; MDViewer Inno Setup Script
; Generates an installer for MDViewer
; Copyright (c) 2025 Nicola Carpanese

#define MyAppName "MDViewer"
#ifndef MyAppVersion
#define MyAppVersion "2.0.0"
#endif
#define MyAppPublisher "Nicola Carpanese"
#define MyAppURL "https://github.com/n-car/MDViewer"
#define MyAppExeName "MDViewer.exe"
#define MyAppAssocName "Markdown File"
#define MyAppAssocExt ".md"
#define MyAppAssocKey StringChange(MyAppAssocName, " ", "") + MyAppAssocExt

[Setup]
; Unique app identifier (generate a new GUID for each app)
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
; Uncomment to require admin privileges
;PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; Output folder and file name
OutputDir=..\Release
OutputBaseFilename=MDViewer-Setup-{#MyAppVersion}
; Setup icon
SetupIconFile=..\MDViewer\Resources\md-viewer-icon.ico
; Compression
Compression=lzma2/ultra64
SolidCompression=yes
; Modern style
WizardStyle=modern
; Windows requirements
MinVersion=10.0
; Uninstall
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
; Architecture
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Notify Windows shell that file associations were updated
ChangesAssociations=yes

[Languages]
Name: "italian"; MessagesFile: "compiler:Languages\Italian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
english.AssocMdFiles=Associate .md files with %1
italian.AssocMdFiles=Associa i file .md a %1

english.FileAssociationsGroup=File associations:
italian.FileAssociationsGroup=Associazioni file:

english.MarkdownFileTypeName=Markdown File
italian.MarkdownFileTypeName=File Markdown

english.DotNetRequiredLine1=MDViewer requires .NET Framework 4.8 or later.
italian.DotNetRequiredLine1=MDViewer richiede .NET Framework 4.8 o superiore.

english.DotNetRequiredLine2=Do you want to open the Microsoft download page?
italian.DotNetRequiredLine2=Vuoi aprire la pagina di download di Microsoft?

english.RemoveUserDataPrompt=Do you also want to remove MDViewer settings and data?
italian.RemoveUserDataPrompt=Vuoi rimuovere anche le impostazioni e i dati di MDViewer?

english.DefaultProgramsDescription=MDViewer Markdown file viewer
italian.DefaultProgramsDescription=MDViewer visualizzatore file Markdown

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
; Quick Launch removed - obsolete since Windows 7
Name: "fileassoc"; Description: "{cm:AssocMdFiles,{#MyAppName}}"; GroupDescription: "{cm:FileAssociationsGroup}"; Flags: checkedonce

[Files]
; Main application file
Source: "..\MDViewer\bin\Release\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
; DLLs and dependencies
Source: "..\MDViewer\bin\Release\*.dll"; DestDir: "{app}"; Flags: ignoreversion
; Configuration files
Source: "..\MDViewer\bin\Release\*.config"; DestDir: "{app}"; Flags: ignoreversion
; Runtime folder for WebView2
Source: "..\MDViewer\bin\Release\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist
; NOTE: Do not use "Flags: ignoreversion" for shared system files

[Registry]
; .md file association
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocExt}\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}"; ValueType: string; ValueName: ""; ValueData: "{cm:MarkdownFileTypeName}"; Flags: uninsdeletekey; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\{#MyAppAssocKey}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc

; Also associate .markdown files
Root: HKA; Subkey: "Software\Classes\.markdown\OpenWithProgids"; ValueType: string; ValueName: "{#MyAppAssocKey}"; ValueData: ""; Flags: uninsdeletevalue; Tasks: fileassoc

; Add to the "Open with" menu
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}"; ValueType: string; ValueName: "FriendlyAppName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekeyifempty; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\DefaultIcon"; ValueType: string; ValueName: ""; ValueData: "{app}\{#MyAppExeName},0"; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\shell\open\command"; ValueType: string; ValueName: ""; ValueData: """{app}\{#MyAppExeName}"" ""%1"""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".md"; ValueData: ""; Tasks: fileassoc
Root: HKA; Subkey: "Software\Classes\Applications\{#MyAppExeName}\SupportedTypes"; ValueType: string; ValueName: ".markdown"; ValueData: ""; Tasks: fileassoc

; Register app capabilities for Windows "Default apps"
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationName"; ValueData: "{#MyAppName}"; Flags: uninsdeletekeyifempty; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities"; ValueType: string; ValueName: "ApplicationDescription"; ValueData: "{cm:DefaultProgramsDescription}"; Flags: uninsdeletekeyifempty; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".md"; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletekeyifempty; Tasks: fileassoc
Root: HKA; Subkey: "Software\{#MyAppName}\Capabilities\FileAssociations"; ValueType: string; ValueName: ".markdown"; ValueData: "{#MyAppAssocKey}"; Flags: uninsdeletekeyifempty; Tasks: fileassoc
Root: HKA; Subkey: "Software\RegisteredApplications"; ValueType: string; ValueName: "{#MyAppName}"; ValueData: "Software\{#MyAppName}\Capabilities"; Flags: uninsdeletevalue; Tasks: fileassoc

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
; Quick Launch removed - obsolete since Windows 7

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// Checks whether .NET Framework 4.8 is installed
function IsDotNetFramework48Installed(): Boolean;
var
  Release: Cardinal;
begin
  Result := False;
  if RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', Release) then
  begin
    // 4.8 = 528040 or higher
    Result := (Release >= 528040);
  end;
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := True;

  // Check .NET Framework 4.8
  if not IsDotNetFramework48Installed() then
  begin
    if MsgBox(
      ExpandConstant('{cm:DotNetRequiredLine1}') + #13#10 + #13#10 +
      ExpandConstant('{cm:DotNetRequiredLine2}'),
      mbConfirmation,
      MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet-framework/net48', '', '', SW_SHOW, ewNoWait, ErrorCode);
    end;
    Result := False;
  end;
end;

// Cleans temporary files during uninstallation
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  AppDataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Ask whether to remove user data
    if MsgBox(ExpandConstant('{cm:RemoveUserDataPrompt}'), mbConfirmation, MB_YESNO) = IDYES then
    begin
      AppDataPath := ExpandConstant('{localappdata}\MDViewer');
      if DirExists(AppDataPath) then
      begin
        DelTree(AppDataPath, True, True, True);
      end;
    end;
  end;
end;
