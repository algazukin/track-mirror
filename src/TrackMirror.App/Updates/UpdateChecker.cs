// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Constants;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrackMirror.Updates;

internal enum UpdateCheckStatus { Available, UpToDate, NoReleases }

internal sealed record UpdateCheckResult(UpdateCheckStatus Status, string? Version = null, Uri? ReleaseUrl = null);

internal sealed class UpdateChecker : IDisposable
{
    private readonly HttpClient _client;
    private readonly bool _ownsClient;

    internal UpdateChecker(HttpClient? client = null)
    {
        _ownsClient = client is null;
        _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
        {
            Timeout = Timeout.InfiniteTimeSpan,
            MaxResponseContentBufferSize = 4 * 1024 * 1024
        };
    }

    internal async Task<UpdateCheckResult> CheckAsync(string version, UpdateChannel channel, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (!ReleaseVersion.TryParse(version, out var installed) || !Enum.IsDefined(channel))
            throw new ArgumentException("Invalid installed version or update channel.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        ReleaseVersion? latest = null;
        string? latestTag = null;

        for (var page = 1; page <= 10; page++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                ProjectInfo.ReleasesApiUrl + $"?per_page=100&page={page}");

            request.Headers.UserAgent.ParseAdd("TrackMirror/" + installed.Text);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseContentRead,
                timeout.Token).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            await using var content = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            var releases = await JsonSerializer.DeserializeAsync<List<PublishedRelease?>>(content,
                cancellationToken: timeout.Token).ConfigureAwait(false)
                ?? throw new JsonException("Expected a GitHub release list.");

            foreach (var release in releases)
            {
                if (release is null)
                    throw new JsonException("Expected a release object.");

                if (release.Draft || !ReleaseVersion.TryParse(release.Tag, out var candidate))
                    continue;

                if (channel == UpdateChannel.Stable && (release.PreRelease || candidate.IsPreRelease))
                    continue;

                if (latest is null || candidate.CompareTo(latest) > 0)
                {
                    latest = candidate;
                    latestTag = release.Tag;
                }
            }

            if (releases.Count < 100)
                break;

            if (page == 10)
                throw new InvalidDataException("The release list exceeds the supported page limit.");
        }

        if (latest is null)
            return new(UpdateCheckStatus.NoReleases);

        if (latest.CompareTo(installed) <= 0)
            return new(UpdateCheckStatus.UpToDate);

        return new(UpdateCheckStatus.Available, latest.Text,
            new Uri(ProjectInfo.RepositoryUrl + "/releases/tag/" + Uri.EscapeDataString(latestTag!)));
    }

    public void Dispose()
    {
        if (_ownsClient)
            _client.Dispose();
    }

    private sealed record PublishedRelease
    {
        [JsonPropertyName("tag_name"), JsonRequired]
        public string? Tag { get; init; }

        [JsonPropertyName("draft"), JsonRequired]
        public bool Draft { get; init; }

        [JsonPropertyName("prerelease"), JsonRequired]
        public bool PreRelease { get; init; }
    }
}
