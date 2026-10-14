// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Diagnostics;
using TrackMirror.Constants;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;

namespace TrackMirror.App;

internal static class HubstaffStartWatcher
{
    internal static async Task RunAsync(string sourceRoot, string dataRoot,
        CancellationToken cancellationToken = default)
    {
        var identifier = SingleInstance.Identifier(dataRoot);

        using var instance = new Mutex(false, StartupNames.WatcherMutexPrefix + identifier, out var created);

        if (!created)
        {
            return;
        }

        var startup = new HubstaffStartupRegistration(sourceRoot, dataRoot);

        var previous = new HashSet<int>();

        var retryAfter = DateTime.MinValue;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (!string.Equals(DataLocationStore.ResolveRedirects(dataRoot), dataRoot,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // A transfer destination must be available before an observer launches a viewer.
                return;
            }

            var settings = new SettingsStore(dataRoot);

            if (settings.InitialWarning == null && !settings.Current.StartWithHubstaff)
            {
                return;
            }

            if (settings.InitialWarning == null)
            {
                var processes = GetHubstaffProcesses();

                var newRun = IsNewRun(previous, processes);

                if (newRun && DateTime.UtcNow >= retryAfter)
                {
                    try
                    {
                        if (!SingleInstance.Exists(ApplicationConstants.MutexPrefix + identifier))
                        {
                            startup.StartViewer();
                        }

                        previous = processes;
                    }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
                    {
                        retryAfter = DateTime.UtcNow.AddSeconds(StartupNames.RetrySeconds);
                    }
                }
                else if (!newRun)
                {
                    previous = processes;

                    if (processes.Count == 0)
                    {
                        retryAfter = DateTime.MinValue;
                    }
                }
            }

            await Task.Delay(StartupNames.PollMilliseconds, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static bool IsNewRun(IReadOnlySet<int> previous, IReadOnlySet<int> current) =>
        current.Count > 0 && !current.Overlaps(previous);

    private static HashSet<int> GetHubstaffProcesses()
    {
        using var current = Process.GetCurrentProcess();

        var session = current.SessionId;

        var found = new HashSet<int>();

        foreach (var process in Process.GetProcessesByName(StartupNames.HubstaffProcessName))
        {
            using (process)
            {
                try
                {
                    if (process.SessionId == session)
                    {
                        found.Add(process.Id);
                    }
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
                {
                    // Hubstaff may exit while its process metadata is being read.
                }
            }
        }

        return found;
    }
}
