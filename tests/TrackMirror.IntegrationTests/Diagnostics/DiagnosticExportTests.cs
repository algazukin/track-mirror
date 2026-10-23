// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.IO.Compression;
using System.Text.Json;
using System.Diagnostics;
using TrackMirror.Diagnostics;
using TrackMirror.IntegrationTests.Infrastructure;
using Xunit;

namespace TrackMirror.IntegrationTests.Diagnostics;

public sealed class DiagnosticExportTests
{
    [Fact]
    public void Ancestor_junctions_are_not_followed_for_writing_or_exporting()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var target = Path.Combine(workspace.Root, "private-parent");
        var directory = Path.Combine(target, "diagnostics");
        Directory.CreateDirectory(directory);
        var privateFile = Path.Combine(directory, $"trackmirror-{DateTime.Now:yyyyMMdd}.jsonl");
        File.WriteAllText(privateFile, "private-canary\n");
        var link = Path.Combine(workspace.Root, "linked-parent");
        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (var argument in new[] { "/c", "mklink", "/J", link, target })
            start.ArgumentList.Add(argument);
        using (var process = Process.Start(start)!)
        {
            Assert.True(process.WaitForExit(5000));
            Assert.Equal(0, process.ExitCode);
        }
        try
        {
            using var log = new DiagnosticLog(Path.Combine(link, "diagnostics"));

            // Act
            log.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);
            var error = Record.Exception(() => log.Export(Path.Combine(workspace.Root, "export.zip")));

            // Assert
            Assert.IsAssignableFrom<IOException>(error);
            Assert.Equal("private-canary\n", File.ReadAllText(privateFile));
            Assert.False(File.Exists(Path.Combine(workspace.Root, "export.zip")));
        }
        finally
        {
            Directory.Delete(link, recursive: false);
        }
    }

    [Fact]
    public void Export_rebuilds_only_safe_records_and_system_fields_without_copying_other_files()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var root = Path.Combine(workspace.Root, "logs");
        using var log = new DiagnosticLog(root);
        log.Write(DiagnosticComponent.Countdown, DiagnosticEvent.OperationFailed, new IOException("private-canary"));
        File.WriteAllText(Path.Combine(root, "startup-error.log"), "private-canary");
        File.WriteAllText(Path.Combine(root, "viewer.db"), "private-canary");
        File.WriteAllText(Path.Combine(root, "screenshot.jpg"), "private-canary");
        File.WriteAllText(Path.Combine(root, "settings.json"), "private-canary");
        DiagnosticEntry entry;
        using (var input = new FileStream(Directory.GetFiles(root, "*.jsonl").Single(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            entry = JsonSerializer.Deserialize<DiagnosticEntry>(input, DiagnosticLog.JsonOptions)!;
        var tampered = entry with
        {
            Version = "private-canary", Exception = entry.Exception! with
            {
                Stack = [new DiagnosticFrame { Token = 123, Method = "private-canary" }]
            }
        };
        var other = Path.Combine(root, $"trackmirror-{DateTime.Now.AddDays(-1):yyyyMMdd}.jsonl");
        File.WriteAllText(other, JsonSerializer.Serialize(tampered, DiagnosticLog.JsonOptions) + "\n");
        File.AppendAllText(other, "{\"Event\":\"private-canary\"}\nnot-json\n");
        var destination = Path.Combine(workspace.Root, "export.zip");

        // Act
        log.Export(destination);
        using var archive = ZipFile.OpenRead(destination);
        var content = string.Join("\n", archive.Entries.Select(item =>
        {
            using var reader = new StreamReader(item.Open());
            return reader.ReadToEnd();
        }));

        // Assert
        Assert.Equal(new[] { "diagnostics.jsonl", "system.json" }, archive.Entries.Select(item => item.FullName).Order().ToArray());
        Assert.DoesNotContain("private-canary", content);
        Assert.DoesNotContain(workspace.Root, content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.UserName + "\\", content, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Countdown", content);
        Assert.Contains("RuntimeVersion", content);
    }

    [Fact]
    public void Export_works_before_the_first_log_and_does_not_overwrite_a_destination_when_reading_fails()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var root = Path.Combine(workspace.Root, "logs");
        using var log = new DiagnosticLog(root);
        var destination = Path.Combine(workspace.Root, "export.zip");

        // Act
        log.Export(destination);
        var previous = File.ReadAllBytes(destination);
        log.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);
        var other = Path.Combine(root, $"trackmirror-{DateTime.Now.AddDays(-1):yyyyMMdd}.jsonl");
        using var locked = new FileStream(other, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        var error = Record.Exception(() => log.Export(destination));

        // Assert
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal(previous, File.ReadAllBytes(destination));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    [Fact]
    public void Export_refuses_to_replace_a_log_file()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        using var log = new DiagnosticLog(workspace.Root);
        log.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);
        var active = Directory.GetFiles(workspace.Root, "*.jsonl").Single();

        // Act
        var error = Record.Exception(() => log.Export(active));

        // Assert
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
        using var input = new FileStream(active, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Assert.Equal(DiagnosticEvent.Started, JsonSerializer.Deserialize<DiagnosticEntry>(input, DiagnosticLog.JsonOptions)!.Event);
    }

    [Fact]
    public void Export_refuses_non_zip_destinations_without_overwriting_user_data()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        using var log = new DiagnosticLog(Path.Combine(workspace.Root, "logs"));
        var destination = Path.Combine(workspace.Root, "viewer.db");
        File.WriteAllText(destination, "synthetic-history");

        // Act
        var error = Record.Exception(() => log.Export(destination));

        // Assert
        Assert.IsAssignableFrom<IOException>(error);
        Assert.Equal("synthetic-history", File.ReadAllText(destination));
        Assert.Empty(Directory.GetFiles(workspace.Root, "*.tmp"));
    }

    [Fact]
    public void Export_omits_expired_records_even_if_the_file_timestamp_is_recent()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        using var log = new DiagnosticLog(Path.Combine(workspace.Root, "logs"));
        log.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);
        var old = new DiagnosticEntry
        {
            TimestampUtc = DateTimeOffset.UtcNow.AddDays(-30), SessionId = Guid.NewGuid(),
            ModuleId = typeof(DiagnosticLog).Module.ModuleVersionId, Event = DiagnosticEvent.OperationCompleted
        };
        File.WriteAllText(Path.Combine(log.DirectoryPath!, $"trackmirror-{DateTime.Now.AddDays(-1):yyyyMMdd}.jsonl"),
            JsonSerializer.Serialize(old, DiagnosticLog.JsonOptions) + "\n");
        var destination = Path.Combine(workspace.Root, "export.zip");

        // Act
        log.Export(destination);
        using var archive = ZipFile.OpenRead(destination);
        using var reader = new StreamReader(archive.GetEntry("diagnostics.jsonl")!.Open());
        var text = reader.ReadToEnd();

        // Assert
        Assert.Contains("Started", text);
        Assert.DoesNotContain("OperationCompleted", text);
    }
}
