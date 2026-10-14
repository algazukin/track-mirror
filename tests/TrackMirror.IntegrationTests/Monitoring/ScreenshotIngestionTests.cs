// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.Monitoring;
using TrackMirror.Storage;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TrackMirror.IntegrationTests.Monitoring;

public sealed class ScreenshotIngestionTests
{
    [Fact]
    public async Task Copied_image_survives_a_failed_catalog_insert_and_is_recovered_once_on_restart()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        fixture.Write("fixture/screens/orphan.jpg");

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(fixture.Data.Root, "viewer.db"));

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TRIGGER reject_capture BEFORE INSERT ON captures
            BEGIN SELECT RAISE(ABORT, 'synthetic catalog failure'); END;
            """;

        command.ExecuteNonQuery();

        var warning = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        fixture.Store.Warning += value => warning.TrySetResult(value);

        // Act
        await fixture.StartAsync();

        var saved = await fixture.NextAsync();

        await warning.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        command.CommandText = "SELECT COUNT(*) FROM captures;";

        Assert.Equal(0L, command.ExecuteScalar());

        Assert.Equal(fixture.Image, File.ReadAllBytes(fixture.Store.CopyPath(saved.Entry)));

        // Act
        command.CommandText = "DROP TRIGGER reject_capture;";

        command.ExecuteNonQuery();

        var recovered = new ScreenshotStore(fixture.Data.Root);

        var reopened = new ScreenshotStore(fixture.Data.Root);

        // Assert
        Assert.Equal(saved.Entry.Id, Assert.Single(recovered.Snapshot()).Id);

        Assert.Equal(saved.Entry.Id, Assert.Single(reopened.Snapshot()).Id);

        command.CommandText = "SELECT COUNT(*) FROM captures;";

        Assert.Equal(1L, command.ExecuteScalar());

        Assert.Equal(fixture.Image, File.ReadAllBytes(reopened.CopyPath(Assert.Single(reopened.Snapshot()))));
    }

    [Fact]
    public async Task Initial_scan_keeps_accounts_separate_and_copies_full_images_without_changing_sources()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        var paths = new[]
        {
            fixture.Write("first/screens/2026-10-13T103000-0.jpg"),
            fixture.Write("second/screens/2026-10-13T103000-0.jpg")
        };

        var timestamps = paths.Select(File.GetLastWriteTimeUtc).ToArray();

        // Act
        await fixture.StartAsync();

        var saved = new[] { await fixture.NextAsync(), await fixture.NextAsync() };

        // Assert
        Assert.Equal(2, fixture.Store.Snapshot().Count);

        Assert.Equal(2, saved.Select(result => result.Entry.Id).Distinct().Count());

        Assert.All(saved, result =>
        {
            Assert.False(result.Notify);

            Assert.True(result.Entry.Viewed);

            Assert.Equal(fixture.Image, File.ReadAllBytes(fixture.Store.CopyPath(result.Entry)));

            using var image = fixture.Store.OpenImage(result.Entry);

            Assert.Equal(new Size(160, 100), image.Size);
        });

        Assert.Equal(timestamps, paths.Select(File.GetLastWriteTimeUtc));

        Assert.All(paths, path => Assert.Equal(fixture.Image, File.ReadAllBytes(path)));

        Assert.False(fixture.Warnings.Reader.TryRead(out _));
    }

    [Fact]
    public async Task Repeated_changes_and_restart_do_not_duplicate_or_reset_existing_marks()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        var path = fixture.Write("fixture/screens/same.jpg");

        await fixture.StartAsync();

        var first = await fixture.NextAsync();

        fixture.Store.ToggleFlag(first.Entry.Id);

        // Act
        for (var index = 0; index < 20; index++)
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(index));
        }

        fixture.Write("fixture/screens/barrier.jpg");

        var next = await fixture.NextAsync();

        // Assert
        Assert.Equal("barrier.jpg", next.Entry.FileName);

        Assert.True(next.Notify);

        Assert.False(next.Entry.Viewed);

        Assert.Equal(2, fixture.Store.Snapshot().Count);

        // Act
        await fixture.StopAsync();

        var duplicateEvents = 0;

        await using (var restarted = new ScreenshotMonitor(fixture.Source.Root, fixture.Store))
        {
            var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var barrier = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            restarted.StatusChanged += _ => ready.TrySetResult();

            restarted.CaptureSaved += (entry, _) =>
            {
                if (entry.FileName == "restart-barrier.jpg")
                {
                    barrier.TrySetResult();
                }
                else
                {
                    Interlocked.Increment(ref duplicateEvents);
                }
            };

            restarted.Start();

            await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));

            fixture.Write("fixture/screens/restart-barrier.jpg");

            await barrier.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        // Assert
        var reopened = new ScreenshotStore(fixture.Data.Root).Snapshot();

        Assert.Equal(3, reopened.Count);

        Assert.Equal(0, duplicateEvents);

        Assert.True(reopened.Single(entry => entry.Id == first.Entry.Id).Flagged);

        Assert.True(reopened.Single(entry => entry.Id == first.Entry.Id).Viewed);

        Assert.False(fixture.Saved.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Locked_or_incomplete_image_is_not_published_until_the_full_file_can_be_read(bool locked)
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        var path = fixture.Write("fixture/screens/recover.jpg", []);

        using var writer = new FileStream(path, FileMode.Open, FileAccess.Write,
            locked ? FileShare.None : FileShare.ReadWrite | FileShare.Delete);

        writer.Write(fixture.Image.AsSpan(0, fixture.Image.Length / 2));

        writer.Flush(flushToDisk: true);

        var destination = Path.Combine(fixture.Store.CapturesRoot, "fixture", "recover.jpg");

        // Act
        await fixture.StartAsync();

        var retriedPath = await fixture.Retries.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(path, retriedPath);

        Assert.Empty(fixture.Store.Snapshot());

        Assert.False(File.Exists(destination));

        // Act
        writer.Position = 0;

        writer.Write(fixture.Image);

        writer.SetLength(fixture.Image.Length);

        writer.Flush(flushToDisk: true);

        writer.Dispose();

        var saved = await fixture.NextAsync();

        // Assert
        Assert.Single(new ScreenshotStore(fixture.Data.Root).Snapshot());

        Assert.Equal(fixture.Image, File.ReadAllBytes(fixture.Store.CopyPath(saved.Entry)));

        Assert.False(File.Exists(destination + StorageNames.PartialSuffix));
    }

    [Fact]
    public async Task Corrupt_image_is_reported_without_a_history_entry_and_the_next_image_is_still_received()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        var broken = fixture.Write("fixture/screens/broken.jpg", [0xff, 0xd8, 0xff, 0xd9]);

        await fixture.StartAsync();

        // Act
        var warning = await fixture.Warnings.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(15));

        fixture.Write("fixture/screens/valid.jpg");

        var saved = await fixture.NextAsync();

        // Assert
        Assert.Contains("broken.jpg", warning);

        Assert.Equal("valid.jpg", Assert.Single(fixture.Store.Snapshot()).FileName);

        Assert.Equal(saved.Entry, Assert.Single(new ScreenshotStore(fixture.Data.Root).Snapshot()));

        Assert.Equal(new byte[] { 0xff, 0xd8, 0xff, 0xd9 }, File.ReadAllBytes(broken));

        Assert.False(File.Exists(Path.Combine(fixture.Store.CapturesRoot, "fixture", "broken.jpg")));

        Assert.Empty(Directory.EnumerateFiles(fixture.Store.CapturesRoot, "*.part", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Thumbnails_other_extensions_and_files_outside_account_screens_are_ignored()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        foreach (var relative in new[]
        {
            "fixture/screens/image-thumb.jpg", "fixture/screens/other.png", "fixture/screens/writing.jpg.partial",
            "fixture/screens/nested/extra.jpg", "fixture/elsewhere/image.jpg", "screens/image.jpg", "image.jpg"
        })
        {
            fixture.Write(relative);
        }

        fixture.Write("fixture/screens/VALID.JPG");

        // Act
        await fixture.StartAsync();

        var saved = await fixture.NextAsync();

        // Assert
        Assert.Equal("VALID.JPG", saved.Entry.FileName);

        Assert.Single(fixture.Store.Snapshot());

        Assert.Single(Directory.EnumerateFiles(fixture.Store.CapturesRoot, "*", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task Source_created_after_start_and_file_renamed_to_jpg_are_discovered()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        var sourceRoot = Path.Combine(fixture.Source.Root, "not-created-yet");

        await using var monitor = new ScreenshotMonitor(sourceRoot, fixture.Store);

        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var saved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        monitor.StatusChanged += _ => waiting.TrySetResult();

        monitor.CaptureSaved += (_, _) => saved.TrySetResult();

        monitor.Start();

        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        var path = fixture.Write("not-created-yet/fixture/screens/new.partial");

        File.Move(path, Path.ChangeExtension(path, ".jpg"));

        await saved.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.False(Assert.Single(fixture.Store.Snapshot()).Viewed);

        Assert.Equal(fixture.Image, File.ReadAllBytes(Path.ChangeExtension(path, ".jpg")));
    }

    [Fact]
    public async Task Input_handle_keeps_the_image_available_when_source_is_deleted_while_writes_are_paused()
    {
        // Arrange
        await using var fixture = new ScreenshotMonitorFixture();

        var path = fixture.Write("fixture/screens/short-lived.jpg");

        await fixture.Monitor.PauseAsync().WaitAsync(TimeSpan.FromSeconds(5));

        await fixture.StartAsync();

        // Act
        File.Delete(path);

        fixture.Monitor.Resume();

        var saved = await fixture.NextAsync();

        // Assert
        Assert.False(File.Exists(path));

        Assert.Equal(fixture.Image, File.ReadAllBytes(fixture.Store.CopyPath(saved.Entry)));

        Assert.Equal(saved.Entry, Assert.Single(new ScreenshotStore(fixture.Data.Root).Snapshot()));
    }

    [Fact]
    public async Task Shutdown_during_a_partial_write_removes_temporary_output_and_restart_recovers_the_source()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var data = new TestWorkspace();

        var template = source.ImageCapture();

        var image = File.ReadAllBytes(Path.Combine(source.Root, "captures", template.Account, template.FileName));

        var inputRoot = Path.Combine(source.Root, "input");

        var screens = Path.Combine(inputRoot, "fixture", "screens");

        Directory.CreateDirectory(screens);

        var path = Path.Combine(screens, "interrupted.jpg");

        var incomplete = image[..(image.Length / 2)];

        File.WriteAllBytes(path, incomplete);

        var store = new ScreenshotStore(data.Root);

        var monitor = new ScreenshotMonitor(inputRoot, store);

        // Act
        try
        {
            monitor.Start();

            await ScreenshotMonitorFixture.WaitForFileAsync(
                Path.Combine(store.CapturesRoot, "fixture", "interrupted.jpg.part"));
        }
        finally
        {
            await monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        }

        // Assert
        Assert.Empty(new ScreenshotStore(data.Root).Snapshot());

        Assert.Empty(Directory.EnumerateFiles(store.CapturesRoot, "*", SearchOption.AllDirectories));

        Assert.Equal(incomplete, File.ReadAllBytes(path));

        // Act
        File.WriteAllBytes(path, image);

        await using var restarted = new ScreenshotMonitor(inputRoot, new ScreenshotStore(data.Root));

        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        restarted.CaptureSaved += (_, _) => received.TrySetResult();

        restarted.Start();

        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        var recovered = new ScreenshotStore(data.Root);

        Assert.Equal(image, File.ReadAllBytes(recovered.CopyPath(Assert.Single(recovered.Snapshot()))));
    }
}
