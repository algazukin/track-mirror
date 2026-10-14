// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class DataManagementNames
{
    internal const string ApplicationFolder = "TrackMirror";
    internal const string Installations = "installations";
    internal const string LocationFile = "data-location.json";
    internal const string RedirectFile = ".viewer-data-redirect.json";
    internal const string MarkerFile = ".viewer-data.json";
    internal const string LockFile = ".viewer-data.lock";
    internal const string StagingPrefix = ".viewer-stage-";
    internal const string ManifestFile = "manifest.json";
    internal const string BackupSignature = "TrackMirror.Backup";
    internal const string BackupExtension = ".zip";
    internal const string BackupNameFormat = "TrackMirror-backup-{0:yyyyMMdd-HHmmss}.zip";
    internal const int ArchiveVersion = 1;
    internal const int DefaultDays = 30;
    internal const int MaximumDays = 3650;
    internal const int CopyBuffer = 131072;
    internal const long MaximumManifestBytes = 16 * 1024 * 1024;
    internal const long MaximumBackupBytes = 1024L * 1024 * 1024 * 1024;
    internal static readonly IReadOnlySet<string> DeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        "COM¹", "COM²", "COM³", "LPT¹", "LPT²", "LPT³"
    };
}
