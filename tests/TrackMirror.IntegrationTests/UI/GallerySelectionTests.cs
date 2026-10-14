// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;

namespace TrackMirror.IntegrationTests.UI;

public sealed class GallerySelectionTests
{
    [Fact]
    public void Select_all_uses_the_whole_result_set_and_excludes_later_captures()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var captures = Enumerable.Range(0, 40)
            .Select(index => workspace.Capture($"2026-10-14T1200{index:D2}-0.jpg")).ToList();

        workspace.Json("index.json", captures);

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            using var gallery = new CaptureGallery(store) { Size = new Size(500, 250) };

            var opened = false;

            gallery.EntryOpened += _ => opened = true;

            gallery.SetEntries(store.Snapshot(), true);

            // Act
            gallery.SelectAll(true);

            // Assert
            Assert.Equal(40, gallery.SelectedEntries.Count);

            Assert.False(opened);

            // Act
            var added = workspace.Capture("2026-10-14T120045-0.jpg");

            store.Add(added.Account, added.FileName, added.CapturedUtc, notify: true);

            gallery.SetEntries(store.Snapshot(), false);

            // Assert
            Assert.Equal(40, gallery.SelectedEntries.Count);

            Assert.DoesNotContain(gallery.SelectedEntries, entry => entry.Id == added.Id);

            // Act
            gallery.SetEntries(store.Snapshot().Take(2).ToList(), true);

            // Assert
            Assert.Single(gallery.SelectedEntries);

            // Act
            gallery.SelectAll(false);

            // Assert
            Assert.Empty(gallery.SelectedEntries);
        });
    }
}
