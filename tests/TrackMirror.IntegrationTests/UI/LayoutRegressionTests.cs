// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

public sealed class LayoutRegressionTests
{
    [Theory]
    [InlineData("en", "96")]
    [InlineData("ru", "96")]
    [InlineData("en", "native")]
    [InlineData("ru", "native")]
    public async Task English_and_Russian_layouts_fit_at_96_DPI_and_the_actual_desktop_DPI(string language, string dpi)
    {
        // Arrange
        // Each DPI mode starts in its own process before any WinForms handles are created.

        // Act
        var failure = await Record.ExceptionAsync(() => UiScenarioProcess.RunAsync("layout", language, dpi));

        // Assert
        Assert.Null(failure);
    }
}
