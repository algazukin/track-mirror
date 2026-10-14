// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Forms;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class HistoryWorkflowTests
{
    [Fact]
    public void Startup_shows_the_gallery_without_selecting_or_reading_the_latest_capture()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.ImageCapture();

        workspace.Json("index.json", new[] { capture });

        StaThread.Run(() =>
        {
            var store = new ScreenshotStore(workspace.Root);

            // Act
            using var history = new HistoryForm(store);

            // Assert
            Assert.Single(UiControls.Field<CaptureGallery>(history, "_gallery").Entries);

            Assert.Null(UiControls.Field<CaptureEntry?>(history, "selected"));

            Assert.False(Assert.Single(store.Snapshot()).Viewed);

            Assert.Null(UiControls.Field<PictureBox>(history, "_picture").Image);
        });
    }

    [Theory]
    [InlineData(nameof(CaptureFilter.All), 4)]
    [InlineData(nameof(CaptureFilter.Flagged), 2)]
    [InlineData(nameof(CaptureFilter.Unread), 3)]
    [InlineData(nameof(CaptureFilter.PrivacyRisks), 1)]
    public void Filters_are_independent_modes_and_keep_only_pending_AI_risks(string mode, int count)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var entries = new[]
        {
            workspace.ImageCapture("a.jpg", flagged: true),
            workspace.ImageCapture("b.jpg", viewed: true, flagged: true),
            workspace.ImageCapture("c.jpg"),
            workspace.ImageCapture("d.jpg")
        };

        workspace.Json("index.json", entries);

        StaThread.Run(() =>
        {
            using var history = new HistoryForm(new ScreenshotStore(workspace.Root));

            var risk = new PrivacyScanRecord
            {
                CaptureId = entries[2].Id, RequestId = "fixture", Profile = "fixture",
                ImageHash = "fixture", State = PrivacyScanState.Completed,
                Categories = [new PrivacyCategoryResult
                {
                    Category = PrivacyScanCategory.Game,
                    Version = PrivacyCoverage.Version(PrivacyScanCategory.Game, new AppSettings().PrivacyModel),
                    State = PrivacyScanState.Completed, Findings = [new("game", "medium", "fixture", [])]
                }]
            };

            history.SetPrivacyResults(new Dictionary<string, PrivacyScanRecord>
            {
                [entries[2].Id] = risk,
                [entries[3].Id] = risk with
                {
                    CaptureId = entries[3].Id, Disposition = PrivacyDisposition.FalsePositive
                }
            }, new AppSettings());

            var selector = UiControls.Field<CaptureFilterSelector>(history, "_captureFilter");

            var filter = Enum.Parse<CaptureFilter>(mode);

            selector.SetFilter(CaptureFilter.Flagged);

            // Act
            selector.SetFilter(filter);

            // Assert
            Assert.Equal(count, UiControls.Field<CaptureGallery>(history, "_gallery").Entries.Count);

            Assert.Equal(filter, selector.SelectedFilter);
        });
    }

    [Fact]
    public void Date_range_is_inclusive_and_clears_selection_even_when_the_results_overlap()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var day = new DateTime(2026, 10, 13);

        var entries = Enumerable.Range(0, 4).Select(index => workspace.ImageCapture(index + ".jpg") with
        {
            CapturedUtc = day.AddDays(index).AddHours(12).ToUniversalTime()
        }).ToArray();

        workspace.Json("index.json", entries);

        StaThread.Run(() =>
        {
            using var history = new HistoryForm(new ScreenshotStore(workspace.Root));

            var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

            gallery.SelectAll(true);

            // Act
            UiControls.Field<DateFilterControl>(history, "_dateFilter")
                .SetRange(new DateRange(day.AddDays(2), day.AddDays(1)));

            // Assert
            Assert.Equal(entries.Skip(1).Take(2).Select(entry => entry.Id).Order(),
                gallery.Entries.Select(entry => entry.Id).Order());

            Assert.Empty(gallery.SelectedEntries);
        });
    }

    [Fact]
    public void Filter_changes_clear_selection_and_an_empty_result_disables_select_all()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("index.json", new[] { workspace.ImageCapture(viewed: true) });

        StaThread.Run(() =>
        {
            using var history = new HistoryForm(new ScreenshotStore(workspace.Root));

            var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

            gallery.SelectAll(true);

            // Act
            UiControls.Field<CaptureFilterSelector>(history, "_captureFilter").SetFilter(CaptureFilter.Unread);

            // Assert
            Assert.Empty(gallery.Entries);

            Assert.Empty(gallery.SelectedEntries);

            var bar = UiControls.Field<GalleryActionBar>(history, "_galleryActions");

            Assert.False(UiControls.Field<CheckBox>(bar, "_all").Enabled);

            Assert.False(UiControls.Field<Button>(bar, "_read").Enabled);
        });
    }

    [Fact]
    public void Selection_checkbox_and_space_do_not_open_a_capture_but_enter_does()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("index.json", new[] { workspace.ImageCapture() });

        StaThread.Run(() =>
        {
            using var history = new HistoryForm(new ScreenshotStore(workspace.Root))
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(history);

            var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

            var checkbox = gallery.SelectionBounds(0);

            // Act
            UiControls.Invoke(gallery, "OnMouseClick",
                new MouseEventArgs(MouseButtons.Left, 1, checkbox.X + 2, checkbox.Y + 2, 0));

            // Assert
            Assert.Single(gallery.SelectedEntries);

            Assert.Null(UiControls.Field<CaptureEntry?>(history, "selected"));

            // Act
            UiControls.Invoke(gallery, "OnKeyDown", new KeyEventArgs(Keys.Space));

            // Assert
            Assert.Empty(gallery.SelectedEntries);

            Assert.Null(UiControls.Field<CaptureEntry?>(history, "selected"));

            // Act
            UiControls.Invoke(gallery, "OnKeyDown", new KeyEventArgs(Keys.Enter));

            // Assert
            Assert.NotNull(UiControls.Field<CaptureEntry?>(history, "selected"));

            Assert.False(UiControls.Field<Panel>(history, "_galleryPage").Visible);
        });
    }

    [Fact]
    public void Closing_history_hides_it_and_explicit_shutdown_disposes_it()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        StaThread.Run(() =>
        {
            using var history = new HistoryForm(new ScreenshotStore(workspace.Root))
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(history);

            // Act
            history.Close();

            // Assert
            Assert.False(history.Visible);

            Assert.False(history.IsDisposed);

            // Act
            history.AllowClose = true;

            history.Close();

            // Assert
            Assert.True(history.IsDisposed);
        });
    }

    [Fact]
    public void Control_A_and_escape_select_and_clear_results_without_opening_an_image()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        workspace.Json("index.json", new[] { workspace.ImageCapture("a.jpg"), workspace.ImageCapture("b.jpg") });

        StaThread.Run(() =>
        {
            using var history = new HistoryForm(new ScreenshotStore(workspace.Root))
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(history);

            var gallery = UiControls.Field<CaptureGallery>(history, "_gallery");

            // Act
            var selected = UiControls.Invoke(history, "ProcessCmdKey", new Message(), Keys.Control | Keys.A);

            // Assert
            Assert.Equal(true, selected);

            Assert.Equal(2, gallery.SelectedEntries.Count);

            Assert.Null(UiControls.Field<CaptureEntry?>(history, "selected"));

            // Act
            var cleared = UiControls.Invoke(history, "ProcessCmdKey", new Message(), Keys.Escape);

            // Assert
            Assert.Equal(true, cleared);

            Assert.Empty(gallery.SelectedEntries);
        });
    }
}
