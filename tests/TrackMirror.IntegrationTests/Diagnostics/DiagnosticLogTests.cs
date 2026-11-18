// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Diagnostics;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Models;
using TrackMirror.Storage;
using Xunit;

namespace TrackMirror.IntegrationTests.Diagnostics;

public sealed class DiagnosticLogTests
{
    [Fact]
    public void Exceptions_keep_actionable_stack_frames_without_messages_paths_or_data()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        using var log = new DiagnosticLog(workspace.Root);
        var blocked = Path.Combine(workspace.Root, "private-account-secret-token.txt");
        File.WriteAllText(blocked, "fixture");
        var preferences = new SettingsStore(blocked);
        var error = Record.Exception(() => preferences.Save(new AppSettings()));
        Assert.NotNull(error);
        error.Data["private-data"] = "private-value";

        // Act
        log.Write(DiagnosticComponent.Storage, DiagnosticEvent.OperationFailed, error);
        log.Dispose();
        var text = File.ReadAllText(Directory.GetFiles(workspace.Root, "*.jsonl").Single());
        using var entry = JsonDocument.Parse(text);

        // Assert
        Assert.DoesNotContain(workspace.Root, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-", text);
        Assert.DoesNotContain("Message", text);
        Assert.Equal("Storage", entry.RootElement.GetProperty("Component").GetString());
        Assert.Equal("OperationFailed", entry.RootElement.GetProperty("Event").GetString());
        Assert.NotEmpty(entry.RootElement.GetProperty("Version").GetString()!);
        Assert.Equal(TimeSpan.Zero, entry.RootElement.GetProperty("TimestampUtc").GetDateTimeOffset().Offset);
        Assert.Equal("IO", entry.RootElement.GetProperty("Exception").GetProperty("Kind").GetString());
        Assert.Contains("SettingsStore.Save", text);
    }

    [Fact]
    public void Nested_and_aggregate_errors_never_serialize_provider_text()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        using var log = new DiagnosticLog(workspace.Root);
        var error = new AggregateException("password=demo-private", Enumerable.Range(0, 20)
            .Select(_ => new InvalidOperationException("OCR demo-private", new Exception("prompt demo-private"))));

        // Act
        log.Write(DiagnosticComponent.AI, DiagnosticEvent.OperationFailed, error);
        log.Dispose();
        var text = File.ReadAllText(Directory.GetFiles(workspace.Root, "*.jsonl").Single());

        // Assert
        Assert.DoesNotContain("demo-private", text);
        Assert.True(text.Length < 16000);
        Assert.Contains("Aggregate", text);
    }

    [Fact]
    public void Rotation_bounds_total_storage_and_keeps_recent_events_under_concurrent_writers()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var options = new DiagnosticLogOptions { MaximumFileBytes = 4096, MaximumFiles = 3 };
        using var first = new DiagnosticLog(workspace.Root, options);
        using var second = new DiagnosticLog(workspace.Root, options);

        // Act
        Parallel.For(0, 100, index => (index % 2 == 0 ? first : second).Write(
            DiagnosticComponent.Application, DiagnosticEvent.Started, metrics: new DiagnosticMetrics { Count = index }));
        first.Dispose();
        second.Dispose();
        var files = Directory.GetFiles(workspace.Root, "*.jsonl");
        var records = files.SelectMany(File.ReadAllLines).Select(line => JsonDocument.Parse(line)).ToList();

        // Assert
        Assert.InRange(files.Length, 1, 3);
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 4096));
        Assert.NotEmpty(records);
        Assert.All(records, record => Assert.Equal("Started", record.RootElement.GetProperty("Event").GetString()));
        records.ForEach(record => record.Dispose());
    }

    [Fact]
    public void An_idle_writer_does_not_prevent_another_instance_from_enforcing_retention()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var options = new DiagnosticLogOptions { MaximumFileBytes = 4096, MaximumFiles = 3 };
        using var idle = new DiagnosticLog(workspace.Root, options);
        using var active = new DiagnosticLog(workspace.Root, options);
        idle.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);

        // Act
        for (var index = 0; index < 100; index++)
            active.Write(DiagnosticComponent.Application, DiagnosticEvent.StateChanged);

        var files = Directory.GetFiles(workspace.Root, "*.jsonl");

        // Assert
        Assert.InRange(files.Length, 1, 3);
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 4096));
    }

    [Fact]
    public void Retention_removes_only_owned_expired_files_and_write_failures_do_not_escape()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var old = Path.Combine(workspace.Root, $"trackmirror-{DateTime.Now.AddDays(-30):yyyyMMdd}.jsonl");
        File.WriteAllText(old, "{}");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddDays(-30));
        var unrelated = Path.Combine(workspace.Root, "personal.txt");
        File.WriteAllText(unrelated, "private fixture");
        using var log = new DiagnosticLog(workspace.Root);

        // Act
        log.Write(DiagnosticComponent.Application, DiagnosticEvent.Started);
        using var unavailable = new DiagnosticLog(unrelated);
        var failure = Record.Exception(() => unavailable.Write(DiagnosticComponent.Application, DiagnosticEvent.Stopped));

        // Assert
        Assert.False(File.Exists(old));
        Assert.Equal("private fixture", File.ReadAllText(unrelated));
        Assert.Null(failure);
    }
}
