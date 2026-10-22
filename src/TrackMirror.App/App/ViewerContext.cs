// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using TrackMirror.Hubstaff;
using System.ComponentModel;
using System.Security;
using System.Runtime.InteropServices;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Monitoring;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Installation;
using TrackMirror.Privacy.Providers;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using TrackMirror.UI.Forms;
using TrackMirror.UI.Theme;
using TrackMirror.UI.Interop;

namespace TrackMirror.App;

public sealed class ViewerContext : ApplicationContext
{
    private readonly ScreenshotStore _store;
    private readonly SettingsStore _settings;
    private readonly HubstaffControlService _hubstaffControl;
    private readonly HubstaffStartupRegistration _startup;
    private ScreenshotMonitor? monitor;
    private HubstaffActivityMonitor? activityMonitor;
    private readonly string _sourceRoot;
    private readonly DataLocationStore _locations;
    private readonly ViewerDataOperations _dataOperations;
    private readonly System.Windows.Forms.Timer _retentionTimer = new() { Interval = 3600000 };
    private int monitorGeneration;
    private readonly PrivacyScanService _privacy;
    private readonly PrivacyModelInstaller _installer;
    private readonly HistoryForm _history;
    private readonly PreviewForm _preview;
    private readonly ScreenshotScheduleMonitor _screenshotSchedule;
    private readonly ScreenshotCountdownForm _countdown;
    private readonly System.Windows.Forms.Timer _countdownTimer = new() { Interval = 500 };
    private readonly NotifyIcon _tray;
    private readonly bool _noPreviews;
    private readonly DateTime _startedUtc;
    private bool exiting;
    private bool hasWarning;
    private bool settingsSaved = true;
    private List<string> reviewUndo = [];
    internal string? NextDataRoot { get; private set; }

    internal ScreenshotMonitor? RetainedMonitor => NextDataRoot != null ? monitor : null;

    internal DateTime SessionStartedUtc => _startedUtc;

    public ViewerContext(string sourceRoot, string dataRoot, bool background, bool noPreviews)
        : this(sourceRoot, dataRoot, background, noPreviews, new DataLocationStore(AppContext.BaseDirectory))
    {
    }

    internal ViewerContext(string sourceRoot, string dataRoot, bool background, bool noPreviews,
        DataLocationStore locations, bool showSettings = false, ScreenshotMonitor? retainedMonitor = null,
        DateTime? sessionStartedUtc = null, IAiPrivacyAnalyzer? analyzer = null,
        PrivacyModelInstaller? installer = null, HubstaffStartupRegistration? startup = null)
    {
        _sourceRoot = sourceRoot;

        _locations = locations;

        monitor = retainedMonitor;

        _startedUtc = sessionStartedUtc ?? DateTime.UtcNow;

        _noPreviews = noPreviews;

        _store = new ScreenshotStore(dataRoot);
        _settings = new SettingsStore(_store.DataRoot);
        _startup = startup ?? new HubstaffStartupRegistration(sourceRoot, _store.DataRoot);
        _history = new HistoryForm(_store, _settings.Current, noPreviews);
        // Create the UI handle before subscribing to background notifications.

        _ = _history.Handle;

        _preview = new PreviewForm(_store);
        _preview.SetNotificationTimeout(_settings.Current.NotificationTimeoutSeconds);

        _screenshotSchedule = new ScreenshotScheduleMonitor(sourceRoot);
        _countdown = new ScreenshotCountdownForm();

        _countdownTimer.Tick += (_, _) => UpdateCountdown();

        _countdown.OptionsChanged += value =>
        {
            _history.SetScreenshotCountdown(value);
            ApplySettings(_settings.Current with { ScreenshotCountdown = value });
        };

        _countdown.SettingsRequested += () =>
        {
            ShowHistory();
            _history.ShowSettings();
        };

        _privacy = new PrivacyScanService(_store, _settings.Current, analyzer);
        _installer = installer ?? new PrivacyModelInstaller();

        _hubstaffControl = new HubstaffControlService(_settings.Current.HubstaffControl);

        _tray = new NotifyIcon
        {
            Icon = ApplicationIcon.Main,
            Visible = true,
            Text = UiText.TrayWaiting,
            ContextMenuStrip = CreateTrayMenu()
        };

        _dataOperations = new ViewerDataOperations(_store, _settings, _privacy, _installer, _history, sourceRoot,
            PauseMonitorsAsync, ResumeDataWorkers, SwitchDataFolder, () => settingsSaved);

        ConnectEvents();

        UpdatePrivacy();

        _history.SetSettingsStatus(_settings.InitialWarning);

        if (_settings.InitialWarning == null)
        {
            UpdateStartup();
        }

        if (_store.InitialWarning != null)
        {
            ShowWarning(_store.InitialWarning);
        }

        if (!background)
        {
            MainForm = _history;

            _history.Show();
        }

        UpdateTray();

        StartMonitors();

        if (showSettings)
        {
            _history.ShowSettings();

            _history.SetDataStatus(DataManagementText.Completed);
        }

        _retentionTimer.Tick += (_, _) => RunAutoCleanup();

        _retentionTimer.Start();

        Post(RunAutoCleanup);
    }

    private ContextMenuStrip CreateTrayMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(UiText.TrayHistory, null, (_, _) => ShowHistory());

        menu.Items.Add(ActivityText.Tab, null, (_, _) =>
        {
            ShowHistory();

            _history.ShowActivity();
        });

        menu.Items.Add(SettingsText.SettingsTab, null, (_, _) =>
        {
            ShowHistory();

            _history.ShowSettings();
        });

        menu.Items.Add(UiText.CopiesFolder, null, (_, _) =>
        {
            Process.Start(new ProcessStartInfo(_store.CapturesRoot) { UseShellExecute = true });
        });

        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(UiText.Exit, null, (_, _) => ExitThread());

        return menu;
    }

    private void ConnectEvents()
    {
        _tray.DoubleClick += (_, _) => ShowHistory();

        _history.ReviewRequested += MarkViewed;

        _history.ReviewSelectionRequested += MarkViewedBatch;

        _history.UndoReviewRequested += UndoViewedBatch;

        _preview.ReviewRequested += MarkViewed;

        _history.FlagRequested += ToggleFlag;

        _preview.FlagRequested += ToggleFlag;

        _preview.OpenRequested += OpenPreviewCapture;

        _history.SettingsChanged += ApplySettings;

        _history.HubstaffControlRetryRequested += () =>
            _hubstaffControl.Apply(_settings.Current.HubstaffControl, retry: true);

        _hubstaffControl.Changed += () => Post(() =>
            _history.SetHubstaffControlStatus(_hubstaffControl.Snapshot()));

        _history.SetHubstaffControlStatus(_hubstaffControl.Snapshot());

        _history.FormClosing += (_, e) =>
        {
            if (e.CloseReason != CloseReason.UserClosing)
            {
                _installer.Cancel();
            }
        };

        _history.PrivacyScanRequested += (entries, force, categories) => _privacy.Enqueue(entries, force, categories);

        _history.PrivacyCancelRequested += _privacy.Cancel;

        _history.PrivacyDispositionRequested += _privacy.SetDisposition;

        _history.ModelInstallRequested += model =>
        {
            _installer.Start(PrivacyModelCatalog.Get(model));

            _history.SetModelInstallProgress(_installer.Snapshot());
        };

        _history.ModelInstallCancelRequested += _installer.Cancel;

        _history.ModelRemoveRequested += model =>
        {
            _installer.Remove(PrivacyModelCatalog.Get(model), _privacy.ReleaseLocalModelAsync,
                _privacy.ResumeModelChecks);

            _history.SetModelInstallProgress(_installer.Snapshot());
        };

        _installer.Changed += () => Post(() =>
        {
            _history.SetModelInstallProgress(_installer.Snapshot());

            UpdatePrivacy();
        });

        _privacy.Changed += () => Post(UpdatePrivacy);

        _store.Warning += message => Post(() => ShowWarning(message));

        _history.DataOperationRequested += kind => _dataOperations.Start(kind);

        _history.DataOperationCancelRequested += _dataOperations.Cancel;
    }

    private void StartMonitors()
    {
        var generation = ++monitorGeneration;

        var newMonitor = monitor == null;

        void PostMonitor(Action action) => Post(() =>
        {
            if (generation == monitorGeneration && !_dataOperations.Busy)
            {
                action();
            }
        });

        if (monitor == null)
        {
            monitor = new ScreenshotMonitor(_sourceRoot, _store);
        }
        else
        {
            monitor.Retarget(_store);
        }

        activityMonitor = new HubstaffActivityMonitor(_sourceRoot, _store.DataRoot);

        monitor.StatusChanged += message => PostMonitor(() => _history.SetStatus(message));

        monitor.Warning += message => PostMonitor(() => ShowWarning(message));

        monitor.CaptureSaved += (entry, notify) => PostMonitor(() => OnCaptureSaved(entry, notify));

        activityMonitor.Updated += activities => PostMonitor(() => _history.SetHubstaffActivities(activities));

        activityMonitor.HistoryUpdated += activities => PostMonitor(() => _history.AddHubstaffHistory(activities));

        activityMonitor.Warning += message => PostMonitor(() => ShowWarning(message));

        if (newMonitor)
        {
            monitor.Start();
        }
        else
        {
            monitor.Resume();
        }

        activityMonitor.Start();

        ApplyCountdown(_settings.Current.ScreenshotCountdown);
    }

    private async Task PauseMonitorsAsync()
    {
        ++monitorGeneration;

        _preview.Hide();
        _countdown.Hide();
        _countdownTimer.Stop();
        _screenshotSchedule.SetEnabled(false);

        if (monitor != null)
        {
            await monitor.PauseAsync();
        }

        var oldActivity = activityMonitor;

        activityMonitor = null;

        if (oldActivity != null)
        {
            await oldActivity.DisposeAsync();

            oldActivity.Flush();
        }
    }

    private void ResumeDataWorkers(bool reload)
    {
        if (reload)
        {
            _store.Reload();

            _privacy.ReloadResults();
        }

        _history.RefreshEntries();

        UpdatePrivacy();

        UpdateTray();

        _privacy.Resume();

        StartMonitors();
    }

    private void SwitchDataFolder(string target)
    {
        // The durable redirect was committed by the transfer before rebuilding the application context.
        NextDataRoot = target;

        try
        {
            _locations.RecordSelection(target);

            _startup.Apply(false);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
            or Win32Exception or InvalidOperationException or COMException)
        {
            _history.SetDataStatus(DataManagementText.FailedPrefix + ex.Message, error: true);
        }

        ExitThread();
    }

    private void RunAutoCleanup()
    {
        if (!exiting && settingsSaved && _settings.InitialWarning == null && _settings.Current.RetentionEnabled)
        {
            _dataOperations.Start(DataOperationKind.Cleanup, automatic: true);
        }
    }

    private void OnCaptureSaved(CaptureEntry entry, bool notify)
    {
        if (notify && entry.CapturedUtc >= _startedUtc && _settings.Current.PrivacyAutoScan
            && LocalModelRuntime.IsInstalled(_settings.Current.PrivacyModel))
        {
            _privacy.Enqueue([entry]);
        }

        _history.RefreshEntries();

        UpdateTray();

        if (!notify || _noPreviews || !_settings.Current.NotificationsEnabled)
            return;

        try
        {
            _preview.ShowCapture(entry);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or OutOfMemoryException)
        {
            ShowWarning(Messages.PreviewUnavailablePrefix + ex.Message);
        }
    }

    private void ApplySettings(AppSettings value)
    {
        if (!value.ScreenshotCountdown.Enabled)
            ApplyCountdown(value.ScreenshotCountdown);

        if (!value.HubstaffControl.Enabled)
        {
            // Opting out must stop live control even when persisting the settings fails.
            _hubstaffControl.Apply(value.HubstaffControl);
        }

        _preview.SetNotificationTimeout(value.NotificationTimeoutSeconds);

        if (!value.NotificationsEnabled)
        {
            _preview.Hide();
        }

        try
        {
            _settings.Save(value);

            settingsSaved = true;

            _hubstaffControl.Apply(value.HubstaffControl);

            ApplyCountdown(value.ScreenshotCountdown);

            _privacy.ApplySettings(value);

            UpdatePrivacy();

            _history.SetSettingsStatus(null);

            UpdateStartup();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            settingsSaved = false;

            _history.SetSettingsStatus(SettingsText.SaveFailedPrefix + ex.Message);
        }
    }

    private void UpdateStartup()
    {
        try
        {
            _startup.Apply(_settings.Current.StartWithHubstaff);

            if (_settings.Current.StartWithHubstaff)
            {
                _startup.StartWatcher();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException
            or Win32Exception or InvalidOperationException or COMException)
        {
            _history.SetSettingsStatus(SettingsText.StartupFailedPrefix + ex.Message);
        }
    }

    private void ApplyCountdown(ScreenshotCountdownOptions options)
    {
        _countdown.ApplyOptions(options);

        _screenshotSchedule.SetEnabled(options.Enabled && !_dataOperations.Busy);
        _countdownTimer.Enabled = options.Enabled;

        UpdateCountdown();
    }

    private void UpdateCountdown()
    {
        if (exiting || !_countdownTimer.Enabled || !_settings.Current.ScreenshotCountdown.Enabled || _dataOperations.Busy)
        {
            _countdown.Hide();

            return;
        }

        var snapshot = _screenshotSchedule.Snapshot(DateTimeOffset.UtcNow);

        _countdown.UpdateState(snapshot);

        if (snapshot.Status is ScreenshotScheduleStatus.Stopped or ScreenshotScheduleStatus.Disabled)
        {
            _countdown.Hide();
        }
        else if (!_countdown.Visible)
        {
            _countdown.Show();
            WindowPosition.KeepAboveWithoutActivation(_countdown.Handle);
        }
    }

    private void UpdatePrivacy() =>
        _history.SetPrivacyResults(_privacy.Snapshot(), _settings.Current, _privacy.Warning);

    private void Post(Action action)
    {
        if (exiting || _history.IsDisposed)
            return;

        try
        {
            _history.BeginInvoke(new Action(() =>
            {
                if (!exiting)
                {
                    action();
                }
            }));
        }
        catch (InvalidOperationException) when (exiting || _history.IsDisposed)
        {
        }
    }

    private void MarkViewed(string id)
    {
        reviewUndo.Clear();

        _history.ClearReviewUndo();

        _store.MarkViewed(id);

        _history.RefreshEntries();

        _preview.UpdateCaption();

        UpdateTray();
    }

    private void MarkViewedBatch(IReadOnlyList<string> ids)
    {
        try
        {
            reviewUndo = _store.SetViewed(ids, true);

            _history.ShowReviewUndo(reviewUndo.Count);

            _history.RefreshEntries();

            _preview.UpdateCaption();

            UpdateTray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowWarning(DatabaseText.WriteFailed);
        }
    }

    private void UndoViewedBatch()
    {
        try
        {
            _store.SetViewed(reviewUndo, false);

            reviewUndo.Clear();

            _history.ClearReviewUndo();

            _history.RefreshEntries();

            _history.SetStatus(GalleryText.UndoCompleted);

            _preview.UpdateCaption();

            UpdateTray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowWarning(DatabaseText.WriteFailed);
        }
    }

    private void OpenPreviewCapture(string id)
    {
        _preview.Hide();
        // Finish the preview click before opening and activating history.

        Post(() =>
        {
            ShowHistory(id);

            MarkViewed(id);
        });
    }

    private void ToggleFlag(string id)
    {
        _store.ToggleFlag(id);

        _history.RefreshEntries();

        _preview.UpdateCaption();

        UpdateTray();
    }

    private void UpdateTray()
    {
        var unread = _store.Snapshot().Count(entry => !entry.Viewed);

        var format = hasWarning ? UiText.TrayWarningFormat : UiText.TrayUnreadFormat;

        _tray.Text = string.Format(format, unread);
    }

    private void ShowWarning(string message)
    {
        _history.SetWarning(message);

        if (!hasWarning)
        {
            var previewText = message[..Math.Min(250, message.Length)];

            _tray.ShowBalloonTip(5000, UiText.WarningTitle, previewText, ToolTipIcon.Warning);
        }

        hasWarning = true;

        UpdateTray();
    }

    private void ShowHistory(string? id = null)
    {
        _history.ShowHistory(_dataOperations.Busy ? null : id);

        if (_dataOperations.Busy)
        {
            _history.ShowSettings();
        }

        hasWarning = false;

        UpdateTray();
    }

    protected override async void ExitThreadCore()
    {
        if (exiting)
        {
            return;
        }

        exiting = true;

        _installer.Cancel();
        _tray.Visible = false;
        _history.Enabled = false;
        _preview.Hide();
        _retentionTimer.Stop();
        _retentionTimer.Dispose();
        _countdown.Hide();
        _countdownTimer.Stop();
        _countdownTimer.Dispose();
        await _screenshotSchedule.DisposeAsync();
        _countdown.Dispose();

        // Keep the UI message loop running while owned operations finish cancellation.
        await _hubstaffControl.DisposeAsync();

        await _dataOperations.DisposeAsync();

        if (NextDataRoot == null && monitor != null)
        {
            await monitor.DisposeAsync();
        }

        if (activityMonitor != null)
        {
            await activityMonitor.DisposeAsync();
        }

        await _installer.DisposeAsync();

        await _privacy.DisposeAsync();

        _preview.Dispose();
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();
        _history.AllowClose = true;
        _history.Dispose();

        base.ExitThreadCore();
    }
}
