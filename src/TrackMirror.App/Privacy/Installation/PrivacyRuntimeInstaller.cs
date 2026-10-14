// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Installation;

internal sealed class PrivacyRuntimeInstaller : IDisposable
{
    private readonly HttpClient _client;
    private readonly PrivacyRuntimePackage _package;

    internal PrivacyRuntimeInstaller(PrivacyRuntimePackage? package = null, HttpClient? client = null)
    {
        _package = package ?? new PrivacyRuntimePackage();

        _client = client ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    }

    internal async Task EnsureAsync(string root, Action<PrivacyInstallProgress> progress, CancellationToken token)
    {
        if (File.Exists(Path.Combine(root, PrivacyNames.RuntimeExecutable))
            && !File.Exists(Path.Combine(root, PrivacyInstallNames.RuntimePendingFile)))
            return;

        var downloads = Path.Combine(Path.GetDirectoryName(root)!, PrivacyInstallNames.DownloadsDirectory);

        Directory.CreateDirectory(downloads);

        var archive = Path.Combine(downloads, PrivacyInstallNames.ArchiveFile);

        await DownloadAsync(archive, progress, token).ConfigureAwait(false);

        progress(new PrivacyInstallProgress(PrivacyInstallStage.RuntimeVerify));

        using (var file = File.OpenRead(archive))
        {
            var hash = Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false))
                .ToLowerInvariant();

            if (hash != _package.Hash)
            {
                file.Dispose();

                File.Delete(archive);

                throw new PrivacyScanException(PrivacyInstallNames.IntegrityError);
            }
        }

        var staging = Path.Combine(Path.GetDirectoryName(root)!, PrivacyInstallNames.StagingPrefix + Guid.NewGuid());

        Directory.CreateDirectory(staging);

        try
        {
            progress(new PrivacyInstallProgress(PrivacyInstallStage.RuntimeExtract));

            using var zip = ZipFile.OpenRead(archive);

            var boundary = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;

            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();

                var path = Path.GetFullPath(Path.Combine(staging, entry.FullName));

                if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)
                    || (entry.ExternalAttributes >> 16 & 0xF000) == 0xA000)
                    throw new PrivacyScanException(PrivacyInstallNames.ArchiveError);

                if (entry.Name.Length == 0)
                {
                    Directory.CreateDirectory(path);

                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);

                using var source = entry.Open();

                using var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                    65536, FileOptions.Asynchronous);

                await source.CopyToAsync(target, token).ConfigureAwait(false);
            }

            if (!File.Exists(Path.Combine(staging, PrivacyNames.RuntimeExecutable)))
                throw new PrivacyScanException(PrivacyInstallNames.ArchiveError);

            token.ThrowIfCancellationRequested();

            if (!Directory.Exists(root))
            {
                Directory.Move(staging, root);
            }
            else
            {
                // A previous incomplete installation may have model files that must survive runtime repair.
                var pending = Path.Combine(root, PrivacyInstallNames.RuntimePendingFile);

                File.WriteAllText(pending, string.Empty);

                var files = Directory.EnumerateFiles(staging, PrivacyInstallNames.AllFiles, SearchOption.AllDirectories)
                    .OrderBy(file => string.Equals(Path.GetFileName(file), PrivacyNames.RuntimeExecutable,
                        StringComparison.OrdinalIgnoreCase)).ToList();

                var total = files.Sum(file => new FileInfo(file).Length);

                long completed = 0;

                foreach (var file in files)
                {
                    progress(new PrivacyInstallProgress(PrivacyInstallStage.RuntimeExtract, completed, total));

                    token.ThrowIfCancellationRequested();

                    var destination = Path.Combine(root, Path.GetRelativePath(staging, file));

                    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);

                    var temporary = destination + PrivacyNames.TemporarySuffix;

                    try
                    {
                        {
                            using var source = File.OpenRead(file);

                            using var target = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                                FileShare.None, 65536, FileOptions.Asynchronous);

                            await source.CopyToAsync(target, token).ConfigureAwait(false);
                        }

                        token.ThrowIfCancellationRequested();

                        File.Move(temporary, destination, overwrite: true);

                        completed += new FileInfo(file).Length;
                    }
                    finally
                    {
                        File.Delete(temporary);
                    }
                }

                token.ThrowIfCancellationRequested();

                File.Delete(pending);
            }
        }
        finally
        {
            // This GUID directory belongs to this operation and is always adjacent to the runtime root.
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }
        }

        File.Delete(archive);
    }

    private async Task DownloadAsync(string path, Action<PrivacyInstallProgress> progress, CancellationToken token)
    {
        var completed = File.Exists(path) ? new FileInfo(path).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, _package.Url);

        if (completed > 0)
        {
            request.Headers.Range = new RangeHeaderValue(completed, null);
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && completed > 0)
            return;

        response.EnsureSuccessStatusCode();

        if (response.StatusCode == HttpStatusCode.PartialContent)
        {
            if (response.Content.Headers.ContentRange?.From != completed)
                throw new PrivacyScanException(PrivacyInstallNames.DownloadError);
        }
        else
        {
            completed = 0;
        }

        var total = response.Content.Headers.ContentLength is long length ? completed + length : 0;

        using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

        using var target = new FileStream(path, completed > 0 ? FileMode.Append : FileMode.Create,
            FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);

        var buffer = new byte[65536];

        while (true)
        {
            var read = await source.ReadAsync(buffer, token).ConfigureAwait(false);

            if (read == 0)
                break;

            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);

            completed += read;

            if (completed > 4L * 1024 * 1024 * 1024)
                throw new PrivacyScanException(PrivacyInstallNames.DownloadError);

            progress(new PrivacyInstallProgress(PrivacyInstallStage.RuntimeDownload, completed, total));
        }
    }

    public void Dispose() => _client.Dispose();
}
