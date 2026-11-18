// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Localization;
using TrackMirror.UI.Controls;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class DiagnosticsSettingsTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void Export_is_explicit_reentry_is_blocked_and_cancel_does_not_write(string language)
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize(language);
            var calls = 0;
            var destination = (string?)null;
            var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var panel = new DiagnosticsSettingsPanel(_ => destination, _ => { calls++; return pending.Task; }, true);

            // Assert
            Assert.Equal(0, calls);

            // Act
            panel.ExportAsync().GetAwaiter().GetResult();
            destination = "synthetic.zip";
            var running = panel.ExportAsync();
            panel.ExportAsync().GetAwaiter().GetResult();

            // Assert
            Assert.Equal(1, calls);
            Assert.False(UiControls.Descendants<Button>(panel).Single().Enabled);

            // Act
            pending.SetResult();
            var deadline = System.Diagnostics.Stopwatch.StartNew();

            while (!running.IsCompleted && deadline.Elapsed < TimeSpan.FromSeconds(5))
            {
                Application.DoEvents();
                Thread.Sleep(1);
            }

            Assert.True(running.IsCompleted);
            running.GetAwaiter().GetResult();

            // Assert
            Assert.True(UiControls.Descendants<Button>(panel).Single().Enabled);
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text == DiagnosticsText.Completed);
        });
    }

    [Fact]
    public void Export_errors_show_a_generic_message_without_echoing_private_exception_text()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var panel = new DiagnosticsSettingsPanel(_ => "fixture.zip",
                _ => Task.FromException(new IOException("private-path-and-secret")), true);

            // Act
            panel.ExportAsync().GetAwaiter().GetResult();

            // Assert
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text == DiagnosticsText.Failed);
            Assert.DoesNotContain(UiControls.Descendants<Label>(panel), label => label.Text.Contains("private-path"));
            Assert.True(UiControls.Descendants<Button>(panel).Single().Enabled);
        });
    }
}
