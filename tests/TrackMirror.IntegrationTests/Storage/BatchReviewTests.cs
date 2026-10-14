// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Storage;
using Microsoft.Data.Sqlite;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage;

public sealed class BatchReviewTests
{
    [Fact]
    public void Undo_restores_only_changed_screenshots_and_preserves_current_flags()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var unread = workspace.Capture("2026-10-14T120000-0.jpg", flagged: false);

        var reviewed = workspace.Capture("2026-10-14T120001-0.jpg", viewed: true);

        workspace.Json("index.json", new[] { unread, reviewed });

        var store = new ScreenshotStore(workspace.Root);

        // Act
        var changed = store.SetViewed([unread.Id, reviewed.Id, unread.Id, "missing"], true);

        // Assert
        Assert.Equal([unread.Id], changed);

        Assert.All(new ScreenshotStore(workspace.Root).Snapshot(), entry => Assert.True(entry.Viewed));

        // Act
        store.ToggleFlag(unread.Id);

        var added = workspace.Capture("2026-10-14T120002-0.jpg");

        store.Add(added.Account, added.FileName, added.CapturedUtc, notify: true);

        store.SetViewed(changed, false);

        var reloaded = new ScreenshotStore(workspace.Root).Snapshot();

        // Assert
        Assert.True(reloaded.Single(entry => entry.Id == reviewed.Id).Viewed);

        Assert.False(reloaded.Single(entry => entry.Id == unread.Id).Viewed);

        Assert.True(reloaded.Single(entry => entry.Id == unread.Id).Flagged);

        Assert.False(reloaded.Single(entry => entry.Id == added.Id).Viewed);
    }

    [Fact]
    public void Failed_batch_rolls_back_every_row_and_keeps_memory_unchanged()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var first = workspace.Capture("2026-10-14T120000-0.jpg");

        var second = workspace.Capture("2026-10-14T120001-0.jpg");

        workspace.Json("index.json", new[] { first, second });

        var store = new ScreenshotStore(workspace.Root);

        using var connection = new SqliteConnection(
            "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db"));

        connection.Open();

        using var command = connection.CreateCommand();

        command.CommandText = """
            CREATE TRIGGER fail_batch BEFORE UPDATE ON captures WHEN OLD.file_name = '2026-10-14T120001-0.jpg'
            BEGIN SELECT RAISE(ABORT, 'synthetic storage failure'); END;
            """;

        command.ExecuteNonQuery();

        // Act
        // Assert
        Assert.Throws<IOException>(() => store.SetViewed([first.Id, second.Id], true));

        Assert.All(store.Snapshot(), entry => Assert.False(entry.Viewed));

        Assert.All(new ScreenshotStore(workspace.Root).Snapshot(), entry => Assert.False(entry.Viewed));
    }
}
