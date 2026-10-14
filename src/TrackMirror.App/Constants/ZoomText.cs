// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class ZoomText
{
    internal static string Image => UiLocalization.Get(nameof(ZoomText), nameof(Image));
    internal static string Fit => UiLocalization.Get(nameof(ZoomText), nameof(Fit));
    internal const string ActualSize = "100%";
    internal const string ZoomIn = "+";
    internal const string ZoomOut = "−";
    internal static string ZoomInName => UiLocalization.Get(nameof(ZoomText), nameof(ZoomInName));
    internal static string ZoomOutName => UiLocalization.Get(nameof(ZoomText), nameof(ZoomOutName));
    internal const string ScaleFormat = "{0:0.#}%";
    internal static string Hint => UiLocalization.Get(nameof(ZoomText), nameof(Hint));
    internal static string FitHint => UiLocalization.Get(nameof(ZoomText), nameof(FitHint));
    internal static string ActualSizeHint => UiLocalization.Get(nameof(ZoomText), nameof(ActualSizeHint));
    internal static string ZoomInHint => UiLocalization.Get(nameof(ZoomText), nameof(ZoomInHint));
    internal static string ZoomOutHint => UiLocalization.Get(nameof(ZoomText), nameof(ZoomOutHint));
}
