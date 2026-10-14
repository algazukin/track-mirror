// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.Storage;
using TrackMirror.UI.Forms;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

public sealed class CaptureAvailabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recovering_a_missing_copy_clears_its_warning_and_preserves_other_errors(bool otherError)
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var workspace = new TestWorkspace();

            var capture = workspace.Capture();

            workspace.Json("index.json", new[] { capture });

            var path = Path.Combine(workspace.Root, "captures", capture.Account, capture.FileName);

            File.Delete(path);

            var store = new ScreenshotStore(workspace.Root);

            using var history = new HistoryForm(store);

            if (store.InitialWarning != null)
            {
                history.SetWarning(store.InitialWarning);
            }

            var warning = UiControls.Field<Label>(history, "_warning");

            Assert.NotEmpty(warning.Text);

            const string failure = "Synthetic database write failure.";

            if (otherError)
            {
                history.SetWarning(failure);
            }

            // Act
            File.WriteAllBytes(path, [0xff, 0xd8, 0xff, 0xd9]);

            store.Add(capture.Account, capture.FileName, capture.CapturedUtc, false);

            history.RefreshEntries();

            // Assert
            Assert.Equal(otherError ? failure : string.Empty, warning.Text);

            Assert.Equal(capture.Id, Assert.Single(store.Snapshot()).Id);
        });
    }
}
