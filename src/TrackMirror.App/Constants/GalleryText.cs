// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class GalleryText
{
    internal static string SelectAllFormat => UiLocalization.Get(nameof(GalleryText), nameof(SelectAllFormat));
    internal static string SelectedFormat => UiLocalization.Get(nameof(GalleryText), nameof(SelectedFormat));
    internal static string SelectCapture => UiLocalization.Get(nameof(GalleryText), nameof(SelectCapture));
    internal static string MarkRead => UiLocalization.Get(nameof(GalleryText), nameof(MarkRead));
    internal static string Scan => UiLocalization.Get(nameof(GalleryText), nameof(Scan));
    internal static string Clear => UiLocalization.Get(nameof(GalleryText), nameof(Clear));
    internal static string More => UiLocalization.Get(nameof(GalleryText), nameof(More));
    internal static string Undo => UiLocalization.Get(nameof(GalleryText), nameof(Undo));
    internal static string ReadFormat => UiLocalization.Get(nameof(GalleryText), nameof(ReadFormat));
    internal static string UndoCompleted => UiLocalization.Get(nameof(GalleryText), nameof(UndoCompleted));
    internal static string ScanTitleFormat => UiLocalization.Get(nameof(GalleryText), nameof(ScanTitleFormat));
    internal static string ScanHint => UiLocalization.Get(nameof(GalleryText), nameof(ScanHint));
    internal static string Recheck => UiLocalization.Get(nameof(GalleryText), nameof(Recheck));
    internal static string MissingOnly => UiLocalization.Get(nameof(GalleryText), nameof(MissingOnly));
    internal static string StartScan => UiLocalization.Get(nameof(GalleryText), nameof(StartScan));
    internal static string OpenSettings => UiLocalization.Get(nameof(GalleryText), nameof(OpenSettings));
    internal static string NoCategories => UiLocalization.Get(nameof(GalleryText), nameof(NoCategories));
    internal static string SelectHint => UiLocalization.Get(nameof(GalleryText), nameof(SelectHint));
    internal const string MoreGlyph = "⋯";
    internal const string ExpandSuffix = " ▾";
}
