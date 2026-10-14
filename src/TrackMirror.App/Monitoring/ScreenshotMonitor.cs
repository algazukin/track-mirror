// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Collections.Concurrent;
using System.Threading.Channels;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Storage;

namespace TrackMirror.Monitoring;

public sealed class ScreenshotMonitor : IAsyncDisposable
{
    private ScreenshotStore store;
    private readonly SemaphoreSlim _writes = new(1, 1);
    private bool paused;
    private long pausedFromTicks = long.MaxValue;
    private readonly string _sourceRoot;
    private readonly CancellationTokenSource _stopping = new();
    private readonly ConcurrentDictionary<string, byte> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _retryAfter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Channel<CaptureJob> _jobs = Channel.CreateUnbounded<CaptureJob>(
        new UnboundedChannelOptions { SingleReader = true }
    );
    private readonly System.Threading.Timer _scanTimer;
    private readonly Task _worker;
    private FileSystemWatcher? watcher;
    private int scanning;
    private int scanCount;
    private volatile bool restartWatcher;
    private string lastStatus = string.Empty;
    public event Action<CaptureEntry, bool>? CaptureSaved;
    public event Action<string>? StatusChanged;
    public event Action<string>? Warning;

    internal event Action<string>? CopyRetrying;

    public ScreenshotMonitor(string sourceRoot, ScreenshotStore store)
    {
        _sourceRoot = Path.GetFullPath(sourceRoot);

        this.store = store;

        _worker = Task.Run(ProcessJobs);

        _scanTimer = new System.Threading.Timer(
            _ => Scan(Interlocked.Increment(ref scanCount) == 1),
            null,
            Timeout.Infinite,
            Timeout.Infinite
        );
    }

    public void Start()
    {
        _scanTimer.Change(0, 1000);
    }

    internal async Task PauseAsync()
    {
        if (paused)
            return;

        Volatile.Write(ref pausedFromTicks, DateTime.UtcNow.Ticks);

        // Keep watching and holding input handles while storage writes are suspended.
        await _writes.WaitAsync();

        paused = true;
    }

    internal void Retarget(ScreenshotStore target)
    {
        if (!paused)
            throw new InvalidOperationException(DataManagementText.Busy);

        store = target;

        CaptureSaved = null;

        StatusChanged = null;

        Warning = null;

        lastStatus = string.Empty;
    }

    internal void Resume()
    {
        if (!paused)
            return;

        paused = false;

        _writes.Release();
    }

    private void Scan(bool initial)
    {
        if (_stopping.IsCancellationRequested || Interlocked.Exchange(ref scanning, 1) != 0)
            return;

        try
        {
            if (!Directory.Exists(_sourceRoot))
            {
                watcher?.Dispose();

                watcher = null;

                SetStatus(Messages.WaitingForSource);

                return;
            }

            if (watcher == null || restartWatcher)
            {
                watcher?.Dispose();

                watcher = new FileSystemWatcher(_sourceRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName
                        | NotifyFilters.DirectoryName
                        | NotifyFilters.LastWrite
                        | NotifyFilters.Size,
                    InternalBufferSize = 64 * 1024
                };

                watcher.Created += (_, e) => QueueFile(e.FullPath, true);

                watcher.Changed += (_, e) => QueueFile(e.FullPath, true);

                watcher.Renamed += (_, e) => QueueFile(e.FullPath, true);

                watcher.Error += (_, _) =>
                {
                    restartWatcher = true;

                    SetStatus(Messages.RestoringMonitor);
                };

                restartWatcher = false;

                watcher.EnableRaisingEvents = true;
            }

            var accountCount = 0;

            foreach (var directory in Directory.EnumerateDirectories(_sourceRoot))
            {
                var screens = Path.Combine(directory, StorageNames.ScreensDirectory);

                if (!Directory.Exists(screens))
                    continue;

                accountCount++;

                try
                {
                    foreach (var file in Directory.EnumerateFiles(screens))
                    {
                        QueueFile(file, !initial);
                    }
                }
                catch (IOException)
                { /* Hubstaff can remove a directory while it is being enumerated. */
                }
            }

            SetStatus(
                accountCount == 0
                    ? Messages.WaitingForScreens
                    : string.Format(Messages.MonitoringAccountsFormat, accountCount)
            );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SetStatus(Messages.SourceUnavailablePrefix + ex.Message);
        }
        finally
        {
            Volatile.Write(ref scanning, 0);
        }
    }

    private void QueueFile(string path, bool notify)
    {
        if (_stopping.IsCancellationRequested)
            return;

        var relative = Path.GetRelativePath(_sourceRoot, path);

        var parts = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (parts.Length != 3 || !parts[1].Equals(
            StorageNames.ScreensDirectory,
            StringComparison.OrdinalIgnoreCase
        ) || !parts[2].EndsWith(
            StorageNames.ScreenshotExtension,
            StringComparison.OrdinalIgnoreCase
        ) || parts[2].EndsWith(
            StorageNames.ThumbnailSuffix,
            StringComparison.OrdinalIgnoreCase
        ) || parts[0] is StorageNames.CurrentDirectory or StorageNames.ParentDirectory)
            return;

        var id = parts[0] + StorageNames.EntrySeparator + parts[2];

        if (_retryAfter.TryGetValue(id, out var nextTry) && nextTry > DateTime.UtcNow)
            return;

        if (store.Contains(id) || !_pending.TryAdd(id, 0))
            return;

        if (store.Contains(id))
        {
            _pending.TryRemove(id, out _);

            return;
        }

        FileStream? input = null;

        var timestamp = DateTime.UtcNow;

        try
        {
            // Hold the read handle immediately, before waiting for the copy worker.
            input = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                65536,
                true
            );

            timestamp = File.GetLastWriteTimeUtc(path);

            notify |= timestamp.Ticks >= Volatile.Read(ref pausedFromTicks);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }

        if (!_jobs.Writer.TryWrite(new CaptureJob(path, parts[0], parts[2], timestamp, notify, input)))
        {
            input?.Dispose();

            _pending.TryRemove(id, out _);
        }
    }

    private async Task ProcessJobs()
    {
        try
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(_stopping.Token))
            {
                var id = job.Account + StorageNames.EntrySeparator + job.FileName;

                FileStream? input = job.Input;

                var acquired = false;

                var folder = Path.Combine(store.CapturesRoot, job.Account);

                var destination = Path.Combine(folder, job.FileName);

                var temporary = destination + StorageNames.PartialSuffix;

                try
                {
                    await _writes.WaitAsync(_stopping.Token);

                    acquired = true;

                    // A folder switch can replace the store while this job is waiting for the writer.
                    folder = Path.Combine(store.CapturesRoot, job.Account);

                    destination = Path.Combine(folder, job.FileName);

                    temporary = destination + StorageNames.PartialSuffix;

                    if (store.Contains(id))
                        continue;

                    Directory.CreateDirectory(folder);

                    Exception? failure = null;

                    var ready = false;
                    // Retry partial JPEG writes for up to approximately 10 seconds.

                    for (var attempt = 0; attempt < 100; attempt++)
                    {
                        _stopping.Token.ThrowIfCancellationRequested();

                        try
                        {
                            input ??= new FileStream(
                                job.Path,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.ReadWrite | FileShare.Delete,
                                65536,
                                true
                            );

                            input.Position = 0;

                            using (var output = new FileStream(
                                temporary,
                                FileMode.Create,
                                FileAccess.Write,
                                FileShare.None,
                                65536,
                                true
                            ))
                            {
                                await input.CopyToAsync(output, _stopping.Token);
                            }

                            using (var imageFile = File.OpenRead(temporary))
                            {
                                if (imageFile.Length < 4)
                                    throw new IOException(Messages.IncompleteImage);

                                imageFile.Position = imageFile.Length - 2;

                                if (imageFile.ReadByte() != 0xff || imageFile.ReadByte() != 0xd9)
                                    throw new IOException(Messages.IncompleteImage);

                                imageFile.Position = 0;

                                using var image = Image.FromStream(imageFile, false, true);

                                using var decoded = new Bitmap(image);
                            }

                            File.Move(temporary, destination, true);

                            ready = true;

                            break;
                        }
                        catch (Exception ex) when (ex is IOException
                            or UnauthorizedAccessException
                            or ArgumentException
                            or OutOfMemoryException)
                        {
                            failure = ex;

                            if (input == null && !File.Exists(job.Path))
                                break;

                            CopyRetrying?.Invoke(job.Path);

                            await Task.Delay(100, _stopping.Token);
                        }
                    }

                    if (ready)
                    {
                        _retryAfter.TryRemove(id, out _);

                        var entry = store.Add(job.Account, job.FileName, job.Time, job.Notify);

                        CaptureSaved?.Invoke(entry, job.Notify);
                    }
                    else
                    {
                        _retryAfter[id] = DateTime.UtcNow.AddMinutes(1);

                        Warning?.Invoke(
                            Messages.CopyFailedPrefix + job.FileName + Messages.ErrorSeparator + failure?.Message
                        );
                    }
                }
                catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
                {
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _retryAfter[id] = DateTime.UtcNow.AddMinutes(1);

                    Warning?.Invoke(Messages.SaveFailedPrefix + ex.Message);
                }
                finally
                {
                    input?.Dispose();

                    if (acquired)
                    {
                        _writes.Release();
                    }

                    try
                    {
                        if (File.Exists(temporary))
                        {
                            File.Delete(temporary);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                    }

                    _pending.TryRemove(id, out _);
                }
            }
        }
        catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
        {
        }
        finally
        {
            while (_jobs.Reader.TryRead(out var job))
            {
                job.Input?.Dispose();
            }
        }
    }

    private void SetStatus(string status)
    {
        if (status == lastStatus)
            return;

        lastStatus = status;

        StatusChanged?.Invoke(status);
    }

    public async ValueTask DisposeAsync()
    {
        await _scanTimer.DisposeAsync().ConfigureAwait(false);

        watcher?.Dispose();

        _jobs.Writer.TryComplete();

        _stopping.Cancel();

        await _worker.ConfigureAwait(false);

        _stopping.Dispose();

        _writes.Dispose();
    }
}
