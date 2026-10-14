// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using Xunit;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal sealed class DesktopTheoryAttribute : TheoryAttribute
{
    public DesktopTheoryAttribute()
    {
        if (Environment.GetEnvironmentVariable("HUBSTAFF_VIEWER_DESKTOP_TESTS") != "1")
        {
            Skip = "Requires a dedicated interactive desktop: set HUBSTAFF_VIEWER_DESKTOP_TESTS=1.";
        }
    }
}
