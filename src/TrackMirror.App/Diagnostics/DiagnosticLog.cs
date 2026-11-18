// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting;
using TrackMirror.Constants;

namespace TrackMirror.Diagnostics;

internal sealed partial class DiagnosticLog : IDisposable
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }, MaxDepth = 32,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly string? _directory;
    private readonly DiagnosticLogOptions _options;
    private readonly Mutex? _mutex;
    private readonly Guid _session = Guid.NewGuid();
    private readonly object _lifetimeGate = new();
    private long dropped;
    private bool disposed;

    internal static DiagnosticLog Disabled { get; } = new();
    internal bool Available => _directory != null && !disposed;
    internal string? DirectoryPath => _directory;

    private DiagnosticLog() { _options = new(); }

    internal DiagnosticLog(string directory, DiagnosticLogOptions? options = null)
    {
        _options = options ?? new DiagnosticLogOptions();

        if (!_options.IsValid)
            throw new ArgumentOutOfRangeException(nameof(options));

        _directory = Path.GetFullPath(directory);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_directory.ToUpperInvariant())));
        _mutex = new Mutex(false, @"Local\TrackMirror.Diagnostics." + key);
    }

    internal void Write(DiagnosticComponent component, DiagnosticEvent diagnosticEvent, Exception? error = null,
        DiagnosticMetrics? metrics = null, DiagnosticLevel? level = null)
    {
        if (!Monitor.TryEnter(_lifetimeGate, level == DiagnosticLevel.Fatal ? 1000 : 100))
        {
            Interlocked.Increment(ref dropped);
            return;
        }

        try
        {
            WriteCore(component, diagnosticEvent, error, metrics, level);
        }
        finally
        {
            Monitor.Exit(_lifetimeGate);
        }
    }

    private void WriteCore(DiagnosticComponent component, DiagnosticEvent diagnosticEvent, Exception? error,
        DiagnosticMetrics? metrics, DiagnosticLevel? level)
    {
        if (!Available || !Enum.IsDefined(component) || !Enum.IsDefined(diagnosticEvent)
            || level is { } severity && !Enum.IsDefined(severity) || metrics is { IsValid: false })
            return;

        var acquired = false;

        try
        {
            var entry = new DiagnosticEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow, SessionId = _session, ModuleId = typeof(DiagnosticLog).Module.ModuleVersionId,
                Version = ProjectInfo.Version, Component = component, Event = diagnosticEvent,
                Level = level ?? (error == null ? DiagnosticLevel.Information : DiagnosticLevel.Error),
                Metrics = metrics, Exception = error == null ? null : DiagnosticException.Capture(error)
            };
            var maximumEntryBytes = Math.Min(16384, _options.MaximumFileBytes / 4);
            var json = JsonSerializer.Serialize(entry, JsonOptions);

            if (Encoding.UTF8.GetByteCount(json) + 1 > maximumEntryBytes)
            {
                entry = entry with { Exception = entry.Exception is { } detail ? detail with { Stack = [], Inner = [], Truncated = true } : null };
                json = JsonSerializer.Serialize(entry, JsonOptions);
            }

            if (Encoding.UTF8.GetByteCount(json) + 1 > maximumEntryBytes)
            {
                Interlocked.Increment(ref dropped);
                return;
            }

            acquired = Enter(entry.Level == DiagnosticLevel.Fatal ? 1000 : 100);

            if (!acquired)
            {
                Interlocked.Increment(ref dropped);
                return;
            }

            EnsureRegular(_directory!, includeParents: true);

            foreach (var file in OwnedFiles())
                EnsureRegular(file);

            // Reserve one bounded record: Serilog rolls before an append, not partway through it.
            // Close the sink before releasing the process mutex: idle instances must not pin old files.
            using var writer = new LoggerConfiguration()
                .WriteTo.Fallible(sinks => sinks.File(new RecordFormatter(), Path.Combine(_directory!, "trackmirror-.jsonl"),
                    fileSizeLimitBytes: _options.MaximumFileBytes - maximumEntryBytes,
                    retainedFileCountLimit: _options.MaximumFiles,
                    retainedFileTimeLimit: TimeSpan.FromDays(_options.RetentionDays),
                    rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true,
                    shared: true, buffered: false, encoding: new UTF8Encoding(false)), new FailureCounter(this))
                .CreateLogger();

            var sinkLevel = entry.Level switch
            {
                DiagnosticLevel.Warning => LogEventLevel.Warning,
                DiagnosticLevel.Error => LogEventLevel.Error,
                DiagnosticLevel.Fatal => LogEventLevel.Fatal,
                _ => LogEventLevel.Information
            };

            // Serilog never receives the original exception or any arbitrary application text.
            writer.ForContext("DiagnosticRecord", json).Write(sinkLevel, "Diagnostic event");
        }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException or SecurityException
            or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            Interlocked.Increment(ref dropped);
        }
        finally
        {
            if (acquired)
                _mutex!.ReleaseMutex();
        }
    }

    private bool Enter(int milliseconds)
    {
        try { return _mutex!.WaitOne(milliseconds); }
        catch (AbandonedMutexException) { return true; }
    }

    private IEnumerable<string> OwnedFiles() => Directory.Exists(_directory)
        ? Directory.EnumerateFiles(_directory!, "trackmirror-*.jsonl", SearchOption.TopDirectoryOnly)
            .Where(path => LogFileName().IsMatch(Path.GetFileName(path)))
        : [];

    [GeneratedRegex(@"^trackmirror-[0-9]{8}(?:_[0-9]{3,})?\.jsonl$", RegexOptions.CultureInvariant)]
    private static partial Regex LogFileName();

    private static void EnsureRegular(string path, bool includeParents = false)
    {
        for (string? current = path; current != null; current = includeParents ? Path.GetDirectoryName(current) : null)
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException("Diagnostic links are unsupported.");
    }

    private sealed class RecordFormatter : ITextFormatter
    {
        public void Format(LogEvent logEvent, TextWriter output)
        {
            if (logEvent.Properties.TryGetValue("DiagnosticRecord", out var record) && record is ScalarValue { Value: string json })
            {
                output.Write(json);
                output.Write('\n');
            }
        }
    }

    private sealed class FailureCounter(DiagnosticLog owner) : ILoggingFailureListener
    {
        public void OnLoggingFailed(object sender, LoggingFailureKind kind, string message,
            IReadOnlyCollection<LogEvent>? events, Exception? exception)
        {
            if (kind != LoggingFailureKind.Temporary)
                Interlocked.Add(ref owner.dropped, events?.Count ?? 1);
        }
    }

    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (disposed || _directory == null)
                return;

            disposed = true;
            _mutex?.Dispose();
        }
    }
}
