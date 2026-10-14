// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class StorageNames
{
    internal const string DataDirectory = "data";
    internal const string CapturesDirectory = "captures";
    internal const string ScreensDirectory = "screens";
    internal const string IndexFile = "index.json";
    internal const string SettingsFile = "settings.json";
    internal const string LegacyPrivacyPersonalActivity = "PrivacyPersonalActivity";
    internal const string StartupErrorFile = "startup-error.log";
    internal const string ScreenshotPattern = "*.jpg";
    internal const string AllFiles = "*";
    internal const string ScreenshotExtension = ".jpg";
    internal const string ThumbnailSuffix = "-thumb.jpg";
    internal const string PartialSuffix = ".part";
    internal const string TemporarySuffix = ".tmp";
    internal const string EntrySeparator = "/";
    internal const string CurrentDirectory = ".";
    internal const string ParentDirectory = "..";
}
