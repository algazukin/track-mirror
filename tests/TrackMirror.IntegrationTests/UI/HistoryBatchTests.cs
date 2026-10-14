// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Forms;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class HistoryBatchTests
{
    [Fact]
    public void Read_all_and_undo_use_filtered_results_and_keep_later_captures_unread()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var entries = new[]
        {
            workspace.ImageCapture("a.jpg", flagged: true),
            workspace.ImageCapture("b.jpg", flagged: true),
            workspace.ImageCapture("c.jpg", viewed: true)
        };

        workspace.Json("index.json", entries);

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            using var history = new HistoryForm(store) { Opacity = 0, ShowInTaskbar = false };

            TestWindow.Show(history);

            var bar = UiControls.Field<GalleryActionBar>(history, "_galleryActions");

            var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

            List<string> undo = [];

            history.ReviewSelectionRequested += ids =>
            {
                undo = store.SetViewed(ids, true);

                history.ShowReviewUndo(undo.Count);

                history.RefreshEntries();
            };

            history.UndoReviewRequested += () =>
            {
                store.SetViewed(undo, false);

                history.ClearReviewUndo();

                history.RefreshEntries();
            };

            UiControls.Field<CaptureFilterSelector>(history, "_captureFilter").SetFilter(CaptureFilter.Flagged);

            gallery.SelectAll(true);

            // Act
            UiControls.Field<Button>(bar, "_read").PerformClick();

            // Assert
            Assert.Equal(2, undo.Count);

            Assert.All(store.Snapshot(), entry => Assert.True(entry.Viewed));

            Assert.Empty(gallery.SelectedEntries);

            Assert.True(UiControls.Field<Button>(bar, "_undo").Visible);

            var arriving = workspace.ImageCapture("later.jpg");

            store.Add(arriving.Account, arriving.FileName, arriving.CapturedUtc, true);

            history.RefreshEntries();

            // Act
            UiControls.Field<Button>(bar, "_undo").PerformClick();

            // Assert
            Assert.True(store.Snapshot().Single(entry => entry.Id == entries[2].Id).Viewed);

            Assert.All(store.Snapshot().Where(entry => entry.Id != entries[2].Id),
                entry => Assert.False(entry.Viewed));

            Assert.Equal(2, store.Snapshot().Count(entry => entry.Flagged));

            Assert.False(UiControls.Field<Button>(bar, "_undo").Visible);
        });
    }
}
