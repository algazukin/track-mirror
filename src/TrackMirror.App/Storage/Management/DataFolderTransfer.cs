// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.Storage.Management;

internal static class DataFolderTransfer
{
    internal static async Task<string> MoveAsync(string source, string target, CancellationToken token,
        IProgress<DataOperationProgress>? progress = null)
    {
        source = DataFolderRules.Canonical(source);

        target = DataFolderRules.Canonical(target);

        DataFolderRules.EmptyTarget(source, target);

        using var lease = new FileStream(Path.Combine(source, DataManagementNames.LockFile), FileMode.OpenOrCreate,
            FileAccess.ReadWrite, FileShare.None);

        using var staging = new OwnedDataStaging(Path.GetDirectoryName(target)!);

        await DataSnapshot.CreateAsync(source, staging.Root, token, progress).ConfigureAwait(false);

        token.ThrowIfCancellationRequested();

        staging.Promote(source, target);

        // The verified destination exists before this atomic redirect becomes the switch point.
        token.ThrowIfCancellationRequested();

        try
        {
            AtomicJson.Write(Path.Combine(source, DataManagementNames.RedirectFile),
                new DataLocationRecord(target, staging.TransferId));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new IOException(DataManagementText.UncommittedCopy, ex);
        }

        return target;
    }
}
