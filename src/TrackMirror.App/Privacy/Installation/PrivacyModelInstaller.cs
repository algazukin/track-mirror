// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;

namespace TrackMirror.Privacy.Installation;

internal sealed class PrivacyModelInstaller : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly string _root;
    private readonly PrivacyRuntimeInstaller _bootstrap;
    private readonly HttpClient _client;
    private CancellationTokenSource? active;
    private Task worker = Task.CompletedTask;
    private PrivacyInstallProgress progress = new(PrivacyInstallStage.Idle);
    private DateTime lastProgress;
    private bool stopping;

    internal event Action? Changed;

    internal PrivacyModelInstaller(string? root = null, PrivacyRuntimeInstaller? bootstrap = null,
        HttpClient? client = null)
    {
        _root = root ?? LocalModelRuntime.Root;

        _bootstrap = bootstrap ?? new PrivacyRuntimeInstaller();

        _client = client ?? new HttpClient(new HttpClientHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false
        }) { BaseAddress = new Uri(PrivacyInstallNames.LocalEndpoint), Timeout = Timeout.InfiniteTimeSpan };
    }

    internal PrivacyInstallProgress Snapshot()
    {
        lock (_gate)
        {
            return progress;
        }
    }

    internal Task Completion
    {
        get
        {
            lock (_gate)
            {
                return worker;
            }
        }
    }

    internal void Start(PrivacyModelDefinition model) => Begin(model, false);

    internal void Remove(PrivacyModelDefinition model, Func<CancellationToken, Task> release, Action resume) =>
        Begin(model, true, release, resume);

    private void Begin(PrivacyModelDefinition model, bool remove,
        Func<CancellationToken, Task>? release = null, Action? resume = null)
    {
        lock (_gate)
        {
            if (stopping || !worker.IsCompleted)
                return;

            active?.Dispose();

            active = new CancellationTokenSource();

            progress = new PrivacyInstallProgress(remove ? PrivacyInstallStage.ModelRemove
                : PrivacyInstallStage.RuntimeDownload) { Model = model.Tag };

            worker = Task.Run(() => RunAsync(model, remove, release, resume, active.Token));
        }

        Changed?.Invoke();
    }

    internal void Cancel()
    {
        lock (_gate)
        {
            active?.Cancel();
        }
    }

    private void Publish(PrivacyInstallProgress value)
    {
        lock (_gate)
        {
            if (value.Stage == progress.Stage && value.Busy
                && DateTime.UtcNow - lastProgress < TimeSpan.FromMilliseconds(150))
                return;

            progress = value with { Model = progress.Model };

            lastProgress = DateTime.UtcNow;
        }

        Changed?.Invoke();
    }

    private async Task RunAsync(PrivacyModelDefinition model, bool remove,
        Func<CancellationToken, Task>? release, Action? resume, CancellationToken token)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_root)!);

            FileStream reservation;

            try
            {
                reservation = new FileStream(Path.Combine(Path.GetDirectoryName(_root)!, PrivacyInstallNames.LockFile),
                    FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                throw new PrivacyScanException(PrivacyInstallNames.BusyError);
            }

            using (reservation)
            {
                if (remove)
                {
                    await release!(token).ConfigureAwait(false);

                    await RemoveAsync(model, token).ConfigureAwait(false);
                }
                else
                {
                    await _bootstrap.EnsureAsync(_root, Publish, token).ConfigureAwait(false);

                    if (!PrivacyModelCatalog.Installed(_root, model))
                    {
                        var pending = PrivacyModelCatalog.PendingPath(_root, model);

                        Directory.CreateDirectory(Path.GetDirectoryName(pending)!);

                        File.WriteAllText(pending, string.Empty);

                        using var runtime = new LocalModelRuntime(_root, PrivacyInstallNames.LocalPort);

                        Publish(new PrivacyInstallProgress(PrivacyInstallStage.ModelDownload));

                        await runtime.EnsureServerAsync(_client, token).ConfigureAwait(false);

                        await PullAsync(model, token).ConfigureAwait(false);
                    }

                    await VerifyAsync(model, token).ConfigureAwait(false);

                    File.Delete(PrivacyModelCatalog.RemovalPath(_root, model));
                }

                File.Delete(PrivacyModelCatalog.PendingPath(_root, model));
            }

            token.ThrowIfCancellationRequested();

            Publish(new PrivacyInstallProgress(remove ? PrivacyInstallStage.Removed : PrivacyInstallStage.Ready));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Publish(new PrivacyInstallProgress(PrivacyInstallStage.Cancelled));
        }
        catch (Exception ex)
        {
            var code = ex is PrivacyScanException error ? error.Code : remove ? PrivacyInstallNames.RemoveError
                : ex is HttpRequestException ? PrivacyInstallNames.DownloadError
                : ex is InvalidDataException ? PrivacyInstallNames.ArchiveError
                : ex is IOException or UnauthorizedAccessException ? PrivacyInstallNames.StorageError
                : PrivacyInstallNames.ModelError;

            Publish(new PrivacyInstallProgress(PrivacyInstallStage.Failed, FailureCode: code));
        }
        finally
        {
            resume?.Invoke();
        }
    }

    private async Task RemoveAsync(PrivacyModelDefinition model, CancellationToken token)
    {
        await PrepareRemovalAsync(model, token).ConfigureAwait(false);

        using var runtime = new LocalModelRuntime(_root, PrivacyInstallNames.LocalPort);

        await runtime.EnsureServerAsync(_client, token).ConfigureAwait(false);

        // Ollama removes only unreferenced layers, retaining weights shared with other models.
        using var request = new HttpRequestMessage(HttpMethod.Delete, PrivacyInstallNames.DeletePath)
        {
            Content = JsonContent.Create(new { model = model.Tag })
        };

        using var response = await _client.SendAsync(request, token).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (File.Exists(PrivacyModelCatalog.ManifestPath(_root, model)))
            throw new PrivacyScanException(PrivacyInstallNames.RemoveError);

        File.Delete(PrivacyModelCatalog.RemovalPath(_root, model));
    }

    private async Task PrepareRemovalAsync(PrivacyModelDefinition model, CancellationToken token)
    {
        var manifest = PrivacyModelCatalog.ManifestPath(_root, model);

        var pending = PrivacyModelCatalog.RemovalPath(_root, model);

        var source = File.Exists(pending) ? pending : manifest;

        if (!File.Exists(source) || new FileInfo(source).Length > 65536)
            throw new PrivacyScanException(PrivacyInstallNames.RemoveError);

        var bytes = await File.ReadAllBytesAsync(source, token).ConfigureAwait(false);

        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        if (hash != model.ManifestDigest && hash != model.LocalManifestDigest)
            throw new PrivacyScanException(PrivacyInstallNames.RemoveError);

        if (!File.Exists(pending))
        {
            var temporary = pending + PrivacyNames.TemporarySuffix;

            try
            {
                await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);

                token.ThrowIfCancellationRequested();

                File.Move(temporary, pending);
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        token.ThrowIfCancellationRequested();

        // Restore the small manifest on retry, even if an earlier DELETE already removed some exclusive layers.
        // Ollama can then retry its reference-aware cleanup; the marker keeps inference disabled until completion.
        Directory.CreateDirectory(Path.GetDirectoryName(manifest)!);

        File.Copy(pending, manifest, overwrite: true);
    }

    private async Task PullAsync(PrivacyModelDefinition model, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, PrivacyInstallNames.PullPath)
        {
            Content = JsonContent.Create(new { model = model.Tag, stream = true })
        };

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);

        using var reader = new StreamReader(stream);

        var layers = new Dictionary<string, long>();

        var success = false;

        while (await reader.ReadLineAsync(token).ConfigureAwait(false) is string line)
        {
            if (line.Length > 65536)
                throw new PrivacyScanException(PrivacyInstallNames.ModelError);

            using var message = JsonDocument.Parse(line);

            var value = message.RootElement;

            if (value.TryGetProperty(PrivacyProtocol.Error, out _))
                throw new PrivacyScanException(PrivacyInstallNames.ModelError);

            if (value.TryGetProperty(PrivacyProtocol.Digest, out var digest)
                && value.TryGetProperty(PrivacyInstallNames.Completed, out var completed)
                && completed.TryGetInt64(out var bytes))
            {
                layers[digest.GetString()!] = Math.Max(0, bytes);

                Publish(new PrivacyInstallProgress(PrivacyInstallStage.ModelDownload,
                    Math.Min(model.DownloadBytes, layers.Values.Sum()), model.DownloadBytes));
            }

            if (value.TryGetProperty(PrivacyInstallNames.Status, out var status)
                && status.GetString() == PrivacyInstallNames.Success)
            {
                success = true;
            }
        }

        if (!success)
            throw new PrivacyScanException(PrivacyInstallNames.ModelError);
    }

    private async Task VerifyAsync(PrivacyModelDefinition model, CancellationToken token)
    {
        Publish(new PrivacyInstallProgress(PrivacyInstallStage.ModelVerify));

        if (!PrivacyModelCatalog.ManifestComplete(_root, model))
            throw new PrivacyScanException(PrivacyInstallNames.IntegrityError);

        using var manifest = JsonDocument.Parse(await File.ReadAllBytesAsync(
            PrivacyModelCatalog.ManifestPath(_root, model), token).ConfigureAwait(false));

        foreach (var part in PrivacyModelCatalog.Parts(manifest).DistinctBy(part =>
            part.GetProperty(PrivacyProtocol.Digest).GetString()))
        {
            using var file = File.OpenRead(PrivacyModelCatalog.BlobPath(_root, part));

            var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token).ConfigureAwait(false))
                .ToLowerInvariant();

            var expected = part.GetProperty(PrivacyProtocol.Digest).GetString()![PrivacyProtocol.Sha256Prefix.Length..];

            if (actual != expected)
            {
                file.Dispose();

                // A corrupt blob must not be reused by the next pull; valid cached blobs stay untouched.
                File.Delete(PrivacyModelCatalog.BlobPath(_root, part));

                var pending = PrivacyModelCatalog.PendingPath(_root, model);

                File.WriteAllText(pending, string.Empty);

                throw new PrivacyScanException(PrivacyInstallNames.IntegrityError);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            stopping = true;

            active?.Cancel();
        }

        await Completion.ConfigureAwait(false);

        active?.Dispose();

        _bootstrap.Dispose();

        _client.Dispose();
    }
}
