// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.App;

using TrackMirror.Constants;

using TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

using TrackMirror.Localization;

using TrackMirror.Models;

using TrackMirror.Privacy;

using TrackMirror.Privacy.Installation;

using TrackMirror.Storage;

using TrackMirror.Storage.Management;

using TrackMirror.UI.Controls;
using TrackMirror.UI.Interop;

using TrackMirror.UI.Forms;

using Xunit;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class UiScenarioHost
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length != 5 || args[0] != "--ui-scenario")
                throw new ArgumentException("Use the xUnit runner or the explicit synthetic UI scenario arguments.");

            var root = Path.GetFullPath(args[2]);

            var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "TrackMirror.Tests"))
                + Path.DirectorySeparatorChar;

            if (!root.StartsWith(expected, StringComparison.OrdinalIgnoreCase) || !Directory.Exists(root))
                throw new ArgumentException("The UI fixture must belong to the test workspace.");

            using var watchdog = new System.Threading.Timer(_ =>
            {
                Console.Error.WriteLine("The synthetic UI process exceeded its shutdown deadline.");

                Environment.Exit(2);
            }, null, TimeSpan.FromSeconds(20), Timeout.InfiniteTimeSpan);

            Application.SetHighDpiMode(args[4] == "96" ? HighDpiMode.DpiUnaware : HighDpiMode.PerMonitorV2);

            Application.EnableVisualStyles();

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

            UiLocalization.Initialize(args[3]);

            Run(root, args[1]);

            return 0;
        }
        catch (Exception error)
        {
            // These diagnostics contain only synthetic fixture values, never a user's data or model output.
            Console.Error.WriteLine(error);

            return 1;
        }
    }

    private static void Run(string root, string scenario)
    {
        // Arrange
        var source = Path.Combine(root, "empty-source");

        var application = Path.Combine(root, "application");

        Directory.CreateDirectory(source);

        Directory.CreateDirectory(application);

        var settings = new AppSettings { PrivacyGames = true, PrivacyConfidentialData = false };

        new SettingsStore(root).Save(settings);

        var store = new ScreenshotStore(root);

        var directory = Path.Combine(store.CapturesRoot, "fixture");

        Directory.CreateDirectory(directory);

        using (var image = new Bitmap(160, 100))
        {
            image.Save(Path.Combine(directory, "2026-10-13T103000-0.jpg"), TestImages.JpegEncoder, null);
        }

        var entry = store.Add("fixture", "2026-10-13T103000-0.jpg", DateTime.UtcNow, true);

        if (scenario == "batch")
        {
            store.ToggleFlag(entry.Id);

            var selected = AddCapture(store, "selected.jpg");

            store.ToggleFlag(selected.Id);

            var reviewed = AddCapture(store, "reviewed.jpg");

            store.MarkViewed(reviewed.Id);

            AddCapture(store, "outside.jpg");
        }

        var gate = new CancellationGate();

        using var http = new HttpClient(new UiBlockingDownloadHandler(gate));

        using var bootstrap = new PrivacyRuntimeInstaller(client: http);

        var installer = new PrivacyModelInstaller(Path.Combine(root, "runtime"), bootstrap, http);

        var startup = new HubstaffStartupRegistration(source, root,
            startupFolder: Path.Combine(root, "startup"));

        using var context = new ViewerContext(source, root, true, false,
            new DataLocationStore(application, Path.Combine(root, "profile")),
            analyzer: new UiBlockingAnalyzer(gate), installer: installer, startup: startup);

        var history = UiControls.Field<HistoryForm>(context, "_history");

        var preview = UiControls.Field<PreviewForm>(context, "_preview");

        var tray = UiControls.Field<NotifyIcon>(context, "_tray");

        history.Opacity = preview.Opacity = 0;

        history.ShowInTaskbar = false;

        if (scenario != "preview-startup")
        {
            context.MainForm = history;
        }

        Form[] owned = [];

        Exception? failure = null;

        // Act
        history.BeginInvoke(new Action(async () =>
        {
            try
            {
                await ExerciseAsync(context, history, preview, store, entry, gate, installer, scenario);

                owned = history.OwnedForms;
            }
            catch (Exception error)
            {
                failure = error;
            }
            finally
            {
                var exit = tray.ContextMenuStrip!.Items.OfType<ToolStripMenuItem>()
                    .Single(item => item.Text == UiText.Exit);

                exit.PerformClick();
            }
        }));

        Application.Run(context);

        // Assert
        Assert.Null(failure);

        Assert.True(history.IsDisposed);

        Assert.True(preview.IsDisposed);

        Assert.All(owned, window => Assert.True(window.IsDisposed));

        if (scenario is "shutdown-scan" or "shutdown-download")
        {
            Assert.True(gate.Cancelled.Task.IsCompletedSuccessfully);

            Assert.True(gate.Released.Task.IsCompletedSuccessfully);
        }

        Console.WriteLine($"PASS {scenario}; DPI={history.DeviceDpi}");
    }

    private static async Task ExerciseAsync(ViewerContext context, HistoryForm history, PreviewForm preview,
        ScreenshotStore store, CaptureEntry entry, CancellationGate gate, PrivacyModelInstaller installer,
        string scenario)
    {
        switch (scenario)
        {
            case "countdown-settings":
                // Arrange
                var overlay = UiControls.Field<ScreenshotCountdownForm>(context, "_countdown");
                var preferences = UiControls.Field<SettingsStore>(context, "_settings");
                var initial = new ScreenshotCountdownOptions
                {
                    Enabled = true, Mode = ScreenshotCountdownMode.Detailed,
                    MiddleButtonDrag = true, Left = 80, Top = 80
                };

                foreach (var next in new[]
                {
                    initial with { Enabled = false }, initial with { Mode = ScreenshotCountdownMode.Default },
                    initial with { Mode = ScreenshotCountdownMode.BeforeScreenshot }
                })
                {
                    preferences.Save(preferences.Current with { ScreenshotCountdown = initial });
                    overlay.ApplyOptions(initial);

                    if (next.Mode == ScreenshotCountdownMode.BeforeScreenshot)
                    {
                        overlay.Show();
                        UiControls.Field<MiddleButtonDragListener?>(overlay, "middleListener")?.SetTarget(Rectangle.Empty);
                        UiControls.Invoke(overlay, "QueueMiddleDrag", new Point(200, 100), false);
                        await UiControls.NextTurn(history);
                    }

                    UiControls.Invoke(overlay, "QueueMiddleDrag", new Point(320, 200), false);

                    // Act
                    UiControls.Invoke(context, "ApplySettings", preferences.Current with { ScreenshotCountdown = next });
                    await UiControls.NextTurn(history);
                    var restored = new SettingsStore(store.DataRoot).Current.ScreenshotCountdown;

                    // Assert
                    Assert.Equal(320, restored.Left);
                    Assert.Equal(200, restored.Top);
                    Assert.Equal(next.Enabled, restored.Enabled);
                    Assert.Equal(next.Mode, restored.Mode);
                    Assert.Equal(new Point(320, 200), overlay.Location);
                    Assert.False(overlay.Visible);
                }
                break;
            case "shutdown":
                TestWindow.Show(history);
                break;
            case "shutdown-scan":
                TestWindow.Show(history);

                UiControls.Field<PrivacyScanService>(context, "_privacy").Enqueue([entry]);

                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

                _ = ReleaseAfterCancellationAsync(gate, history);
                break;
            case "shutdown-download":
                TestWindow.Show(history);

                installer.Start(PrivacyModelCatalog.Get(new AppSettings().PrivacyModel));

                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.True(installer.Snapshot().Busy);

                _ = ReleaseAfterCancellationAsync(gate, history);
                break;
            case "batch":
                TestWindow.Show(history);

                var captures = UiControls.Field<ScreenshotStore>(context, "_store");

                UiControls.Field<CaptureFilterSelector>(history, "_captureFilter").SetFilter(CaptureFilter.Flagged);

                UiControls.Field<CaptureGallery>(history, "_gallery").SelectAll(true);

                var actions = UiControls.Field<GalleryActionBar>(history, "_galleryActions");

                UiControls.Field<Button>(actions, "_read").PerformClick();

                Assert.All(captures.Snapshot().Where(item => item.Flagged), item => Assert.True(item.Viewed));

                Assert.False(captures.Snapshot().Single(item => item.FileName == "outside.jpg").Viewed);

                Assert.True(captures.Snapshot().Single(item => item.FileName == "reviewed.jpg").Viewed);

                var later = AddCapture(captures, "later.jpg");

                history.RefreshEntries();

                UiControls.Field<Button>(actions, "_undo").PerformClick();

                Assert.All(captures.Snapshot().Where(item => item.Flagged), item => Assert.False(item.Viewed));

                Assert.Equal(2, captures.Snapshot().Count(item => item.Flagged));

                Assert.Single(captures.Snapshot(), item => item.Viewed);

                Assert.False(captures.Snapshot().Single(item => item.Id == later.Id).Viewed);
                break;
            case "undo-expiry":
                TestWindow.Show(history);

                var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

                var bar = UiControls.Field<GalleryActionBar>(history, "_galleryActions");

                var timer = UiControls.Field<System.Windows.Forms.Timer>(bar, "_undoTimer");

                Assert.Equal(10000, timer.Interval);

                timer.Interval = 40;

                var tick = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                timer.Tick += (_, _) => tick.TrySetResult();

                gallery.SelectAll(true);

                UiControls.Field<Button>(bar, "_read").PerformClick();

                Assert.True(Assert.Single(UiControls.Field<ScreenshotStore>(context, "_store").Snapshot()).Viewed);

                await tick.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.False(UiControls.Field<Button>(bar, "_undo").Visible);
                break;
            case "notification-replacement":
                var expiry = UiControls.Field<System.Windows.Forms.Timer>(preview, "_dismissTimer");

                expiry.Interval = 3000;

                preview.ShowCapture(entry);

                var firstImage = UiControls.Field<PictureBox>(preview, "_picture").Image;

                using (var replacement = new System.Windows.Forms.Timer { Interval = 1000 })
                {
                    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                    replacement.Tick += (_, _) => ready.TrySetResult();

                    replacement.Start();

                    await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

                    replacement.Stop();
                }
                Assert.True(preview.Visible);

                var second = AddCapture(store, "replacement.jpg", 320);

                var dismissed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                expiry.Tick += (_, _) => dismissed.TrySetResult();

                var replacedAt = System.Diagnostics.Stopwatch.GetTimestamp();

                preview.ShowCapture(second);

                Assert.NotSame(firstImage, UiControls.Field<PictureBox>(preview, "_picture").Image);

                Assert.Equal(320, UiControls.Field<PictureBox>(preview, "_picture").Image!.Width);

                await dismissed.Task.WaitAsync(TimeSpan.FromSeconds(8));

                Assert.True(System.Diagnostics.Stopwatch.GetElapsedTime(replacedAt)
                    >= TimeSpan.FromMilliseconds(2500), "The replacement used the first notification's deadline.");

                Assert.False(preview.Visible);
                break;
            case "notification-timeout":
                preview.ShowCapture(entry);

                var dismiss = UiControls.Field<System.Windows.Forms.Timer>(preview, "_dismissTimer");

                dismiss.Interval = 40;

                var hidden = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                preview.VisibleChanged += (_, _) =>
                {
                    if (!preview.Visible)
                    {
                        hidden.TrySetResult();
                    }
                };

                await hidden.Task.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.False(dismiss.Enabled);

                Assert.False(Assert.Single(store.Snapshot()).Viewed);
                break;
            case "notifications-disabled":
                var settingsPanel = UiControls.Field<SettingsPanel>(history, "_settingsPage");

                UiControls.Field<CheckBox>(settingsPanel, "_notifications").Checked = false;

                Assert.True(UiControls.Field<bool>(context, "settingsSaved"),
                    UiControls.Field<Label>(settingsPanel, "_saveStatus").Text);

                Assert.False(UiControls.Field<SettingsStore>(context, "_settings").Current.NotificationsEnabled);

                UiControls.Invoke(context, "OnCaptureSaved", entry, true);

                Assert.False(preview.Visible);

                Assert.Single(store.Snapshot());
                break;
            case "layout":
                TestWindow.Show(history);

                UiLayoutAssertions.Check(history);
                break;
            case "preview-startup":
            case "preview-hidden":
            case "preview-minimized":
            case "focus":
            case "window-reuse":
            case "shutdown-windows":
                await InteractiveWindowScenarios.RunAsync(history, preview,
                    UiControls.Field<ScreenshotStore>(context, "_store"), entry, scenario);
                break;
            default:
                throw new ArgumentException("Unknown synthetic UI scenario.");
        }
    }

    private static async Task ReleaseAfterCancellationAsync(CancellationGate gate, Control control)
    {
        await gate.Cancelled.Task;

        control.BeginInvoke(new Action(() => gate.Released.TrySetResult()));
    }

    private static CaptureEntry AddCapture(ScreenshotStore store, string name, int width = 160)
    {
        using var image = new Bitmap(width, 100);

        image.Save(Path.Combine(store.CapturesRoot, "fixture", name), TestImages.JpegEncoder, null);

        return store.Add("fixture", name, DateTime.UtcNow, true);
    }
}
