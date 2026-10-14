// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class UiText
{
    internal const string FontFamily = "Segoe UI";
    internal static string HistoryWindowTitle => UiLocalization.Get(nameof(UiText), nameof(HistoryWindowTitle));
    internal static string PreviewWindowTitle => UiLocalization.Get(nameof(UiText), nameof(PreviewWindowTitle));
    internal static string SelectCapture => UiLocalization.Get(nameof(UiText), nameof(SelectCapture));
    internal static string WaitingForCaptures => UiLocalization.Get(nameof(UiText), nameof(WaitingForCaptures));
    internal static string SelectedCapturePlaceholder =>
        UiLocalization.Get(nameof(UiText), nameof(SelectedCapturePlaceholder));
    internal static string FlaggedFilter => UiLocalization.Get(nameof(UiText), nameof(FlaggedFilter));
    internal static string AllFilter => UiLocalization.Get(nameof(UiText), nameof(AllFilter));
    internal static string HistoryFilters => UiLocalization.Get(nameof(UiText), nameof(HistoryFilters));
    internal static string VerticalScroll => UiLocalization.Get(nameof(UiText), nameof(VerticalScroll));
    internal static string HorizontalScroll => UiLocalization.Get(nameof(UiText), nameof(HorizontalScroll));
    internal static string UnreadFilter => UiLocalization.Get(nameof(UiText), nameof(UnreadFilter));
    internal static string CaptureScrollBar => UiLocalization.Get(nameof(UiText), nameof(CaptureScrollBar));
    internal static string Gallery => UiLocalization.Get(nameof(UiText), nameof(Gallery));
    internal static string BackToGallery => UiLocalization.Get(nameof(UiText), nameof(BackToGallery));
    internal static string LoadingThumbnail => UiLocalization.Get(nameof(UiText), nameof(LoadingThumbnail));
    internal static string GalleryDescription => UiLocalization.Get(nameof(UiText), nameof(GalleryDescription));
    internal static string MarkReviewed => UiLocalization.Get(nameof(UiText), nameof(MarkReviewed));
    internal static string Flag => UiLocalization.Get(nameof(UiText), nameof(Flag));
    internal static string OpenFullSize => UiLocalization.Get(nameof(UiText), nameof(OpenFullSize));
    internal static string HistoryHeading => UiLocalization.Get(nameof(UiText), nameof(HistoryHeading));
    internal static string HistoryDescription => UiLocalization.Get(nameof(UiText), nameof(HistoryDescription));
    internal static string CopiesFolder => UiLocalization.Get(nameof(UiText), nameof(CopiesFolder));
    internal static string CapturesHeading => UiLocalization.Get(nameof(UiText), nameof(CapturesHeading));
    internal static string Reviewed => UiLocalization.Get(nameof(UiText), nameof(Reviewed));
    internal static string New => UiLocalization.Get(nameof(UiText), nameof(New));
    internal static string FlaggedSuffix => UiLocalization.Get(nameof(UiText), nameof(FlaggedSuffix));
    internal const string Separator = " · ";
    internal const string WideSeparator = "  ·  ";
    internal const string StatusPrefix = "●  ";
    internal const string FlagPrefix = "★ ";
    internal static string NoFlaggedCaptures => UiLocalization.Get(nameof(UiText), nameof(NoFlaggedCaptures));
    internal static string NoUnreadCaptures => UiLocalization.Get(nameof(UiText), nameof(NoUnreadCaptures));
    internal static string NoCaptures => UiLocalization.Get(nameof(UiText), nameof(NoCaptures));
    internal static string NewCapturesPlaceholder => UiLocalization.Get(nameof(UiText), nameof(NewCapturesPlaceholder));
    internal static string FlaggedCapturesPlaceholder =>
        UiLocalization.Get(nameof(UiText), nameof(FlaggedCapturesPlaceholder));
    internal static string WaitingForFirstCapture => UiLocalization.Get(nameof(UiText), nameof(WaitingForFirstCapture));
    internal static string Unflag => UiLocalization.Get(nameof(UiText), nameof(Unflag));
    internal static string FullSizeTitleSuffix => UiLocalization.Get(nameof(UiText), nameof(FullSizeTitleSuffix));
    internal static string PreviewHeading => UiLocalization.Get(nameof(UiText), nameof(PreviewHeading));
    internal static string Open => UiLocalization.Get(nameof(UiText), nameof(Open));
    internal static string TrayWaiting => UiLocalization.Get(nameof(UiText), nameof(TrayWaiting));
    internal static string TrayHistory => UiLocalization.Get(nameof(UiText), nameof(TrayHistory));
    internal static string Exit => UiLocalization.Get(nameof(UiText), nameof(Exit));
    internal static string WarningTitle => UiLocalization.Get(nameof(UiText), nameof(WarningTitle));
    internal static string HistoryCountsFormat => UiLocalization.Get(nameof(UiText), nameof(HistoryCountsFormat));
    internal static string PreviewUnreadFormat => UiLocalization.Get(nameof(UiText), nameof(PreviewUnreadFormat));
    internal static string TrayUnreadFormat => UiLocalization.Get(nameof(UiText), nameof(TrayUnreadFormat));
    internal static string TrayWarningFormat => UiLocalization.Get(nameof(UiText), nameof(TrayWarningFormat));
    internal static string ActivityWaiting => UiLocalization.Get(nameof(UiText), nameof(ActivityWaiting));
    internal static string ActivityWaitingDetails => UiLocalization.Get(nameof(UiText), nameof(ActivityWaitingDetails));
    internal static string ActivityUnavailable => UiLocalization.Get(nameof(UiText), nameof(ActivityUnavailable));
    internal static string ActivityFormat => UiLocalization.Get(nameof(UiText), nameof(ActivityFormat));
    internal static string ActivityDetailsFormat => UiLocalization.Get(nameof(UiText), nameof(ActivityDetailsFormat));
}
