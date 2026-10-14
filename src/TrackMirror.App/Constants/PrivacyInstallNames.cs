// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class PrivacyInstallNames
{
    internal const string RuntimeUrl =
        "https://github.com/ollama/ollama/releases/download/v0.40.3/ollama-windows-amd64.zip";
    internal const string RuntimeHash = "e5ecff02dfc7cae8ddf6d6b7a7ac5f727ffeedf4491f44ae75ff5b0820dbbbd5";
    internal const string DownloadsDirectory = "privacy-downloads";
    internal const string ArchiveFile = "ollama.zip.part";
    internal const string StagingPrefix = "privacy-runtime-installing-";
    internal const string LockFile = "privacy-install.lock";
    internal const string RuntimePendingFile = "runtime-install.pending";
    internal const string ModelPendingPrefix = "model-install-";
    internal const string RemovalPendingPrefix = "model-remove-";
    internal const string PendingSuffix = ".pending";
    internal const string LocalEndpoint = "http://127.0.0.1:11438";
    internal const int LocalPort = 11438;
    internal const string PullPath = "/api/pull";
    internal const string DeletePath = "/api/delete";
    internal const string Status = "status";
    internal const string Completed = "completed";
    internal const string Total = "total";
    internal const string Success = "success";
    internal const string DownloadError = "install_download_failed";
    internal const string IntegrityError = "install_integrity_failed";
    internal const string ArchiveError = "install_archive_failed";
    internal const string StorageError = "install_storage_failed";
    internal const string BusyError = "install_busy";
    internal const string ModelError = "install_model_failed";
    internal const string RemoveError = "model_remove_failed";
    internal const string AllFiles = "*";
    internal const string HostSeparator = ":";
}
