// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

public sealed class ApplicationLifecycleTests
{
    [Theory]
    [InlineData("shutdown")]
    [InlineData("shutdown-scan")]
    [InlineData("shutdown-download")]
    [InlineData("batch")]
    [InlineData("undo-expiry")]
    [InlineData("notification-timeout")]
    [InlineData("notification-replacement")]
    [InlineData("notifications-disabled")]
    public async Task Synthetic_application_scenario_finishes_without_hanging_or_changing_real_data(string scenario)
    {
        // Arrange
        // The child owns its fixture folders, windows and workers, including its shutdown watchdog.

        // Act
        var failure = await Record.ExceptionAsync(() => UiScenarioProcess.RunAsync(scenario));

        // Assert
        Assert.Null(failure);
    }

    [DesktopTheory]
    [InlineData("preview-startup")]
    [InlineData("preview-hidden")]
    [InlineData("preview-minimized")]
    [InlineData("focus")]
    [InlineData("window-reuse")]
    [InlineData("shutdown-windows")]
    public async Task Interactive_window_regression_passes_in_a_dedicated_desktop(string scenario)
    {
        // Arrange
        // Explicit opt-in keeps activation/focus checks away from an ordinary developer desktop.

        // Act
        var failure = await Record.ExceptionAsync(() => UiScenarioProcess.RunAsync(scenario));

        // Assert
        Assert.Null(failure);
    }
}
