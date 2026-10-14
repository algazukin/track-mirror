// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class CancelOnceAnalyzer : IAiPrivacyAnalyzer
{
    private int calls;

    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<AiPrivacyAnalysis> AnalyzeAsync(PrivacyAnalysisInput input, CancellationToken token)
    {
        if (Interlocked.Increment(ref calls) == 1)
        {
            Entered.TrySetResult();

            await Task.Delay(Timeout.Infinite, token);
        }

        return new AiPrivacyAnalysis(false, []);
    }
}
