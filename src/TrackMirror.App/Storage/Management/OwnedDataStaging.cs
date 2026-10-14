// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.Storage.Management;

internal sealed class OwnedDataStaging : IDisposable
{
    internal string Root { get; }

    internal Guid TransferId { get; } = Guid.NewGuid();

    internal OwnedDataStaging(string parent)
    {
        DataFolderRules.CheckLocal(parent);

        Directory.CreateDirectory(parent);

        Root = Path.Combine(parent, DataManagementNames.StagingPrefix + TransferId.ToString(Formats.CompactGuid));

        Directory.CreateDirectory(Root);

        AtomicJson.Write(Path.Combine(Root, DataManagementNames.MarkerFile), new DataFolderMarker(TransferId));
    }

    internal void Promote(string source, string target)
    {
        DataFolderRules.EmptyTarget(source, target);

        if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: false);
        }

        Directory.Move(Root, target);
    }

    public void Dispose()
    {
        if (!Directory.Exists(Root))
            return;

        DataFolderRules.CheckLocal(Root);

        if (Path.GetFileName(Root) != DataManagementNames.StagingPrefix + TransferId.ToString(Formats.CompactGuid)
            || AtomicJson.Read<DataFolderMarker>(Path.Combine(Root, DataManagementNames.MarkerFile))
                .TransferId != TransferId)
            throw new IOException(DataManagementText.UnsafePath);

        foreach (var path in Directory.EnumerateFileSystemEntries(Root, StorageNames.AllFiles,
            SearchOption.AllDirectories))
        {
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                throw new IOException(DataManagementText.UnsafePath);
        }

        Directory.Delete(Root, recursive: true);
    }
}
