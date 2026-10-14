; SPDX-License-Identifier: MPL-2.0
; This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
; If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

[Code]
function IsStartupObserver(CommandLine, Executable: String): Boolean;
var
  Prefix, Arguments: String;
begin
  Prefix := '"' + Executable + '" ';

  if CompareText(Copy(CommandLine, 1, Length(Prefix)), Prefix) <> 0 then
  begin
    Prefix := Executable + ' ';
  end;

  if CompareText(Copy(CommandLine, 1, Length(Prefix)), Prefix) <> 0 then
  begin
    Result := False;
    Exit;
  end;

  Arguments := Copy(CommandLine, Length(Prefix) + 1, Length(CommandLine));

  Result := (Pos('--watch-hubstaff ', Arguments) = 1) or
    (Pos('"--watch-hubstaff" ', Arguments) = 1) or
    (Arguments = '--watch-hubstaff') or (Arguments = '"--watch-hubstaff"');
end;

function PrepareInstalledApplication(StopObservers: Boolean): Boolean;
var
  Locator, Services, Processes, Process: Variant;
  Observers: Array of Variant;
  Executable: String;
  Index, Count, Attempt: Integer;
  Running: Boolean;
begin
  Result := False;

  Executable := ExpandConstant('{app}\TrackMirror.exe');

  if not FileExists(Executable) then
  begin
    Result := True;
    Exit;
  end;

  try
    Locator := CreateOleObject('WbemScripting.SWbemLocator');

    Services := Locator.ConnectServer('', 'root\CIMV2');

    Processes := Services.ExecQuery('SELECT ExecutablePath,CommandLine,ProcessId FROM Win32_Process ' +
      'WHERE Name=''TrackMirror.exe''');

    Count := 0;

    for Index := 0 to Processes.Count - 1 do
    begin
      Process := Processes.ItemIndex(Index);

      if VarIsNull(Process.ExecutablePath) then Continue;

      if CompareText(Process.ExecutablePath, Executable) <> 0 then Continue;

      if VarIsNull(Process.CommandLine) then Exit;

      // Never force-stop a viewer: it can own screenshot writes, migrations or AI checkpoints.
      if not IsStartupObserver(Process.CommandLine, Executable) then Exit;

      SetArrayLength(Observers, Count + 1);

      Observers[Count] := Process;

      Count := Count + 1;
    end;

    if not StopObservers then
    begin
      Result := True;
      Exit;
    end;

    // This separate observer only reads settings/process state; it owns no pending data writes.
    for Index := 0 to Count - 1 do
    begin
      Observers[Index].Terminate(0);
    end;

    for Attempt := 0 to 14 do
    begin
      Running := False;

      Processes := Services.ExecQuery('SELECT ExecutablePath FROM Win32_Process ' +
        'WHERE Name=''TrackMirror.exe''');

      for Index := 0 to Processes.Count - 1 do
      begin
        Process := Processes.ItemIndex(Index);

        if VarIsNull(Process.ExecutablePath) then Continue;

        if CompareText(Process.ExecutablePath, Executable) = 0 then
        begin
          Running := True;
        end;
      end;

      if not Running then
      begin
        Result := True;
        Exit;
      end;

      Sleep(200);
    end;
  except
    Log('Could not confirm that this installation has stopped. No program files will be changed.');
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';

  if not PrepareInstalledApplication(True) then
  begin
    Result := FmtMessage(CustomMessage('CloseViewer'), ['{#AppName}']);
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := PrepareInstalledApplication(False);

  if not Result then
  begin
    SuppressibleMsgBox(FmtMessage(CustomMessage('CloseViewer'), ['{#AppName}']), mbError, MB_OK, IDOK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  Found: TFindRec;
  Shell, Shortcut: Variant;
  Path, Executable: String;
begin
  if CurUninstallStep <> usUninstall then Exit;

  if not PrepareInstalledApplication(True) then
  begin
    SuppressibleMsgBox(FmtMessage(CustomMessage('CloseViewer'), ['{#AppName}']), mbError, MB_OK, IDOK);

    Abort;
  end;

  Executable := ExpandConstant('{app}\TrackMirror.exe');

  if not FindFirst(ExpandConstant('{userstartup}\TrackMirror-*.lnk'), Found) then Exit;

  try
    Shell := CreateOleObject('WScript.Shell');

    repeat
      Path := ExpandConstant('{userstartup}\') + Found.Name;

      try
        Shortcut := Shell.CreateShortcut(Path);

        if CompareText(Shortcut.TargetPath, Executable) = 0 then
        begin
          DeleteFile(Path);
        end;
      except
        // Leave unreadable or unrecognised shortcuts intact; another installation may own them.
        Log('A Startup shortcut could not be verified and was retained.');
      end;
    until not FindNext(Found);
  finally
    FindClose(Found);
  end;
end;
