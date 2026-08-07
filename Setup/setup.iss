; ═══════════════════════════════════════════════════════════════════════
; FILE: Setup/setup.iss
; PROJECT: ProGlassAutomation
; PURPOSE: Modern, high-fidelity Inno Setup Configuration Script
; ═══════════════════════════════════════════════════════════════════════

[Setup]
AppId={{D3B3A5A3-98C1-4DCE-A4A6-2F1CE8F4F50C}
AppName=ProGlass Automation
AppVersion=1.0.0
AppPublisher=ProGlass Automation
AppPublisherURL=https://github.com/JUBERSALEEM/ProGlassAutomationADV
AppSupportURL=https://github.com/JUBERSALEEM/ProGlassAutomationADV
AppUpdatesURL=https://github.com/JUBERSALEEM/ProGlassAutomationADV
DefaultDirName={localappdata}\ProGlassAutomation
DisableProgramGroupPage=yes
; Clean modern style layout and animations
WizardStyle=modern
SetupIconFile=Images\app_icon.ico
UninstallDisplayIcon={app}\ProGlassAutomation.exe
Compression=lzma2/max
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=Setup
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\ProGlass Automation"; Filename: "{app}\ProGlassAutomation.exe"; IconFilename: "{app}\favicon.ico"
Name: "{autodesktop}\ProGlass Automation"; Filename: "{app}\ProGlassAutomation.exe"; Tasks: desktopicon; IconFilename: "{app}\favicon.ico"

[Run]
Filename: "{app}\ProGlassAutomation.exe"; Description: "{cm:LaunchProgram,ProGlass Automation}"; Flags: nowait postinstall skipifsilent

[Code]
// Custom modern animations & visual behaviors can be script-injected here
procedure InitializeWizard;
begin
  // Set custom wizard colors or stylish adjustments if needed
end;
