// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

namespace TrackMirror.IntegrationTests.Privacy.Providers;

public sealed class PrivacyProviderBatchTests
{
    [Fact]
    public async Task Context_rejection_splits_batch_and_deduplicates_confirmed_regions()
    {
        // Arrange
        using var handler = new OllamaBatchHandler((_, ids) => ids.Length > 2
            ? OllamaBatchHandler.ContextError() : OllamaBatchHandler.Success(ids));

        using var analyzer = Analyzer(handler);

        // Act
        var result = await analyzer.AnalyzeAsync(Input(4), CancellationToken.None);

        // Assert
        Assert.Empty(result.FailureCode);

        Assert.Single(result.Findings);

        Assert.Equal(3, handler.Requests);

        Assert.Equal(PrivacyNames.ModelContext, result.Diagnostics[0].FailureCode);

        Assert.Null(result.Diagnostics[0].PromptTokens);

        Assert.Equal(123, result.Diagnostics[1].PromptTokens);

        Assert.Equal(20, result.Diagnostics[1].ReplyTokens);
    }

    [Fact]
    public async Task Cancellation_preserves_successful_batch_and_failed_request_metrics()
    {
        // Arrange
        using var handler = new OllamaBatchHandler((_, ids) => OllamaBatchHandler.Success([ids[0]]))
        {
            BlockAfter = 1
        };

        using var analyzer = Analyzer(handler);

        using var cancel = new CancellationTokenSource();

        AiPrivacyAnalysis? checkpoint = null;

        var input = Input(80) with { Checkpoint = progress => checkpoint = progress };

        // Act
        var pending = analyzer.AnalyzeAsync(input, cancel.Token);

        await handler.Blocked.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cancel.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);

        Assert.NotNull(checkpoint);

        Assert.Single(checkpoint.Findings);

        Assert.Equal(2, checkpoint.Diagnostics.Count);

        Assert.Equal(PrivacyNames.Cancelled, checkpoint.Diagnostics[^1].FailureCode);
    }

    [Fact]
    public async Task Missing_token_metrics_remain_unknown()
    {
        // Arrange
        using var handler = new OllamaBatchHandler((_, _) => OllamaBatchHandler.Success([], metrics: false));

        using var analyzer = Analyzer(handler);

        // Act
        var result = await analyzer.AnalyzeAsync(Input(1), CancellationToken.None);

        // Assert
        var diagnostic = Assert.Single(result.Diagnostics);

        Assert.Null(diagnostic.PromptTokens);

        Assert.Null(diagnostic.ReplyTokens);

        Assert.True(diagnostic.RequestBytes > 0);
    }

    private static OllamaPrivacyAnalyzer Analyzer(OllamaBatchHandler handler) => new(
        new HttpClient(handler) { BaseAddress = new Uri("http://127.0.0.1/") },
        (_, _, _) => Task.CompletedTask
    );

    [Fact]
    public async Task Repeated_context_errors_stop_at_request_limit_with_explicit_failure()
    {
        // Arrange
        using var handler = new OllamaBatchHandler((_, _) => OllamaBatchHandler.ContextError());

        using var analyzer = Analyzer(handler);

        // Act
        var result = await analyzer.AnalyzeAsync(Input(80), CancellationToken.None);

        // Assert
        Assert.Equal(PrivacyNames.AnalysisLimit, result.FailureCode);

        Assert.Equal(PrivacyProtocol.MaxPrivacyRequests, handler.Requests);

        Assert.Equal(handler.Requests, result.Diagnostics.Count);
    }

    private static PrivacyAnalysisInput Input(int count) => new([], Enumerable.Range(0, count)
        .Select(id => new OcrBlock(id, new string('x', 80), new PrivacyRegion(0, 0, 1, 1))).ToList(),
        false, false, true);
}
