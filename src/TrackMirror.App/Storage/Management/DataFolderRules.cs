// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;

namespace TrackMirror.Storage.Management;

internal static class DataFolderRules
{
    internal static string Canonical(string path) =>
        Path.GetFullPath(Path.Combine(path, StorageNames.CurrentDirectory));

    internal static void CheckLocal(string path)
    {
        var full = Canonical(path);

        var root = Path.GetPathRoot(full)!;

        if (root.StartsWith(new string(Path.DirectorySeparatorChar, 2), StringComparison.Ordinal)
            || new DriveInfo(root).DriveType == DriveType.Network)
            throw new IOException(DataManagementText.LocalFolderOnly);

        for (var current = new DirectoryInfo(full); current != null; current = current.Parent)
        {
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new IOException(DataManagementText.LocalFolderOnly);

            if (current.Parent != null && !SafeSegment(current.Name))
                throw new IOException(DataManagementText.UnsafePath);
        }
    }

    internal static bool Overlaps(string first, string second)
    {
        var left = Canonical(first);

        var right = Canonical(second);

        var leftPrefix = left.EndsWith(Path.DirectorySeparatorChar) ? left : left + Path.DirectorySeparatorChar;

        var rightPrefix = right.EndsWith(Path.DirectorySeparatorChar) ? right : right + Path.DirectorySeparatorChar;

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
            || left.StartsWith(rightPrefix, StringComparison.OrdinalIgnoreCase)
            || right.StartsWith(leftPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static void EmptyTarget(string source, string target)
    {
        CheckLocal(source);

        CheckLocal(target);

        if (Overlaps(source, target))
            throw new IOException(DataManagementText.Overlap);

        if (File.Exists(target) || Directory.Exists(target) && Directory.EnumerateFileSystemEntries(target).Any())
            throw new IOException(DataManagementText.EmptyTarget);
    }

    internal static string OwnedFile(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(Path.VolumeSeparatorChar)
            || relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => !SafeSegment(part)))
            throw new IOException(DataManagementText.UnsafePath);

        var directory = Canonical(root);

        var full = Path.GetFullPath(Path.Combine(directory, relative));

        var prefix = directory.EndsWith(Path.DirectorySeparatorChar)
            ? directory : directory + Path.DirectorySeparatorChar;

        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new IOException(DataManagementText.UnsafePath);

        CheckLocal(Path.GetDirectoryName(full)!);

        if (File.Exists(full) && File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
            throw new IOException(DataManagementText.UnsafePath);

        return full;
    }

    internal static bool SafeSegment(string value) => !string.IsNullOrEmpty(value)
        && !value.EndsWith(' ') && !value.EndsWith('.') && value.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && !DataManagementNames.DeviceNames.Contains(value.Split('.')[0]);

    internal static string CapturePath(string root, CaptureEntry entry)
    {
        if (!CaptureFileMetadata.Valid(entry))
            throw new IOException(DataManagementText.UnsafePath);

        return OwnedFile(root, Path.Combine(StorageNames.CapturesDirectory, entry.Account, entry.FileName));
    }
}
