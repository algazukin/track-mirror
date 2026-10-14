// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Globalization;
using TrackMirror.Constants;
using TrackMirror.Models;

namespace TrackMirror.Storage;

internal static class CaptureFileMetadata
{
    internal static bool Valid(CaptureEntry? entry) => entry != null
        && !string.IsNullOrEmpty(entry.Account) && !string.IsNullOrEmpty(entry.FileName)
        && entry.Account is not (StorageNames.CurrentDirectory or StorageNames.ParentDirectory)
        && entry.FileName is not (StorageNames.CurrentDirectory or StorageNames.ParentDirectory)
        && Path.GetFileName(entry.Account) == entry.Account && Path.GetFileName(entry.FileName) == entry.FileName;

    internal static DateTime CaptureTime(string name, DateTime fallback)
    {
        if (name.Length >= 17 && DateTime.TryParseExact(name[..17], DateFormats.CaptureFileName,
            CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var time))
            return time;

        return DateTime.SpecifyKind(fallback, DateTimeKind.Utc);
    }
}
