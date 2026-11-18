// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using TrackMirror.Diagnostics;

namespace TrackMirror.Monitoring;

internal sealed class ScreenshotScheduleMonitor : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly string _sourceRoot;
    private readonly string _logPath;
    private readonly Func<bool> _running;
    private readonly System.Threading.Timer _timer;

    private readonly Dictionary<long, long> _tasks = [];
    private readonly Dictionary<string, long?> _projects = new(StringComparer.Ordinal);

    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();

    private ScreenshotSchedule schedule;
    private ScreenshotScheduleStatus? unavailable;

    private long offset;
    private byte[] prefix = [];
    private byte[] bookmark = [];
    private string partial = string.Empty;
    private DateTime lastWrite;
    private DateTime metadataRead;

    private bool disposed;
    private bool enabled;
    private bool archiveRead;

    internal ScreenshotScheduleMonitor(string sourceRoot, string? logPath = null, Func<bool>? running = null)
    {
        _sourceRoot = sourceRoot;
        _logPath = logPath ?? Path.GetFullPath(Path.Combine(sourceRoot, "..", "..", "logs", "hubstaff.log"));
        _running = running ?? HubstaffRunning;

        schedule = new ScreenshotSchedule(ResolveProject);

        _timer = new System.Threading.Timer(_ =>
        {
            lock (_gate)
            {
                if (enabled && !disposed)
                    Refresh();
            }
        }, null, Timeout.Infinite, Timeout.Infinite);
    }

    internal void SetEnabled(bool value)
    {
        lock (_gate)
        {
            if (disposed || enabled == value)
                return;

            enabled = value;

            Reset();

            _timer.Change(value ? 0 : Timeout.Infinite, value ? 500 : Timeout.Infinite);
        }
    }

    internal void Refresh()
    {
        lock (_gate)
        {
            if (disposed)
                return;

            try
            {
                if (!_running())
                {
                    Reset();
                    unavailable = ScreenshotScheduleStatus.Stopped;

                    return;
                }

                using var stream = new FileStream(_logPath, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);

                var length = stream.Length;
                var write = File.GetLastWriteTimeUtc(_logPath);

                var beginning = new byte[(int)Math.Min(256, length)];
                stream.ReadExactly(beginning);

                var common = Math.Min(beginning.Length, prefix.Length);
                var replaced = length < offset || !beginning.AsSpan(0, common).SequenceEqual(prefix.AsSpan(0, common))
                    || (length == offset && write != lastWrite);

                if (!replaced && bookmark.Length > 0)
                {
                    stream.Position = offset - bookmark.Length;
                    var previous = new byte[bookmark.Length];
                    stream.ReadExactly(previous);

                    replaced = !previous.AsSpan().SequenceEqual(bookmark);
                }

                if (replaced)
                    Reset(keepSchedule: ContinueRotatedLog());

                if (DateTime.UtcNow - metadataRead > TimeSpan.FromSeconds(5))
                    ReadProjects();

                if (!archiveRead)
                {
                    ReadPreviousLog();

                    archiveRead = true;
                }

                // Hubstaff normally rotates at 2 MiB. Avoid unbounded replay of unexpected files.
                if (offset == 0 && length > 4 * 1024 * 1024)
                    throw new IOException("The Hubstaff log exceeds the supported replay size.");

                stream.Position = offset;
                var budget = offset == 0 ? 4 * 1024 * 1024 : 256 * 1024;
                var bytes = new byte[(int)Math.Min(length - offset, budget)];
                stream.ReadExactly(bytes);
                offset = stream.Position;

                ReadLines(bytes);

                prefix = beginning;
                lastWrite = write;

                bookmark = new byte[(int)Math.Min(64, offset)];
                stream.Position = offset - bookmark.Length;
                stream.ReadExactly(bookmark);

                unavailable = offset == length ? null : ScreenshotScheduleStatus.Waiting;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException
                or System.ComponentModel.Win32Exception or System.Security.SecurityException)
            {
                if (unavailable != ScreenshotScheduleStatus.Unavailable)
                    AppDiagnostics.Write(DiagnosticComponent.Countdown, DiagnosticEvent.OperationFailed, ex);

                Reset();

                unavailable = ScreenshotScheduleStatus.Unavailable;
            }
        }
    }

    internal ScreenshotScheduleSnapshot Snapshot(DateTimeOffset now)
    {
        lock (_gate)
            return unavailable is { } status ? new(status) : schedule.Snapshot(now);
    }

    private void Reset(bool keepSchedule = false)
    {
        if (!keepSchedule)
            schedule = new ScreenshotSchedule(ResolveProject);

        offset = 0;
        prefix = bookmark = [];
        partial = string.Empty;
        metadataRead = DateTime.MinValue;
        archiveRead = keepSchedule;

        _decoder.Reset();
    }

    private bool ContinueRotatedLog()
    {
        var path = Path.ChangeExtension(_logPath, ".1");

        if (offset == 0 || !File.Exists(path))
            return false;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length < offset || stream.Length > 4 * 1024 * 1024)
            return false;

        var beginning = new byte[prefix.Length];
        stream.ReadExactly(beginning);

        if (!beginning.AsSpan().SequenceEqual(prefix))
            return false;

        stream.Position = offset - bookmark.Length;
        var previous = new byte[bookmark.Length];
        stream.ReadExactly(previous);

        if (!previous.AsSpan().SequenceEqual(bookmark))
            return false;

        // Only preserve tracking when the archive continues the exact file already observed.
        stream.Position = offset;
        var bytes = new byte[(int)(stream.Length - offset)];
        stream.ReadExactly(bytes);

        ReadLines(bytes);

        return partial.Length == 0;
    }

    private void ReadLines(byte[] bytes)
    {
        var characters = new char[Encoding.UTF8.GetMaxCharCount(bytes.Length)];
        var count = _decoder.GetChars(bytes, 0, bytes.Length, characters, 0, flush: false);
        var text = partial + new string(characters, 0, count);
        var start = 0;

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] != '\n')
                continue;

            var end = index > start && text[index - 1] == '\r' ? index - 1 : index;
            schedule.Read(text[start..end]);
            start = index + 1;
        }

        partial = text[start..];

        if (partial.Length > 16384)
            partial = string.Empty;
    }

    private void ReadPreviousLog()
    {
        // Session start can precede the current log after Hubstaff rotates hubstaff.log to hubstaff.1.
        var path = Path.ChangeExtension(_logPath, ".1");

        if (!File.Exists(path))
            return;

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        if (stream.Length > 4 * 1024 * 1024)
            return;

        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);

        var text = Encoding.UTF8.GetString(bytes);
        var end = text.LastIndexOf('\n');

        if (end < 0)
            return;

        using var lines = new StringReader(text[..(end + 1)]);

        while (lines.ReadLine() is { } line)
            schedule.Read(line);
    }

    private long? ResolveProject(long task, string name) => task != 0
        ? (_tasks.TryGetValue(task, out var project) ? project : null)
        : _projects.GetValueOrDefault(name);

    private void ReadProjects()
    {
        var tasks = new Dictionary<long, long>();
        var projects = new Dictionary<string, long?>(StringComparer.Ordinal);

        if (Directory.Exists(_sourceRoot))
        {
            foreach (var account in Directory.EnumerateDirectories(_sourceRoot))
            {
                foreach (var fileName in new[] { "Task.xml", "Project.xml" })
                {
                    var path = Path.Combine(account, fileName);

                    if (!File.Exists(path))
                        continue;

                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite | FileShare.Delete);
                    using var reader = XmlReader.Create(stream, new XmlReaderSettings
                    {
                        DtdProcessing = DtdProcessing.Ignore, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024
                    });

                    foreach (var item in XDocument.Load(reader).Descendants("item"))
                    {
                        if (!long.TryParse(item.Element("id")?.Value, out var id))
                            continue;

                        if (fileName == "Task.xml" && long.TryParse(item.Element("project")?.Value, out var parent))
                            tasks[id] = parent;
                        else if (fileName == "Project.xml" && item.Element("name")?.Value is { } name)
                            projects[name] = projects.TryGetValue(name, out var existing) && existing != id ? null : id;
                    }
                }
            }
        }

        _tasks.Clear();
        _projects.Clear();

        foreach (var pair in tasks)
            _tasks.Add(pair.Key, pair.Value);

        foreach (var pair in projects)
            _projects.Add(pair.Key, pair.Value);

        metadataRead = DateTime.UtcNow;
    }

    private static bool HubstaffRunning()
    {
        var processes = Process.GetProcessesByName("HubstaffClient");

        foreach (var process in processes)
            process.Dispose();

        return processes.Length > 0;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            disposed = true;
            enabled = false;
        }

        await _timer.DisposeAsync().ConfigureAwait(false);
    }
}
