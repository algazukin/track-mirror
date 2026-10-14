// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Diagnostics;
using TrackMirror.Constants;

namespace TrackMirror.Hubstaff;

internal static class HubstaffCliLocator
{
    internal static string? Find()
    {
        foreach (var process in Process.GetProcessesByName(StartupNames.HubstaffProcessName))
        {
            using (process)
            {
                try
                {
                    var directory = Path.GetDirectoryName(process.MainModule?.FileName);

                    if (directory != null)
                    {
                        var path = Path.Combine(directory, HubstaffControlNames.CliExecutable);

                        if (File.Exists(path))
                            return path;
                    }
                }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException)
                {
                    // A process can exit or deny access while checking its installation directory.
                }
            }
        }

        foreach (var folder in new[] { Environment.SpecialFolder.ProgramFiles,
            Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.LocalApplicationData })
        {
            var root = Environment.GetFolderPath(folder);

            if (root.Length == 0)
                continue;

            var path = Path.Combine(root, HubstaffControlNames.InstallDirectory, HubstaffControlNames.CliExecutable);

            if (File.Exists(path))
                return path;
        }

        return null;
    }
}
