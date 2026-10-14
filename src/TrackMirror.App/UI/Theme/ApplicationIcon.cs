// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.UI.Theme;

internal static class ApplicationIcon
{
    internal static readonly Icon Main = Load();
    private static Icon Load()
    {
        using var stream = typeof(ApplicationIcon).Assembly.GetManifestResourceStream(
            ApplicationConstants.IconResourceName
        ) ?? throw new InvalidOperationException(
            Messages.IconResourceMissing
        );

        return new Icon(stream);
    }
}
