; SPDX-License-Identifier: MPL-2.0
; This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
; If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

#if Ver < EncodeVer(6, 3, 0)
  #error Inno Setup 6.3 or newer is required.
#endif

#ifndef AppSource
  #error Pass the validated application directory as /DAppSource.
#endif
#ifndef AppVersion
  #error Pass the release version as /DAppVersion.
#endif
#ifndef NumericVersion
  #error Pass the numeric file version as /DNumericVersion.
#endif
#ifndef AppName
  #define AppName "TrackMirror"
#endif
#ifndef AppId
  #define AppId "E0B7F36C-BF57-4E05-BB50-EDBA51A04930"
#endif

[Setup]
; Keep AppId and DefaultDirName stable across versions and display-name changes.
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
VersionInfoVersion={#NumericVersion}
VersionInfoProductVersion={#NumericVersion}
VersionInfoProductTextVersion={#AppVersion}
DefaultDirName={localappdata}\Programs\TrackMirror
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
UninstallDisplayIcon={app}\TrackMirror.exe
UninstallDisplayName={#AppName}
SetupIconFile={#SourcePath}..\..\src\TrackMirror.App\Resources\Application.ico
LicenseFile={#AppSource}\LICENSE
WizardStyle=modern
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=no
RestartApplications=no
OutputBaseFilename=TrackMirror-v{#AppVersion}-win-x64-setup

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#AppSource}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\TrackMirror.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\TrackMirror.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\TrackMirror.exe"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

; No recursive removal rules: history, settings, backups and downloaded models belong to the user.

#include "Messages.iss"
#include "Lifecycle.iss"
