// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Net;
using System.Text;
using System.Text.Json;
using TrackMirror.Models;
using TrackMirror.Updates;
using Xunit;

namespace TrackMirror.IntegrationTests.Updates;

public sealed class UpdateCheckerTests
{
    private const string Published = """
        [
          {"tag_name":"v1.0.0-preview.2","draft":false,"prerelease":true},
          {"tag_name":"v1.0.0","draft":false,"prerelease":false},
          {"tag_name":"v1.1.0-preview.10","draft":false,"prerelease":true},
          {"tag_name":"v1.1.0-preview.2","draft":false,"prerelease":false},
          {"tag_name":"v9.0.0","draft":true,"prerelease":false}
        ]
        """;

    [Theory]
    [InlineData(UpdateChannel.PreRelease, "1.1.0-preview.10")]
    [InlineData(UpdateChannel.Stable, "1.0.0")]
    public async Task The_selected_channel_uses_the_highest_published_version_and_ignores_drafts(
        UpdateChannel channel, string expected)
    {
        // Arrange
        Uri? requested = null;
        string? userAgent = null;

        using var handler = new Handler((request, _) =>
        {
            requested = request.RequestUri;
            userAgent = request.Headers.UserAgent.ToString();
            Assert.Null(request.Headers.Authorization);

            return Task.FromResult(Json(Published));
        });

        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var result = await checker.CheckAsync("1.0.0-preview.1", channel, CancellationToken.None);

        // Assert
        Assert.Equal(UpdateCheckStatus.Available, result.Status);
        Assert.Equal(expected, result.Version);
        Assert.Equal(new Uri("https://github.com/algazukin/track-mirror/releases/tag/v" + expected), result.ReleaseUrl);

        Assert.Equal("api.github.com", requested?.Host);
        Assert.Contains("/repos/algazukin/track-mirror/releases", requested?.AbsolutePath);
        Assert.Contains("TrackMirror/1.0.0-preview.1", userAgent);
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("2.0.0")]
    public async Task An_equal_or_newer_installed_version_is_not_offered_an_update(string installed)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json(Published)));
        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var result = await checker.CheckAsync(installed, UpdateChannel.Stable, CancellationToken.None);

        // Assert
        Assert.Equal(UpdateCheckStatus.UpToDate, result.Status);
        Assert.Null(result.ReleaseUrl);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"tag_name\":\"v1.0.0-preview.1\",\"draft\":false,\"prerelease\":true}]")]
    [InlineData("[{\"tag_name\":\"invalid\",\"draft\":false,\"prerelease\":false}]")]
    public async Task No_matching_release_is_distinct_from_being_up_to_date(string feed)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json(feed)));
        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var result = await checker.CheckAsync("1.0.0-preview.1", UpdateChannel.Stable, CancellationToken.None);

        // Assert
        Assert.Equal(UpdateCheckStatus.NoReleases, result.Status);
        Assert.Null(result.ReleaseUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task A_failed_GitHub_request_does_not_claim_that_the_app_is_up_to_date(HttpStatusCode status)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var operation = checker.CheckAsync("1.0.0", UpdateChannel.PreRelease, CancellationToken.None);

        // Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => operation);
        Assert.Equal(status, exception.StatusCode);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{}")]
    [InlineData("[{\"tag_name\":\"v2.0.0\"}]")]
    public async Task A_malformed_response_is_reported_as_a_check_failure(string feed)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json(feed)));
        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var operation = checker.CheckAsync("1.0.0", UpdateChannel.PreRelease, CancellationToken.None);

        // Assert
        await Assert.ThrowsAsync<JsonException>(() => operation);
    }

    [Fact]
    public async Task Later_pages_are_checked_before_reporting_the_newest_version()
    {
        // Arrange
        var requests = 0;
        var firstPage = JsonSerializer.Serialize(Enumerable.Range(0, 100).Select(index => new
        {
            tag_name = $"v1.0.{index}", draft = false, prerelease = false
        }));

        using var handler = new Handler((request, _) =>
        {
            requests++;

            return Task.FromResult(Json(request.RequestUri!.Query.Contains("page=2", StringComparison.Ordinal)
                ? "[{\"tag_name\":\"v2.0.0\",\"draft\":false,\"prerelease\":false}]" : firstPage));
        });

        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var result = await checker.CheckAsync("1.0.0", UpdateChannel.Stable, CancellationToken.None);

        // Assert
        Assert.Equal(2, requests);
        Assert.Equal("2.0.0", result.Version);
        Assert.Equal(UpdateCheckStatus.Available, result.Status);
    }

    [Fact]
    public async Task Cancellation_is_observed_before_contacting_GitHub()
    {
        // Arrange
        var requests = 0;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        using var handler = new Handler((_, _) =>
        {
            requests++;

            return Task.FromResult(Json(Published));
        });

        using var client = new HttpClient(handler);
        using var checker = new UpdateChecker(client);

        // Act
        var operation = checker.CheckAsync("1.0.0", UpdateChannel.PreRelease, cancellation.Token);

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Equal(0, requests);
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            respond(request, token);
    }
}
