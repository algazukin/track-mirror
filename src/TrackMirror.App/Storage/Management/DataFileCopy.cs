// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Security.Cryptography;
using TrackMirror.Constants;

namespace TrackMirror.Storage.Management;

internal static class DataFileCopy
{
    internal static async Task<DataBackupFile> CopyAsync(Stream input, string destination, string relative,
        CancellationToken token, Action<long>? copied = null, long maximum = DataManagementNames.MaximumBackupBytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write,
            FileShare.None, DataManagementNames.CopyBuffer, useAsync: true);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        var buffer = new byte[DataManagementNames.CopyBuffer];

        long length = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, token).ConfigureAwait(false);

            if (read == 0)
                break;

            length = checked(length + read);

            if (length > maximum)
                throw new IOException(DataManagementText.InvalidArchive);

            hash.AppendData(buffer, 0, read);

            await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);

            copied?.Invoke(read);
        }

        await output.FlushAsync(token).ConfigureAwait(false);

        return new DataBackupFile(relative, length, Convert.ToHexString(hash.GetHashAndReset()));
    }
}
