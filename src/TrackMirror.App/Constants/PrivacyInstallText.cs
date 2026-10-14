// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Constants;

internal static class PrivacyInstallText
{
    internal static string Model => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Model));
    internal static string ModelScroll => UiLocalization.Get(nameof(PrivacyInstallText), nameof(ModelScroll));
    internal static string NoModel => UiLocalization.Get(nameof(PrivacyInstallText), nameof(NoModel));
    internal static string Qwen2B => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Qwen2B));
    internal static string Qwen4B => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Qwen4B));
    internal static string Qwen8B => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Qwen8B));
    internal static string Gemma4B => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Gemma4B));
    internal static string Install => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Install));
    internal static string Cancel => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Cancel));
    internal static string CancelOperation => UiLocalization.Get(nameof(PrivacyInstallText), nameof(CancelOperation));
    internal static string Remove => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Remove));
    internal static string Removing => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Removing));
    internal static string Removed => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Removed));
    internal static string RemoveError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(RemoveError));
    internal static string RemovalIncomplete =>
        UiLocalization.Get(nameof(PrivacyInstallText), nameof(RemovalIncomplete));
    internal static string Installed => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Installed));
    internal static string Missing => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Missing));
    internal static string DownloadHint => UiLocalization.Get(nameof(PrivacyInstallText), nameof(DownloadHint));
    internal static string RuntimeDownload => UiLocalization.Get(nameof(PrivacyInstallText), nameof(RuntimeDownload));
    internal static string RuntimeVerify => UiLocalization.Get(nameof(PrivacyInstallText), nameof(RuntimeVerify));
    internal static string RuntimeExtract => UiLocalization.Get(nameof(PrivacyInstallText), nameof(RuntimeExtract));
    internal static string ModelDownload => UiLocalization.Get(nameof(PrivacyInstallText), nameof(ModelDownload));
    internal static string ModelVerify => UiLocalization.Get(nameof(PrivacyInstallText), nameof(ModelVerify));
    internal static string Ready => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Ready));
    internal static string Cancelled => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Cancelled));
    internal static string Failed => UiLocalization.Get(nameof(PrivacyInstallText), nameof(Failed));
    internal static string ProgressFormat => UiLocalization.Get(nameof(PrivacyInstallText), nameof(ProgressFormat));
    internal static string DownloadError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(DownloadError));
    internal static string IntegrityError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(IntegrityError));
    internal static string ArchiveError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(ArchiveError));
    internal static string StorageError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(StorageError));
    internal static string BusyError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(BusyError));
    internal static string ModelError => UiLocalization.Get(nameof(PrivacyInstallText), nameof(ModelError));
    internal static string InvalidSavedModel =>
        UiLocalization.Get(nameof(PrivacyInstallText), nameof(InvalidSavedModel));

    internal static string Describe(PrivacyInstallProgress progress)
    {
        var text = progress.Stage switch
        {
            PrivacyInstallStage.RuntimeDownload => RuntimeDownload,
            PrivacyInstallStage.RuntimeVerify => RuntimeVerify,
            PrivacyInstallStage.RuntimeExtract => RuntimeExtract,
            PrivacyInstallStage.ModelDownload => ModelDownload,
            PrivacyInstallStage.ModelVerify => ModelVerify,
            PrivacyInstallStage.ModelRemove => Removing,
            PrivacyInstallStage.Removed => Removed,
            PrivacyInstallStage.Ready => Ready,
            PrivacyInstallStage.Cancelled => Cancelled,
            PrivacyInstallStage.Failed => Failure(progress.FailureCode),
            _ => string.Empty
        };

        return progress.Total > 0 ? string.Format(ProgressFormat, text,
            progress.Completed / 1e9, progress.Total / 1e9) : text;
    }

    private static string Failure(string code) => code switch
    {
        PrivacyInstallNames.DownloadError => DownloadError,
        PrivacyInstallNames.IntegrityError => IntegrityError,
        PrivacyInstallNames.ArchiveError => ArchiveError,
        PrivacyInstallNames.StorageError => StorageError,
        PrivacyInstallNames.BusyError => BusyError,
        PrivacyInstallNames.ModelError => ModelError,
        PrivacyInstallNames.RemoveError => RemoveError,
        _ => PrivacyText.Failure(code)
    };
}
