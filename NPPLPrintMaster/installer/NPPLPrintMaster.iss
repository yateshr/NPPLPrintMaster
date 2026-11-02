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
WizardSizePercent=110
DisableWelcomePage=no
DisableFinishedPage=no
InfoBeforeFile=BeforeInstall.rtf
InfoAfterFile=AfterInstall.rtf
SetupIconFile=..\Branding\PrintMaster.ico
WizardImageFile=..\Branding\InstallerWelcome.bmp
WizardSmallImageFile=..\Branding\InstallerSmall.bmp
WizardImageStretch=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\NPPLPrintMaster.exe
ChangesAssociations=yes
[Messages]
WelcomeLabel1=Welcome to NPPL PrintMaster
WelcomeLabel2=Print. Layout. Create.%nBuilt by Yatesh Rohit.%n%nCreate image collages, prepare label images and export production-ready layouts from one workspace.%n%nThis wizard will install version {#AppVersion}. Your existing preferences will be kept.%n%nSave your open work before continuing.
WizardInfoBefore=Before you install
InfoBeforeLabel=A quick introduction to your PrintMaster workspace.
InfoBeforeClickLabel=Review the information below, then click Next to choose where PrintMaster is installed.
WizardInfoAfter=Getting started with PrintMaster
InfoAfterLabel=Installation is complete. Here are a few useful next steps.
InfoAfterClickLabel=Click Next to finish setup and open PrintMaster.
FinishedHeadingLabel=Your PrintMaster workspace is ready
FinishedLabel=NPPL PrintMaster has been installed successfully.%n%nBuilt by Yatesh Rohit.%n%nCreate a collage, prepare your images or continue an existing layout. Use Settings to choose your preferred interface.%n%nSelect Open NPPLPrintMaster below to begin.
FinishedLabelNoIcons=NPPL PrintMaster has been installed successfully.%n%nBuilt by Yatesh Rohit.%n%nOpen NPPLPrintMaster.exe from your selected installation folder to begin.
[Files]
Source: "..\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb,Verify*.exe,Verify*.exe.config,*Check.exe,*Check.exe.config,*.vshost.*,settings.ini,*.log,Logs\*,CollageRecovery\*,Updates\*"
[Icons]
Name: "{group}\NPPLPrintMaster"; Filename: "{app}\NPPLPrintMaster.exe"
Name: "{autodesktop}\NPPLPrintMaster"; Filename: "{app}\NPPLPrintMaster.exe"
[Run]
Filename: "{app}\NPPLPrintMaster.exe"; Description: "Open NPPLPrintMaster"; Flags: nowait postinstall skipifsilent
