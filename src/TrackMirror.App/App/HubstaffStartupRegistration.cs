// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.Runtime.InteropServices;
using TrackMirror.Constants;

namespace TrackMirror.App;

internal sealed class HubstaffStartupRegistration
{
    private readonly string _sourceRoot;
    private readonly string _dataRoot;
    private readonly string _executable;
    private readonly string _startupFolder;
    private readonly string _identifier;

    internal HubstaffStartupRegistration(string sourceRoot, string dataRoot,
        string? executable = null, string? startupFolder = null)
    {
        _sourceRoot = Path.GetFullPath(sourceRoot);
        _dataRoot = Path.GetFullPath(dataRoot);
        _executable = executable ?? Path.Combine(AppContext.BaseDirectory, StartupNames.ExecutableName);
        _startupFolder = startupFolder ?? Environment.GetFolderPath(Environment.SpecialFolder.Startup);

        _identifier = SingleInstance.Identifier(dataRoot);
    }

    internal void Apply(bool enabled)
    {
        var path = Path.Combine(_startupFolder,
            StartupNames.ShortcutPrefix + _identifier + StartupNames.ShortcutExtension);

        if (!enabled)
        {
            File.Delete(path);

            return;
        }

        var info = StartInfo(StartupNames.WatchOption);

        Directory.CreateDirectory(_startupFolder);

        var temporary = Path.Combine(_startupFolder, Guid.NewGuid() + StartupNames.ShortcutExtension);

        object? shell = null;

        object? shortcut = null;

        try
        {
            shell = Activator.CreateInstance(Type.GetTypeFromProgID(StartupNames.ShellProgId, throwOnError: true)!);

            shortcut = ((dynamic)shell!).CreateShortcut(temporary);

            dynamic link = shortcut!;

            link.TargetPath = info.FileName;
            link.Arguments = string.Join(StartupNames.ArgumentSeparator, info.ArgumentList.Select(QuotePath));
            link.WorkingDirectory = Path.GetDirectoryName(_executable);
            link.Description = ApplicationConstants.DisplayName;
            link.Save();

            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (shortcut != null)
            {
                Marshal.FinalReleaseComObject(shortcut);
            }

            if (shell != null)
            {
                Marshal.FinalReleaseComObject(shell);
            }

            File.Delete(temporary);
        }
    }

    internal void StartWatcher()
    {
        if (SingleInstance.Exists(StartupNames.WatcherMutexPrefix + _identifier))
        {
            return;
        }

        using var process = Process.Start(StartInfo(StartupNames.WatchOption));
    }

    internal void StartViewer()
    {
        using var process = Process.Start(StartInfo(StartupNames.LaunchOption));
    }

    internal ProcessStartInfo StartInfo(string mode)
    {
        var info = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        info.ArgumentList.Add(mode);
        info.ArgumentList.Add(ApplicationConstants.SourceRootOption);
        info.ArgumentList.Add(_sourceRoot);
        info.ArgumentList.Add(ApplicationConstants.DataRootOption);
        info.ArgumentList.Add(_dataRoot);

        return info;
    }

    private static string QuotePath(string value)
    {
        if (value.Contains('"'))
        {
            throw new ArgumentException(nameof(value));
        }

        var trailing = 0;

        while (trailing < value.Length && value[value.Length - trailing - 1] == '\\')
        {
            trailing++;
        }

        return StartupNames.Quote + value + new string('\\', trailing) + StartupNames.Quote;
    }
}
