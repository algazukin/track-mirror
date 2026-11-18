// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TrackMirror.Constants;

namespace TrackMirror.Diagnostics;

internal sealed partial class DiagnosticLog
{
    internal void Export(string destination)
    {
        if (!Available)
            throw new IOException("Diagnostics are unavailable.");

        var target = Path.GetFullPath(destination);
        var relative = Path.GetRelativePath(_directory!, target);

        if (!string.Equals(Path.GetExtension(target), ".zip", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose a ZIP archive destination.");

        if (relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative))
            throw new IOException("Choose a destination outside the diagnostic directory.");

        var temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            var snapshot = Snapshot();

            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(archive.CreateEntry("diagnostics.jsonl").Open()))
                {
                    // Parse and compress outside the writer lock, so exporting does not stall the UI.
                    foreach (var bytes in snapshot)
                    {
                        using var input = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);

                        while (input.ReadLine() is { } line)
                        {
                            if (line.Length > 16384)
                                continue;

                            var safe = ExportEntry(line);

                            if (safe != null)
                                writer.WriteLine(JsonSerializer.Serialize(safe, JsonOptions));
                        }
                    }
                }

                using var metadata = new StreamWriter(archive.CreateEntry("system.json").Open());
                metadata.Write(JsonSerializer.Serialize(new
                {
                    Application = "TrackMirror", Version = ProjectInfo.Version,
                    ExportedUtc = DateTimeOffset.UtcNow,
                    ModuleId = typeof(DiagnosticLog).Module.ModuleVersionId,
                    OSPlatform = Environment.OSVersion.Platform.ToString(), OSVersion = Environment.OSVersion.Version.ToString(),
                    RuntimeVersion = Environment.Version.ToString(),
                    OSArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    ProcessorCount = Environment.ProcessorCount,
                    AvailableMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                    MaximumFileBytes = _options.MaximumFileBytes, MaximumFiles = _options.MaximumFiles,
                    RetentionDays = _options.RetentionDays, DroppedRecords = Interlocked.Read(ref dropped)
                }, JsonOptions));
            }

            File.Move(temporary, target, true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException) { }
        }
    }

    private List<byte[]> Snapshot()
    {
        lock (_lifetimeGate)
        {
            if (!Available || !Enter(2000))
                throw new IOException("Diagnostics are unavailable or busy.");

            try
            {
                EnsureRegular(_directory!, includeParents: true);
                var files = new List<byte[]>();

                foreach (var file in OwnedFiles().OrderByDescending(File.GetLastWriteTimeUtc).Take(_options.MaximumFiles).Reverse())
                {
                    EnsureRegular(file);

                    if (!File.Exists(file) || File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddDays(-_options.RetentionDays))
                        continue;

                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

                    if (stream.Length > _options.MaximumFileBytes)
                        continue;

                    var bytes = new byte[(int)stream.Length];
                    stream.ReadExactly(bytes);
                    files.Add(bytes);
                }

                return files;
            }
            finally
            {
                _mutex!.ReleaseMutex();
            }
        }
    }

    private DiagnosticEntry? ExportEntry(string line)
    {
        try
        {
            var entry = JsonSerializer.Deserialize<DiagnosticEntry>(line, JsonOptions);

            if (entry == null || !Enum.IsDefined(entry.Component) || !Enum.IsDefined(entry.Event)
                || !Enum.IsDefined(entry.Level) || entry.SessionId == Guid.Empty || entry.ModuleId == Guid.Empty
                || entry.TimestampUtc < DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays) || entry.Metrics is { IsValid: false })
                return null;

            return entry with
            {
                TimestampUtc = entry.TimestampUtc.ToUniversalTime(),
                Version = entry.ModuleId == typeof(DiagnosticLog).Module.ModuleVersionId ? ProjectInfo.Version : "previous-build",
                Exception = entry.Exception?.ForExport(entry.ModuleId)
            };
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
