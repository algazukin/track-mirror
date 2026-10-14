// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.App;
using TrackMirror.Constants;
using TrackMirror.Storage.Management;
using TrackMirror.Monitoring;
using TrackMirror.Localization;
using TrackMirror.Storage;

namespace TrackMirror;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        UiLocalization.Initialize(LocalizationNames.English);

        var source = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            ApplicationConstants.HubstaffDirectory,
            StorageNames.DataDirectory,
            ApplicationConstants.HubstaffServerDirectory
        );

        var data = Path.Combine(AppContext.BaseDirectory, StorageNames.DataDirectory);

        string? explicitData = null;

        var background = false;

        var noPreviews = false;

        var watchHubstaff = false;

        var quiet = false;

        try
        {
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case ApplicationConstants.SourceRootOption when i + 1 < args.Length:
                        source = Path.GetFullPath(args[++i]);
                        break;
                    case ApplicationConstants.DataRootOption when i + 1 < args.Length:
                        explicitData = Path.GetFullPath(args[++i]);
                        break;
                    case ApplicationConstants.BackgroundOption:
                        background = true;
                        break;
                    case ApplicationConstants.NoPreviewsOption:
                        noPreviews = true;
                        break;
                    case StartupNames.WatchOption:
                        watchHubstaff = true;
                        quiet = true;
                        break;
                    case StartupNames.LaunchOption:
                        background = true;
                        quiet = true;
                        break;
                    default:
                        throw new ArgumentException(Messages.InvalidOptionPrefix + args[i]);
                }
            }

            var locations = new DataLocationStore(AppContext.BaseDirectory);

            data = locations.Resolve(explicitData);

            UiLocalization.Initialize(new SettingsStore(data).Current.Language);

            if (watchHubstaff)
            {
                HubstaffStartWatcher.RunAsync(source, data).GetAwaiter().GetResult();

                return;
            }

            ApplicationConfiguration.Initialize();

            var showSettings = false;

            ScreenshotMonitor? retainedMonitor = null;

            DateTime? sessionStartedUtc = null;

            while (true)
            {
                // A restored or moved dataset may have a different language preference.
                UiLocalization.Initialize(new SettingsStore(data).Current.Language);

                var identifier = SingleInstance.Identifier(data);

                using var singleInstance = new Mutex(true, ApplicationConstants.MutexPrefix + identifier,
                    out var created);

                if (!created)
                {
                    if (quiet)
                        return;

                    MessageBox.Show(Messages.AlreadyRunning, ApplicationConstants.DisplayName,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

                    return;
                }

                try
                {
                    using var context = new ViewerContext(source, data, background, noPreviews, locations, showSettings,
                        retainedMonitor, sessionStartedUtc);

                    Application.Run(context);

                    if (context.NextDataRoot == null)
                        return;

                    data = context.NextDataRoot;

                    retainedMonitor = context.RetainedMonitor;

                    sessionStartedUtc = context.SessionStartedUtc;

                    background = false;

                    showSettings = true;
                }
                finally
                {
                    singleInstance.ReleaseMutex();
                }
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (Directory.Exists(data))
                {
                    File.WriteAllText(Path.Combine(data, StorageNames.StartupErrorFile), ex.ToString());
                }
            }
            catch (Exception loggingError) when (loggingError is IOException
                or UnauthorizedAccessException
                or ArgumentException)
            {
            }

            if (quiet)
                return;

            MessageBox.Show(
                Messages.StartupFailedPrefix + ex.Message,
                ApplicationConstants.DisplayName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }
}
