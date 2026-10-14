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
public sealed class PrivacyRescanTests
{
    [Theory]
    [InlineData("all", true)]
    [InlineData("all", false)]
    [InlineData("game", true)]
    [InlineData("none", true)]
    public void Result_actions_use_current_settings_after_selection_changes(string selection, bool force)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.ImageCapture();

        workspace.Json("index.json", new[] { capture });

        StaThread.Run(() =>
        {
            var initial = new AppSettings();

            var record = new PrivacyScanRecord
            {
                CaptureId = capture.Id, RequestId = "fixture", Profile = PrivacyScanService.Profile(initial),
                ImageHash = "fixture", State = PrivacyScanState.Completed,
                Categories = [new PrivacyCategoryResult
                {
                    Category = PrivacyScanCategory.ConfidentialData,
                    Version = PrivacyCoverage.Version(PrivacyScanCategory.ConfidentialData, initial.PrivacyModel),
                    State = PrivacyScanState.Completed
                }]
            };

            var results = new Dictionary<string, PrivacyScanRecord> { [capture.Id] = record };

            using var history = new HistoryForm(new ScreenshotStore(workspace.Root), initial)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(history);

            history.SetPrivacyResults(results, initial);

            UiControls.Invoke(history, "OpenCapture", capture.Id);

            UiControls.Invoke(history, "OpenPrivacyResult");

            using var window = UiControls.Field<PrivacyResultForm>(history, "privacyWindow");

            window.Opacity = 0;

            window.ShowInTaskbar = false;

            var settings = initial with
            {
                PrivacyGames = selection is "all" or "game",
                PrivacyPersonalChats = selection == "all",
                PrivacyConfidentialData = selection == "all"
            };

            var requests = new List<(IReadOnlyList<CaptureEntry> Entries, bool Force,
                IReadOnlyList<PrivacyScanCategory>? Categories)>();

            history.PrivacyScanRequested += (entries, recheck, categories) => requests.Add((entries, recheck, categories));

            // Act
            history.SetPrivacyResults(results, settings);

            var details = UiControls.Field<PrivacyDetailPanel>(window, "_details");

            UiControls.Field<Button>(details, force ? "_rescan" : "_scan").PerformClick();

            // Assert
            var request = Assert.Single(requests);

            Assert.Equal(capture.Id, Assert.Single(request.Entries).Id);

            Assert.Equal(force, request.Force);

            PrivacyScanCategory[] expected = selection switch
            {
                "all" => [PrivacyScanCategory.Game, PrivacyScanCategory.PersonalChat, PrivacyScanCategory.ConfidentialData],
                "game" => [PrivacyScanCategory.Game],
                _ => [PrivacyScanCategory.ConfidentialData]
            };

            Assert.Equal(expected, request.Categories);
        });
    }
}
