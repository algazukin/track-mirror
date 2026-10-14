// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Installation;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using TrackMirror.UI.Forms;

namespace TrackMirror.App;

internal sealed class ViewerDataOperations : IAsyncDisposable
{
    private readonly ScreenshotStore _store;
    private readonly SettingsStore _settings;
    private readonly PrivacyScanService _privacy;
    private readonly PrivacyModelInstaller _installer;
    private readonly HistoryForm _history;
    private readonly string _sourceRoot;
    private readonly Func<Task> _pauseMonitors;
    private readonly Action<bool> _resumeWorkers;
    private readonly Action<string> _switchFolder;
    private readonly Func<bool> _settingsSaved;
    private Task operation = Task.CompletedTask;
    private CancellationTokenSource? cancellation;
    private bool stopping;

    internal bool Busy => !operation.IsCompleted;

    internal ViewerDataOperations(ScreenshotStore store, SettingsStore settings, PrivacyScanService privacy,
        PrivacyModelInstaller installer, HistoryForm history, string sourceRoot, Func<Task> pauseMonitors,
        Action<bool> resumeWorkers, Action<string> switchFolder, Func<bool> settingsSaved)
    {
        _store = store;

        _settings = settings;

        _privacy = privacy;

        _installer = installer;

        _history = history;

        _sourceRoot = sourceRoot;

        _pauseMonitors = pauseMonitors;

        _resumeWorkers = resumeWorkers;

        _switchFolder = switchFolder;

        _settingsSaved = settingsSaved;
    }

    internal void Start(DataOperationKind kind, bool automatic = false)
    {
        if (stopping || Busy || _installer.Snapshot().Busy)
        {
            if (!automatic)
            {
                _history.SetDataStatus(DataManagementText.Busy, error: true);
            }

            return;
        }

        if (!_settingsSaved())
        {
            _history.SetDataStatus(DataManagementText.Unsaved, error: true);

            return;
        }

        string? target = null;

        string? archive = null;

        try
        {
            if (kind == DataOperationKind.Cleanup)
            {
                var retention = new CaptureRetention(_store.DataRoot);

                var candidates = retention.Candidates(_settings.Current, DateTime.UtcNow);

                if (candidates.Count == 0 && retention.PendingFiles() == 0)
                {
                    if (!automatic)
                    {
                        _history.SetDataStatus(DataManagementText.NothingToClean);
                    }

                    return;
                }

                if (!automatic && candidates.Count > 0 && MessageBox.Show(_history,
                    string.Format(DataManagementText.CleanupConfirmation, candidates.Count),
                    DataManagementText.Heading, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK)
                    return;
            }
            else if (kind == DataOperationKind.Backup)
            {
                using var dialog = new SaveFileDialog
                {
                    Filter = DataManagementText.ArchiveFilter,
                    FileName = string.Format(DataManagementNames.BackupNameFormat, DateTime.Now),
                    OverwritePrompt = true,
                    AddExtension = true
                };

                if (dialog.ShowDialog(_history) != DialogResult.OK)
                    return;

                archive = dialog.FileName;

                ValidateExternalPath(archive);
            }
            else
            {
                if (kind == DataOperationKind.Restore)
                {
                    using var dialog = new OpenFileDialog { Filter = DataManagementText.ArchiveFilter };

                    if (dialog.ShowDialog(_history) != DialogResult.OK)
                        return;

                    archive = dialog.FileName;
                }

                using var folder = new FolderBrowserDialog
                {
                    Description = DataManagementText.SelectTarget,
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true
                };

                if (folder.ShowDialog(_history) != DialogResult.OK)
                    return;

                target = DataFolderRules.Canonical(folder.SelectedPath);

                ValidateExternalPath(target);

                DataFolderRules.EmptyTarget(_store.DataRoot, target);

                if (SingleInstance.Exists(ApplicationConstants.MutexPrefix + SingleInstance.Identifier(target)))
                    throw new IOException(DataManagementText.SelectedFolderBusy);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _history.SetDataStatus(DataManagementText.FailedPrefix + ex.Message, error: true);

            return;
        }

        cancellation = new CancellationTokenSource();

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Publish the task before RunAsync can request application shutdown after a folder switch.
        operation = completion.Task;

        _history.SetDataBusy(true, showSettings: !automatic);

        _ = RunAsync(kind, target, archive, cancellation.Token, completion);
    }

    private void ValidateExternalPath(string path)
    {
        if (DataFolderRules.Overlaps(_sourceRoot, path)
            || DataFolderRules.Overlaps(AppContext.BaseDirectory, path))
            throw new IOException(DataManagementText.ProtectedFolder);
    }

    private async Task RunAsync(DataOperationKind kind, string? target, string? archive, CancellationToken token,
        TaskCompletionSource completion)
    {
        var cleanupStarted = false;

        try
        {
            await _pauseMonitors();

            await _privacy.PauseAsync(token);

            _store.FlushPending();

            token.ThrowIfCancellationRequested();

            var progress = new Progress<DataOperationProgress>(value =>
            {
                if (!stopping && !completion.Task.IsCompleted)
                {
                    _history.SetDataProgress(value);
                }
            });

            switch (kind)
            {
                case DataOperationKind.Move:
                    _switchFolder(await Task.Run(() =>
                        DataFolderTransfer.MoveAsync(_store.DataRoot, target!, token, progress), token));
                    break;
                case DataOperationKind.Backup:
                    await Task.Run(() => DataArchive.CreateBackupAsync(_store.DataRoot, archive!, token, progress),
                        token);

                    _history.SetDataStatus(DataManagementText.Completed);
                    break;
                case DataOperationKind.Restore:
                    _switchFolder(await Task.Run(() =>
                        DataArchive.RestoreAsync(_store.DataRoot, archive!, target!, token, progress), token));
                    break;
                case DataOperationKind.Cleanup:
                    cleanupStarted = true;

                    _history.SetDataProgress(new DataOperationProgress(DataManagementText.Cleaning));

                    var result = await new CaptureRetention(_store.DataRoot)
                        .PurgeAsync(_settings.Current, DateTime.UtcNow, token);

                    _history.SetDataStatus(string.Format(DataManagementText.CleanupFormat,
                        result.Removed, result.PendingFiles));
                    break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!stopping)
            {
                _history.SetDataStatus(cleanupStarted
                    ? DataManagementText.CleanupCancelled : DataManagementText.Cancelled);
            }
        }
        catch (Exception ex)
        {
            if (!stopping)
            {
                var message = ex is JsonException or InvalidDataException or OverflowException
                    ? DataManagementText.InvalidArchive : ex.Message;

                _history.SetDataStatus(DataManagementText.FailedPrefix + message, error: true);
            }
        }
        finally
        {
            if (!stopping)
            {
                try
                {
                    _resumeWorkers(cleanupStarted);
                }
                catch (Exception)
                {
                    _history.SetDataStatus(DataManagementText.Unsaved, error: true);
                }

                _history.SetDataBusy(false);
            }

            cancellation?.Dispose();

            cancellation = null;

            completion.TrySetResult();
        }
    }

    internal void Cancel() => cancellation?.Cancel();

    public async ValueTask DisposeAsync()
    {
        stopping = true;

        Cancel();

        await operation;
    }
}
