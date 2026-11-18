// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Xml;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Storage;

using TrackMirror.Diagnostics;

namespace TrackMirror.Monitoring;

public sealed class HubstaffActivityMonitor : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly string _sourceRoot;
    private readonly HubstaffActivityStore _store;
    private readonly System.Threading.Timer _timer;
    private FileSystemWatcher? watcher;
    private volatile bool restartWatcher;
    private bool disposed;
    private bool cacheWarningShown;
    private int scanning;

    public event Action<List<HubstaffActivity>>? Updated;

    public event Action<List<HubstaffActivity>>? HistoryUpdated;

    public event Action<string>? Warning;

    public HubstaffActivityMonitor(string sourceRoot, string dataRoot)
    {
        _sourceRoot = Path.GetFullPath(sourceRoot);

        _store = new HubstaffActivityStore(dataRoot);

        _timer = new System.Threading.Timer(_ => Scan(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Start()
    {
        HistoryUpdated?.Invoke(_store.Snapshot());

        Updated?.Invoke(_store.LatestSnapshot());

        if (_store.InitialWarning != null)
        {
            Warning?.Invoke(_store.InitialWarning);
        }

        _timer.Change(0, 1000);
    }

    private void Scan()
    {
        if (Interlocked.Exchange(ref scanning, 1) != 0)
            return;

        try
        {
            if (!Directory.Exists(_sourceRoot))
            {
                watcher?.Dispose();

                watcher = null;

                return;
            }

            if (watcher == null || restartWatcher)
            {
                watcher?.Dispose();

                watcher = new FileSystemWatcher(_sourceRoot, HubstaffActivityNames.SourceFile)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    InternalBufferSize = 16 * 1024
                };

                watcher.Created += (_, e) => ReadFile(e.FullPath);

                watcher.Changed += (_, e) => ReadFile(e.FullPath);

                watcher.Renamed += (_, e) => ReadFile(e.FullPath);

                watcher.Error += (_, _) => restartWatcher = true;

                restartWatcher = false;

                watcher.EnableRaisingEvents = true;
            }

            foreach (var directory in Directory.EnumerateDirectories(_sourceRoot))
            {
                ReadFile(Path.Combine(directory, HubstaffActivityNames.SourceFile));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppDiagnostics.Write(DiagnosticComponent.Activity, DiagnosticEvent.OperationFailed, ex);

            // The next scan retries folders that are temporarily unavailable.
        }
        finally
        {
            lock (_gate)
            {
                if (!disposed)
                {
                    SaveCache();
                }
            }

            Volatile.Write(ref scanning, 0);
        }
    }

    private void ReadFile(string path)
    {
        lock (_gate)
        {
            if (disposed)
                return;

            var relative = Path.GetRelativePath(_sourceRoot, path);

            var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (parts.Length != 2 || parts[0] is StorageNames.CurrentDirectory or StorageNames.ParentDirectory)
                return;

            try
            {
                var changed = _store.Merge(HubstaffActivityReader.Read(path, parts[0]));

                if (changed.Count == 0)
                    return;

                HistoryUpdated?.Invoke(changed);

                Updated?.Invoke(_store.LatestSnapshot());

                SaveCache();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
            {
                AppDiagnostics.Write(DiagnosticComponent.Activity, DiagnosticEvent.OperationFailed, ex);

                // Hubstaff can rewrite or clear a record while it is being read.
            }
        }
    }

    private void SaveCache()
    {
        try
        {
            var recovered = _store.RetryLoadHistory();

            if (recovered.Count > 0)
            {
                HistoryUpdated?.Invoke(recovered);

                Updated?.Invoke(_store.LatestSnapshot());
            }

            _store.Save();

            cacheWarningShown = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AppDiagnostics.Write(DiagnosticComponent.Activity, DiagnosticEvent.OperationFailed, ex);

            if (!cacheWarningShown)
            {
                cacheWarningShown = true;

                Warning?.Invoke(Messages.ActivityCacheFailedPrefix + ex.Message);
            }
        }
    }

    internal void Flush()
    {
        lock (_gate)
        {
            _store.Save();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _timer.DisposeAsync().ConfigureAwait(false);

        lock (_gate)
        {
            disposed = true;

            watcher?.Dispose();

            SaveCache();
        }
    }
}
