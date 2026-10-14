// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SkiaSharp;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Analysis;
using TrackMirror.Privacy.Installation;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Ocr;
using TrackMirror.Privacy.Providers;
using TrackMirror.Privacy.Storage;
using TrackMirror.Storage;

namespace TrackMirror.Privacy;

internal sealed class PrivacyScanService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly ScreenshotStore _captures;
    private readonly PrivacyResultStore _results;
    private readonly IAiPrivacyAnalyzer _analyzer;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _signal = new(0, 1);
    private readonly Task _worker;
    private AppSettings settings;
    private LocalOcrEngine? ocr;
    private CancellationTokenSource? active;
    private TaskCompletionSource? modelRelease;
    private TaskCompletionSource? paused;
    private bool stopping;

    internal event Action? Changed;

    internal string? Warning { get; private set; }

    internal PrivacyScanService(ScreenshotStore captures, AppSettings settings, IAiPrivacyAnalyzer? analyzer = null)
    {
        _captures = captures;

        this.settings = settings;

        _analyzer = analyzer ?? new OllamaPrivacyAnalyzer();

        _results = new PrivacyResultStore(captures.DataRoot,
            captures.Snapshot().Select(entry => entry.Id).ToHashSet(StringComparer.OrdinalIgnoreCase));

        Warning = _results.InitialWarning;

        _worker = Task.Run(RunAsync);

        Wake();
    }

    internal static string Profile(AppSettings settings) =>
        PrivacyModelCatalog.Version(settings.PrivacyModel) + settings.PrivacyGames + settings.PrivacyPersonalChats
            + settings.PrivacyConfidentialData;

    internal Dictionary<string, PrivacyScanRecord> Snapshot() => _results.Snapshot();

    internal void ApplySettings(AppSettings value)
    {
        lock (_gate)
        {
            if (Profile(settings) != Profile(value))
            {
                CancelCore();
            }

            settings = value;
        }

        Changed?.Invoke();
    }

    internal void Enqueue(IEnumerable<CaptureEntry> entries, bool force = false,
        IReadOnlyList<PrivacyScanCategory>? categories = null)
    {
        lock (_gate)
        {
            if (stopping || paused != null || modelRelease != null)
                return;

            var selected = categories?.Distinct().ToList() ?? PrivacyCoverage.Selected(settings);

            if (selected.Any(category => !Enum.IsDefined(category)))
                throw new ArgumentOutOfRangeException(nameof(categories));

            if (selected.Count == 0)
                return;

            var current = _results.Snapshot();

            var profile = Profile(settings);

            var jobs = new List<PrivacyScanRecord>();

            var restartActive = false;

            foreach (var entry in entries.DistinctBy(entry => entry.Id))
            {
                current.TryGetValue(entry.Id, out var old);

                var requested = selected
                    .Where(category => force || old == null || !PrivacyCoverage.Complete(old, category,
                        PrivacyCoverage.Version(category, settings.PrivacyModel))).ToList();

                if (requested.Count == 0)
                    continue;

                if (old?.State is PrivacyScanState.Queued or PrivacyScanState.Scanning)
                {
                    if (!force && requested.All(old.RequestedCategories.Contains))
                        continue;

                    requested = old.RequestedCategories.Union(requested).ToList();

                    restartActive |= old.State == PrivacyScanState.Scanning;
                }

                var record = old ?? new PrivacyScanRecord
                {
                    CaptureId = entry.Id,
                    RequestId = Guid.NewGuid().ToString(),
                    Profile = profile
                };

                jobs.Add(record with
                {
                    RequestId = Guid.NewGuid().ToString(),
                    Profile = profile,
                    State = PrivacyScanState.Queued,
                    RequestedCategories = requested,
                    FailureCode = string.Empty,
                    Attempts = 0,
                    UpdatedUtc = DateTime.UtcNow
                });
            }

            if (jobs.Count == 0)
                return;

            if (!Save(jobs))
            {
                Changed?.Invoke();

                return;
            }

            // Persist the expanded request before cancelling its superseded in-flight operation.
            if (restartActive)
            {
                active?.Cancel();
            }

            Wake();
        }

        Changed?.Invoke();
    }

    internal void SetDisposition(string id, PrivacyDisposition disposition)
    {
        lock (_gate)
        {
            if (_results.Snapshot().TryGetValue(id, out var record))
            {
                Save([record with { Disposition = disposition }]);
            }
        }

        Changed?.Invoke();
    }

    internal void Cancel()
    {
        lock (_gate)
        {
            CancelCore();
        }

        Changed?.Invoke();
    }

    private void CancelCore()
    {
        active?.Cancel();

        var queued = _results.Snapshot().Values
            .Where(record => record.State is PrivacyScanState.Queued or PrivacyScanState.Scanning)
            .Select(record => record with { State = PrivacyScanState.Cancelled,
                FailureCode = PrivacyNames.Cancelled, UpdatedUtc = DateTime.UtcNow }).ToList();

        if (queued.Count > 0)
        {
            Save(queued);
        }
    }

    internal async Task ReleaseLocalModelAsync(CancellationToken token)
    {
        Task release;

        lock (_gate)
        {
            modelRelease ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            release = modelRelease.Task;

            CancelCore();

            Wake();
        }

        Changed?.Invoke();

        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, _shutdown.Token);

        await release.WaitAsync(cancellation.Token).ConfigureAwait(false);
    }

    internal void ResumeModelChecks()
    {
        lock (_gate)
        {
            modelRelease = null;

            if (!stopping)
            {
                Wake();
            }
        }

        Changed?.Invoke();
    }

    internal async Task PauseAsync(CancellationToken token)
    {
        Task idle;

        lock (_gate)
        {
            paused ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            idle = paused.Task;

            active?.Cancel();

            Wake();
        }

        await idle.ConfigureAwait(false);

        token.ThrowIfCancellationRequested();

        if (Warning == PrivacyText.StorageError)
            throw new IOException(DataManagementText.Unsaved);
    }

    internal void Resume()
    {
        lock (_gate)
        {
            paused = null;

            if (!stopping)
            {
                Wake();
            }
        }
    }

    internal void ReloadResults()
    {
        lock (_gate)
        {
            if (paused?.Task.IsCompletedSuccessfully != true)
                throw new InvalidOperationException(DataManagementText.Busy);

            _results.Reload();
        }

        Changed?.Invoke();
    }

    private bool Save(IEnumerable<PrivacyScanRecord> records)
    {
        try
        {
            _results.Put(records);

            Warning = null;

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Warning = PrivacyText.StorageError;

            return false;
        }
    }

    private void Wake()
    {
        if (_signal.CurrentCount == 0)
        {
            _signal.Release();
        }
    }

    private async Task RunAsync()
    {
        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await _signal.WaitAsync(_shutdown.Token);

                while (!_shutdown.IsCancellationRequested)
                {
                    TaskCompletionSource? release;

                    lock (_gate)
                    {
                        if (paused != null)
                        {
                            paused.TrySetResult();

                            break;
                        }

                        release = modelRelease;
                    }

                    if (release != null)
                    {
                        try
                        {
                            (_analyzer as OllamaPrivacyAnalyzer)?.ReleaseRuntime();

                            release.TrySetResult();
                        }
                        catch (Exception ex)
                        {
                            release.TrySetException(ex);
                        }

                        break;
                    }

                    PrivacyScanRecord? job;

                    AppSettings profile;

                    lock (_gate)
                    {
                        if (paused != null)
                        {
                            paused.TrySetResult();

                            break;
                        }

                        profile = settings;

                        job = _results.Snapshot().Values
                            .FirstOrDefault(record => record.State == PrivacyScanState.Queued);

                        if (job == null)
                            break;

                        if (job.Profile != Profile(profile))
                        {
                            if (!Save([job with { State = PrivacyScanState.Cancelled }]))
                                break;

                            continue;
                        }

                        job = job with { State = PrivacyScanState.Scanning, Attempts = job.Attempts + 1 };

                        if (!Save([job]))
                            break;

                        active = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
                    }

                    Changed?.Invoke();

                    var completed = await AnalyzeAsync(job, profile, active.Token);

                    lock (_gate)
                    {
                        active.Dispose();

                        active = null;

                        var current = _results.Snapshot()[job.CaptureId];

                        if (current.RequestId == job.RequestId && current.State == PrivacyScanState.Scanning)
                        {
                            var final = completed with
                            {
                                Disposition = completed.ImageHash != current.ImageHash
                                    ? PrivacyDisposition.Pending : current.Disposition
                            };

                            if (!Save([final]))
                            {
                                _results.SetTransientFailure(final);
                            }
                        }
                    }

                    Changed?.Invoke();
                }

                Changed?.Invoke();
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task<PrivacyScanRecord> AnalyzeAsync(PrivacyScanRecord job, AppSettings profile,
        CancellationToken cancellationToken)
    {
        var timer = Stopwatch.StartNew();

        try
        {
            var entry = _captures.Snapshot().FirstOrDefault(entry =>
                string.Equals(entry.Id, job.CaptureId, StringComparison.OrdinalIgnoreCase))
                ?? throw new FileNotFoundException();

            var path = _captures.CopyPath(entry);

            if (new FileInfo(path).Length > 40 * 1024 * 1024)
                throw new InvalidDataException();

            var bytes = await File.ReadAllBytesAsync(path, cancellationToken);

            var hash = Convert.ToHexString(SHA256.HashData(bytes));

            if (hash != job.ImageHash)
            {
                job = job with
                {
                    ImageHash = hash,
                    Categories = [],
                    Findings = [],
                    Disposition = PrivacyDisposition.Pending
                };

                job = Checkpoint(job);
            }

            using var source = SKBitmap.Decode(bytes) ?? throw new InvalidDataException();

            var scale = Math.Min(1d, 1536d / Math.Max(source.Width, source.Height));

            using var overview = source.Resize(new SKImageInfo(Math.Max(1, (int)(source.Width * scale)),
                Math.Max(1, (int)(source.Height * scale))), new SKSamplingOptions(SKFilterMode.Linear));

            using var encoded = overview.Encode(SKEncodedImageFormat.Png, 95);

            var image = encoded.ToArray();

            var activity = job.RequestedCategories
                .Where(category => category != PrivacyScanCategory.ConfidentialData).ToList();

            foreach (var group in new[] { activity, new List<PrivacyScanCategory>
                { PrivacyScanCategory.ConfidentialData } })
            {
                var requested = group.Where(job.RequestedCategories.Contains).ToList();

                if (requested.Count == 0)
                    continue;

                cancellationToken.ThrowIfCancellationRequested();

                var outcomes = await AnalyzeCategoriesAsync(requested, bytes, image, profile.PrivacyModel,
                    progress => job = Checkpoint(job with
                    {
                        Categories = job.Categories.Where(result => !requested.Contains(result.Category))
                            .Concat(progress).ToList()
                    }, allowCancelled: true), cancellationToken);

                job = Checkpoint(job with
                {
                    Categories = job.Categories.Where(result => !requested.Contains(result.Category))
                        .Concat(outcomes).ToList(),
                    RequestedCategories = job.RequestedCategories.Except(requested).ToList()
                });
            }

            return PrivacyCoverage.Project(job with
            {
                State = PrivacyScanState.Completed,
                UpdatedUtc = DateTime.UtcNow,
                DurationSeconds = timer.Elapsed.TotalSeconds
            }, profile) with { Findings = job.Findings };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            lock (_gate)
            {
                if (paused != null)
                {
                    return job with
                    {
                        State = PrivacyScanState.Queued,
                        Attempts = Math.Max(0, job.Attempts - 1),
                        FailureCode = PrivacyNames.Interrupted
                    };
                }
            }

            return job with
            {
                State = _shutdown.IsCancellationRequested
                    ? job.Attempts < 2 ? PrivacyScanState.Queued : PrivacyScanState.Failed
                    : PrivacyScanState.Cancelled,
                FailureCode = _shutdown.IsCancellationRequested ? PrivacyNames.Interrupted : PrivacyNames.Cancelled
            };
        }
        catch (Exception ex)
        {
            // Keep completed checkpoints and persist safe codes, never source text or exception messages.
            return job with
            {
                State = PrivacyScanState.Failed,
                FailureCode = ex is PrivacyScanException error ? error.Code : PrivacyNames.MissingCopy,
                UpdatedUtc = DateTime.UtcNow,
                DurationSeconds = timer.Elapsed.TotalSeconds
            };
        }
    }

    private async Task<List<PrivacyCategoryResult>> AnalyzeCategoriesAsync(List<PrivacyScanCategory> requested,
        byte[] bytes, byte[] image, string model, Action<List<PrivacyCategoryResult>> checkpoint,
        CancellationToken cancellationToken)
    {
        var findings = new List<PrivacyFinding>();

        var blocks = new List<OcrBlock>();

        var ocrIncomplete = false;

        var stage = PrivacyNames.VisionFailure;

        List<PrivacyCategoryResult> Outcomes(AiPrivacyAnalysis analysis, bool partial)
        {
            var merged = PrivacyFindings.Merge(findings.Concat(analysis.Findings));

            var code = merged.Count > 400 ? PrivacyNames.AnalysisLimit
                : analysis.FailureCode.Length > 0 ? analysis.FailureCode
                : ocrIncomplete ? PrivacyNames.OcrIncomplete
                : analysis.Uncertain ? PrivacyNames.ModelUncertain : string.Empty;

            return requested.Select(category => new PrivacyCategoryResult
            {
                Category = category,
                Version = PrivacyCoverage.Version(category, model),
                State = partial || code.Length > 0 ? PrivacyScanState.Partial : PrivacyScanState.Completed,
                Findings = merged.Take(400).Where(finding => PrivacyCoverage.Category(finding) == category).ToList(),
                FailureCode = code,
                Diagnostics = analysis.Diagnostics,
                UpdatedUtc = DateTime.UtcNow
            }).ToList();
        }

        try
        {
            if (requested.Contains(PrivacyScanCategory.ConfidentialData))
            {
                stage = PrivacyNames.OcrFailure;

                ocr ??= new LocalOcrEngine();

                var text = ocr.Read(bytes, cancellationToken);

                findings.AddRange(SecretRuleScanner.Scan(text.Blocks));

                blocks.AddRange(text.Blocks);

                ocrIncomplete = text.Incomplete;
            }

            stage = PrivacyNames.VisionFailure;

            var input = new PrivacyAnalysisInput(image, blocks,
                requested.Contains(PrivacyScanCategory.Game), requested.Contains(PrivacyScanCategory.PersonalChat),
                requested.Contains(PrivacyScanCategory.ConfidentialData), model)
            {
                Checkpoint = analysis => checkpoint(Outcomes(analysis, true))
            };

            return Outcomes(await _analyzer.AnalyzeAsync(input, cancellationToken), false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Outcomes(new AiPrivacyAnalysis(false, [],
                ex is PrivacyScanException error ? error.Code : stage), true);
        }
    }

    private PrivacyScanRecord Checkpoint(PrivacyScanRecord job, bool allowCancelled = false)
    {
        lock (_gate)
        {
            var current = _results.Snapshot()[job.CaptureId];

            if (current.RequestId != job.RequestId || (current.State != PrivacyScanState.Scanning
                && (!allowCancelled || current.State != PrivacyScanState.Cancelled)))
                throw new OperationCanceledException(active?.Token ?? _shutdown.Token);

            if (current.State == PrivacyScanState.Cancelled)
            {
                job = job with { State = current.State, FailureCode = current.FailureCode };
            }

            var findings = job.Categories.SelectMany(result => result.Findings).ToList();

            var previous = current.Findings.Select(finding => JsonSerializer.Serialize(finding)).ToHashSet();

            var newRisk = findings.Any(finding => !previous.Contains(JsonSerializer.Serialize(finding)));

            job = job with
            {
                Findings = findings,
                Disposition = newRisk || job.ImageHash != current.ImageHash
                    ? PrivacyDisposition.Pending : current.Disposition,
                UpdatedUtc = DateTime.UtcNow
            };

            if (!Save([job]))
                throw new PrivacyScanException(PrivacyNames.StorageFailure);
        }

        Changed?.Invoke();

        return job;
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            stopping = true;

            _shutdown.Cancel();

            active?.Cancel();
        }

        await _worker;

        (_analyzer as IDisposable)?.Dispose();

        ocr?.Dispose();

        active?.Dispose();

        _signal.Dispose();

        _shutdown.Dispose();
    }
}
