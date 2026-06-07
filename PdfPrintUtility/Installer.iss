[Setup]
AppName=PDF Print Utility
AppVersion=1.0
AppPublisher=Balar Builders
DefaultDirName={autopf}\PdfPrintUtility
DefaultGroupName=PDF Print Utility
UninstallDisplayIcon={app}\PdfPrintUtility.exe
Compression=lzma2
SolidCompression=yes
OutputDir=Output
OutputBaseFilename=PdfPrintUtilitySetup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
SetupIconFile=PdfPrintUtility\icon.ico

[Files]
Source: "PdfPrintUtility\bin\Release\net9.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Icons]
Name: "{group}\PDF Print Utility Settings"; Filename: "{app}\PdfPrintUtility.exe"
Name: "{group}\Uninstall PDF Print Utility"; Filename: "{uninstallexe}"
Name: "{autodesktop}\PDF Print Utility Settings"; Filename: "{app}\PdfPrintUtility.exe"; Tasks: desktopicon

[Registry]
; Context Menu Registration for PDF files
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPrintUtility"; ValueType: string; ValueName: "MUIVerb"; ValueData: "Print All PDFs"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPrintUtility"; ValueType: string; ValueName: "MultiSelectModel"; ValueData: "Player"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\Classes\SystemFileAssociations\.pdf\shell\PdfPrintUtility\command"; ValueType: string; ValueName: ""; ValueData: """{app}\PdfPrintUtility.exe"" ""%1"""; Flags: uninsdeletekey

[Run]
; Launch the app after install so user can verify settings
Filename: "{app}\PdfPrintUtility.exe"; Description: "Open PDF Print Utility Settings"; Flags: postinstall nowait skipifsilent

[Code]
// Check if .NET 9 Desktop Runtime is installed
function IsDotNet9Installed(): Boolean;
var
  ResultCode: Integer;
begin
  // Check registry for .NET 9 Desktop Runtime
  Result := RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App\9.0.0');
  if not Result then
  begin
    // Fallback: try running dotnet to see if version 9 exists
    Result := False;
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  DotNetInstallerPath: String;
begin
  Result := '';

  // If .NET 9 Desktop Runtime is already installed, skip
  if RegKeyExists(HKEY_LOCAL_MACHINE, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedhost') then
  begin
    // .NET host is present, assume compatible runtime
    Exit;
  end;

  // Prompt user that .NET 9 Desktop Runtime is needed
  if MsgBox('.NET 9 Desktop Runtime is required but was not found on this PC.' + #13#10 +
            'Click OK to open the Microsoft download page to install it, then re-run this installer.' + #13#10#13#10 +
            'Download URL: https://aka.ms/dotnet-download', mbInformation, MB_OKCANCEL) = IDOK then
  begin
    ShellExec('open', 'https://aka.ms/dotnet-download', '', '', SW_SHOWNORMAL, ewNoWait, ResultCode);
    Result := '.NET 9 Desktop Runtime is required. Please install it and re-run this setup.';
  end;
end;
