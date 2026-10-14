// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class Messages
{
    internal static string ActivityCacheFailedPrefix =>
        UiLocalization.Get(nameof(Messages), nameof(ActivityCacheFailedPrefix));
    internal static string ActivityHistoryUnreadable =>
        UiLocalization.Get(nameof(Messages), nameof(ActivityHistoryUnreadable));
    internal static string IconResourceMissing => UiLocalization.Get(nameof(Messages), nameof(IconResourceMissing));
    internal static string AlreadyRunning => UiLocalization.Get(nameof(Messages), nameof(AlreadyRunning));
    internal static string InvalidOptionPrefix => UiLocalization.Get(nameof(Messages), nameof(InvalidOptionPrefix));
    internal static string StartupFailedPrefix => UiLocalization.Get(nameof(Messages), nameof(StartupFailedPrefix));
    internal static string WaitingForSource => UiLocalization.Get(nameof(Messages), nameof(WaitingForSource));
    internal static string RestoringMonitor => UiLocalization.Get(nameof(Messages), nameof(RestoringMonitor));
    internal static string WaitingForScreens => UiLocalization.Get(nameof(Messages), nameof(WaitingForScreens));
    internal static string SourceUnavailablePrefix =>
        UiLocalization.Get(nameof(Messages), nameof(SourceUnavailablePrefix));
    internal static string IncompleteImage => UiLocalization.Get(nameof(Messages), nameof(IncompleteImage));
    internal static string CopyFailedPrefix => UiLocalization.Get(nameof(Messages), nameof(CopyFailedPrefix));
    internal const string ErrorSeparator = ": ";
    internal static string SaveFailedPrefix => UiLocalization.Get(nameof(Messages), nameof(SaveFailedPrefix));
    internal static string RecoveredHistory => UiLocalization.Get(nameof(Messages), nameof(RecoveredHistory));
    internal static string IndexSaveFailedPrefix => UiLocalization.Get(nameof(Messages), nameof(IndexSaveFailedPrefix));
    internal static string ImageUnavailablePrefix =>
        UiLocalization.Get(nameof(Messages), nameof(ImageUnavailablePrefix));
    internal static string ImageUnavailable => UiLocalization.Get(nameof(Messages), nameof(ImageUnavailable));
    internal static string OpenFailedPrefix => UiLocalization.Get(nameof(Messages), nameof(OpenFailedPrefix));
    internal static string PreviewUnavailablePrefix =>
        UiLocalization.Get(nameof(Messages), nameof(PreviewUnavailablePrefix));
    internal static string MonitoringAccountsFormat =>
        UiLocalization.Get(nameof(Messages), nameof(MonitoringAccountsFormat));
    internal static string MissingCopiesFormat => UiLocalization.Get(nameof(Messages), nameof(MissingCopiesFormat));
}
