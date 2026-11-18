// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class DiagnosticsText
{
    internal static string Heading => UiLocalization.Get(nameof(DiagnosticsText), nameof(Heading));
    internal static string Hint => UiLocalization.Get(nameof(DiagnosticsText), nameof(Hint));
    internal static string Export => UiLocalization.Get(nameof(DiagnosticsText), nameof(Export));
    internal static string Exporting => UiLocalization.Get(nameof(DiagnosticsText), nameof(Exporting));
    internal static string Completed => UiLocalization.Get(nameof(DiagnosticsText), nameof(Completed));
    internal static string Failed => UiLocalization.Get(nameof(DiagnosticsText), nameof(Failed));
    internal static string Unavailable => UiLocalization.Get(nameof(DiagnosticsText), nameof(Unavailable));
    internal static string UnexpectedError => UiLocalization.Get(nameof(DiagnosticsText), nameof(UnexpectedError));
}
