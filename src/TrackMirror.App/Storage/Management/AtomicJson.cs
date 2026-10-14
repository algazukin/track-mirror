// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Constants;

namespace TrackMirror.Storage.Management;

internal static class AtomicJson
{
    internal static T Read<T>(string path, long maximum = 16384)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            if (stream.Length > maximum)
                throw new InvalidDataException();

            return JsonSerializer.Deserialize<T>(stream) ?? throw new InvalidDataException();
        }
        catch (JsonException ex)
        {
            throw new IOException(DataManagementText.InvalidFile, ex);
        }
    }

    internal static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var temporary = path + Guid.NewGuid().ToString(Formats.CompactGuid) + StorageNames.TemporarySuffix;

        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, value);

                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }
        finally
        {
            File.Delete(temporary);
        }
    }
}
