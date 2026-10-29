#define AppVersion GetFileVersion('..\bin\Release\NPPLPrintMaster.exe')
[Setup]
AppId={{7F44B4EA-2DA7-4FB3-97E9-202146A56D7D}
AppName=NPPLPrintMaster
AppVersion={#AppVersion}
AppPublisher=NPPL
DefaultDirName={localappdata}\Programs\NPPLPrintMaster
DefaultGroupName=NPPLPrintMaster
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\output\installer
OutputBaseFilename=NPPLPrintMaster-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\NPPLPrintMaster.exe
[Files]
Source: "..\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,Verify*.exe,Verify*.exe.config,*Check.exe,*Check.exe.config,*.vshost.*,settings.ini,*.log,Logs\*,CollageRecovery\*,Updates\*"
[Icons]
Name: "{group}\NPPLPrintMaster"; Filename: "{app}\NPPLPrintMaster.exe"
Name: "{autodesktop}\NPPLPrintMaster"; Filename: "{app}\NPPLPrintMaster.exe"
[Run]
Filename: "{app}\NPPLPrintMaster.exe"; Description: "Open NPPLPrintMaster"; Flags: nowait postinstall skipifsilent
