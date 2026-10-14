// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Storage;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Migrations;

public sealed class StorageMigrationTests
{
    [Fact]
    public void Legacy_capture_creates_database_and_keeps_original_archive()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var before = File.ReadAllBytes(Path.Combine(workspace.Root, "index.json"));

        // Act
        var store = new ScreenshotStore(workspace.Root);

        // Assert
        Assert.Equal(capture, Assert.Single(store.Snapshot()));

        Assert.True(File.Exists(Path.Combine(workspace.Root, "viewer.db")), "SQLite storage was not created.");

        // Act
        store.MarkViewed(capture.Id);

        // Assert
        Assert.Equal(before, File.ReadAllBytes(Path.Combine(workspace.Root, "index.json")));

        Assert.True(Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()).Viewed);
    }
}
