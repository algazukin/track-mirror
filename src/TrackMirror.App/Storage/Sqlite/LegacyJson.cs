// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Constants;

namespace TrackMirror.Storage.Sqlite;

internal static class LegacyJson
{
    internal static List<T> Read<T>(string root, string name, long maximum = 128 * 1024 * 1024)
    {
        var path = Path.Combine(root, name);

        if (!File.Exists(path))
            return [];

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);

            if (stream.Length > maximum)
                throw new InvalidDataException();

            return JsonSerializer.Deserialize<List<T>>(stream) ?? throw new InvalidDataException();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            throw new IOException(string.Format(DatabaseText.LegacyFailedFormat, name), ex);
        }
    }
}
