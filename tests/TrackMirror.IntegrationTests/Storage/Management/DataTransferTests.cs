// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Management;

public sealed class DataTransferTests
{
    [Fact]
    public async Task Move_keeps_identifiers_marks_settings_queue_and_files_and_redirects_the_previous_folder()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.Capture("Проверка.jpg") with { Id = "original-external-id" };

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        var settings = new AppSettings { RetentionDays = 90, PrivacyGames = true, RetentionKeepUnread = false };

        new SettingsStore(source.Root).Save(settings);

        var queued = new PrivacyScanRecord { CaptureId = capture.Id, RequestId = "keep-queued", Profile = "fixture",
            State = PrivacyScanState.Queued, RequestedCategories = [PrivacyScanCategory.PersonalChat] };

        new PrivacyResultStore(source.Root, new HashSet<string> { capture.Id }).Put([queued]);

        var target = Path.Combine(output.Root, "New folder with spaces");

        // Act
        // Assert
        Assert.Equal(target, await DataFolderTransfer.MoveAsync(source.Root, target, default));

        Assert.Equal(target, DataLocationStore.ResolveRedirects(source.Root));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(target).Snapshot()));

        Assert.Equal(settings, new SettingsStore(target).Current);

        Assert.Equal(JsonSerializer.Serialize(queued), JsonSerializer.Serialize(new PrivacyResultStore(target,
            new HashSet<string> { capture.Id }).Snapshot()[capture.Id]));

        Assert.Equal(File.ReadAllBytes(store.CopyPath(capture)),
            File.ReadAllBytes(new ScreenshotStore(target).CopyPath(capture)));

        Assert.True(File.Exists(store.CopyPath(capture)));

        Assert.True(File.Exists(Path.Combine(source.Root, "viewer.db")));
    }

    [Fact]
    public async Task Backup_restore_recreates_a_verified_dataset_without_overwriting_the_current_one()
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.Capture(viewed: true);

        source.Json("index.json", new[] { capture });

        _ = new ScreenshotStore(source.Root);

        var backup = Path.Combine(output.Root, "backup.zip");

        // Act
        await DataArchive.CreateBackupAsync(source.Root, backup, default);

        // Assert
        Assert.True(File.Exists(backup));

        var target = Path.Combine(output.Root, "restored");

        // Act
        // Assert
        Assert.Equal(target, await DataArchive.RestoreAsync(source.Root, backup, target, default));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(target).Snapshot()));

        Assert.True(File.Exists(Path.Combine(source.Root, "viewer.db")));

        Assert.Equal(target, DataLocationStore.ResolveRedirects(source.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Move_and_backup_preserve_activity_AI_checkpoints_settings_and_pending_deletion_tombstones(
        bool backup)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var now = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

        var capture = source.ImageCapture("kept.jpg", viewed: true, flagged: true);

        var queued = source.ImageCapture("queued.jpg");

        var deleted = source.ImageCapture("deleted.jpg", viewed: true) with { CapturedUtc = now.AddDays(-31) };

        source.Json("index.json", new[] { capture, queued, deleted });

        var store = new ScreenshotStore(source.Root);

        var settings = new AppSettings
        {
            Language = "ru", NotificationsEnabled = false, PrivacyAutoScan = true, PrivacyGames = true,
            RetentionEnabled = true, RetentionDays = 30, RetentionKeepFlagged = false
        };

        new SettingsStore(source.Root).Save(settings);

        var activity = new HubstaffActivity("fixture", now.AddMinutes(-10), 300, 100, 50, 120, now);

        var activityStore = new HubstaffActivityStore(source.Root);

        activityStore.Merge([activity]);

        activityStore.Save();

        var scans = new[]
        {
            new PrivacyScanRecord
            {
                CaptureId = capture.Id, RequestId = "completed", Profile = "fixture", ImageHash = "stable-hash",
                State = PrivacyScanState.Completed, Disposition = PrivacyDisposition.Acknowledged,
                Categories = [new PrivacyCategoryResult
                {
                    Category = PrivacyScanCategory.Game, Version = "keep-version", State = PrivacyScanState.Completed,
                    Findings = [new PrivacyFinding(PrivacyRules.Game, PrivacyRules.Medium,
                        PrivacyRules.ContextReason, [])],
                    Diagnostics = [new PrivacyRequestDiagnostic { RequestBytes = 123, PromptTokens = 42 }]
                }]
            },
            new PrivacyScanRecord
            {
                CaptureId = queued.Id, RequestId = "queued", Profile = "fixture", State = PrivacyScanState.Queued,
                RequestedCategories = [PrivacyScanCategory.ConfidentialData]
            }
        };

        var ids = new HashSet<string> { capture.Id, queued.Id };

        new PrivacyResultStore(source.Root, ids).Put(scans);

        var scanSnapshot = JsonSerializer.Serialize(new PrivacyResultStore(source.Root, ids).Snapshot());

        var target = Path.Combine(output.Root, "transferred");

        using var locked = new FileStream(store.CopyPath(deleted), FileMode.Open, FileAccess.Read, FileShare.Read);

        Assert.Equal(new RetentionResult(1, 1),
            await new CaptureRetention(source.Root).PurgeAsync(settings, now, default));

        // Act
        if (backup)
        {
            var archive = Path.Combine(output.Root, "backup.zip");

            await DataArchive.CreateBackupAsync(source.Root, archive, default);

            await DataArchive.RestoreAsync(source.Root, archive, target, default);
        }
        else
        {
            await DataFolderTransfer.MoveAsync(source.Root, target, default);
        }

        // Assert
        var restored = new ScreenshotStore(target);

        Assert.Equal(new[] { capture, queued }.OrderBy(entry => entry.Id),
            restored.Snapshot().OrderBy(entry => entry.Id));

        Assert.Equal(settings, new SettingsStore(target).Current);

        Assert.Equal(activity, Assert.Single(new HubstaffActivityStore(target).Snapshot()));

        Assert.Equal(scanSnapshot, JsonSerializer.Serialize(new PrivacyResultStore(target, ids).Snapshot()));

        Assert.Equal(File.ReadAllBytes(store.CopyPath(capture)), File.ReadAllBytes(restored.CopyPath(capture)));

        Assert.True(restored.Contains(deleted.Id));

        Assert.False(File.Exists(restored.CopyPath(deleted)));

        Assert.Equal(0, new CaptureRetention(target).PendingFiles());

        Assert.True(File.Exists(store.CopyPath(deleted)));

        Assert.Equal(target, DataLocationStore.ResolveRedirects(source.Root));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_redirect_publication_preserves_both_the_active_source_and_the_verified_copy(bool restore)
    {
        // Arrange
        using var source = new TestWorkspace();

        using var output = new TestWorkspace();

        var capture = source.ImageCapture(viewed: true, flagged: true);

        source.Json("index.json", new[] { capture });

        var store = new ScreenshotStore(source.Root);

        var archive = Path.Combine(output.Root, "backup.zip");

        if (restore)
        {
            await DataArchive.CreateBackupAsync(source.Root, archive, default);
        }

        var target = Path.Combine(output.Root, "verified-copy");

        Directory.CreateDirectory(Path.Combine(source.Root, DataManagementNames.RedirectFile));

        // Act
        var error = await Assert.ThrowsAsync<IOException>(() => restore
            ? DataArchive.RestoreAsync(source.Root, archive, target, default)
            : DataFolderTransfer.MoveAsync(source.Root, target, default));

        // Assert
        Assert.Equal(DataManagementText.UncommittedCopy, error.Message);

        Assert.Equal(source.Root, DataLocationStore.ResolveRedirects(source.Root));

        Assert.Equal(capture, Assert.Single(new ScreenshotStore(source.Root).Snapshot()));

        var copy = new ScreenshotStore(target);

        Assert.Equal(capture, Assert.Single(copy.Snapshot()));

        Assert.Equal(File.ReadAllBytes(store.CopyPath(capture)), File.ReadAllBytes(copy.CopyPath(capture)));

        Assert.Equal(target, Assert.Single(Directory.EnumerateDirectories(output.Root)));
    }
}
