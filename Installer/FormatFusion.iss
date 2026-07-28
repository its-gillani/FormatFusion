; FormatFusion Inno Setup Script
; Produces: FormatFusion-Setup.exe

#define AppName "FormatFusion"
#define AppVersion "1.0.0"
#define AppPublisher "FormatFusion"
#define AppURL "https://github.com/FormatFusion"
#define AppExeName "FormatFusion.exe"
#define PublishDir "..\Publish\FormatFusion"

[Setup]
; Unique GUID - regenerate if forking
AppId={{A7B3C2D1-E4F5-6789-ABCD-EF0123456789}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
OutputDir=..\Installer\Output
OutputBaseFilename=FormatFusion-Setup-{#AppVersion}
SetupIconFile=..\FormatFusion.UI\icon.ico
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; Require 64-bit Windows 10+
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
MinVersion=10.0.19041
; Privilege level
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=commandline

; Signing (optional - comment out if no cert)
; SignTool=signtool sign /a /td sha256 /fd sha256 /tr http://timestamp.digicert.com $f

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Create a Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; All app files from publish output
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Start menu
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
; Desktop (optional)
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; IconFilename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
; Launch app after install
Filename: "{app}\{#AppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Clean up logs and settings only if user opts in
; Type: filesandordirs; Name: "{localappdata}\FormatFusion"

[Code]
// Verify minimum Windows version before install
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  if Version.Major < 10 then
  begin
    MsgBox('FormatFusion requires Windows 10 or later.', mbError, MB_OK);
    Result := False;
  end
  else
    Result := True;
end;
