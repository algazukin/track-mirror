// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class DataManagementText
{
    internal static string Heading => UiLocalization.Get(nameof(DataManagementText), nameof(Heading));
    internal static string Folder => UiLocalization.Get(nameof(DataManagementText), nameof(Folder));
    internal static string Move => UiLocalization.Get(nameof(DataManagementText), nameof(Move));
    internal static string Backup => UiLocalization.Get(nameof(DataManagementText), nameof(Backup));
    internal static string Restore => UiLocalization.Get(nameof(DataManagementText), nameof(Restore));
    internal static string Cancel => UiLocalization.Get(nameof(DataManagementText), nameof(Cancel));
    internal static string Cleanup => UiLocalization.Get(nameof(DataManagementText), nameof(Cleanup));
    internal static string Retention => UiLocalization.Get(nameof(DataManagementText), nameof(Retention));
    internal static string Days => UiLocalization.Get(nameof(DataManagementText), nameof(Days));
    internal static string DaysUnit => UiLocalization.Get(nameof(DataManagementText), nameof(DaysUnit));
    internal static string KeepFlagged => UiLocalization.Get(nameof(DataManagementText), nameof(KeepFlagged));
    internal static string KeepUnread => UiLocalization.Get(nameof(DataManagementText), nameof(KeepUnread));
    internal static string BackupHint => UiLocalization.Get(nameof(DataManagementText), nameof(BackupHint));
    internal static string RetentionHint => UiLocalization.Get(nameof(DataManagementText), nameof(RetentionHint));
    internal static string SelectTarget => UiLocalization.Get(nameof(DataManagementText), nameof(SelectTarget));
    internal static string ArchiveFilter => UiLocalization.Get(nameof(DataManagementText), nameof(ArchiveFilter));
    internal static string Preparing => UiLocalization.Get(nameof(DataManagementText), nameof(Preparing));
    internal static string Copying => UiLocalization.Get(nameof(DataManagementText), nameof(Copying));
    internal static string Checking => UiLocalization.Get(nameof(DataManagementText), nameof(Checking));
    internal static string Restoring => UiLocalization.Get(nameof(DataManagementText), nameof(Restoring));
    internal static string Packing => UiLocalization.Get(nameof(DataManagementText), nameof(Packing));
    internal static string CleanupCancelled => UiLocalization.Get(nameof(DataManagementText), nameof(CleanupCancelled));
    internal static string SelectedFolderBusy =>
        UiLocalization.Get(nameof(DataManagementText), nameof(SelectedFolderBusy));
    internal static string ProtectedFolder => UiLocalization.Get(nameof(DataManagementText), nameof(ProtectedFolder));
    internal static string Cleaning => UiLocalization.Get(nameof(DataManagementText), nameof(Cleaning));
    internal static string Completed => UiLocalization.Get(nameof(DataManagementText), nameof(Completed));
    internal static string UncommittedCopy => UiLocalization.Get(nameof(DataManagementText), nameof(UncommittedCopy));
    internal static string Cancelled => UiLocalization.Get(nameof(DataManagementText), nameof(Cancelled));
    internal static string FailedPrefix => UiLocalization.Get(nameof(DataManagementText), nameof(FailedPrefix));
    internal static string InvalidFile => UiLocalization.Get(nameof(DataManagementText), nameof(InvalidFile));
    internal static string UnavailableFolder =>
        UiLocalization.Get(nameof(DataManagementText), nameof(UnavailableFolder));
    internal static string InvalidRedirect => UiLocalization.Get(nameof(DataManagementText), nameof(InvalidRedirect));
    internal static string RedirectCycle => UiLocalization.Get(nameof(DataManagementText), nameof(RedirectCycle));
    internal static string LocalFolderOnly => UiLocalization.Get(nameof(DataManagementText), nameof(LocalFolderOnly));
    internal static string Overlap => UiLocalization.Get(nameof(DataManagementText), nameof(Overlap));
    internal static string EmptyTarget => UiLocalization.Get(nameof(DataManagementText), nameof(EmptyTarget));
    internal static string UnsafePath => UiLocalization.Get(nameof(DataManagementText), nameof(UnsafePath));
    internal static string InvalidArchive => UiLocalization.Get(nameof(DataManagementText), nameof(InvalidArchive));
    internal static string InvalidDatabase => UiLocalization.Get(nameof(DataManagementText), nameof(InvalidDatabase));
    internal static string Busy => UiLocalization.Get(nameof(DataManagementText), nameof(Busy));
    internal static string Unsaved => UiLocalization.Get(nameof(DataManagementText), nameof(Unsaved));
    internal static string ProgressFormat => UiLocalization.Get(nameof(DataManagementText), nameof(ProgressFormat));
    internal static string CleanupFormat => UiLocalization.Get(nameof(DataManagementText), nameof(CleanupFormat));
    internal static string CleanupConfirmation =>
        UiLocalization.Get(nameof(DataManagementText), nameof(CleanupConfirmation));
    internal static string NothingToClean => UiLocalization.Get(nameof(DataManagementText), nameof(NothingToClean));
    internal static string InvalidRetention => UiLocalization.Get(nameof(DataManagementText), nameof(InvalidRetention));
}
