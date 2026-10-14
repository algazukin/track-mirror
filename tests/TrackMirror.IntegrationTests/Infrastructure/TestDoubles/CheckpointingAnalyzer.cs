// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class CheckpointingAnalyzer : IAiPrivacyAnalyzer
{
    internal TaskCompletionSource Waiting { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<AiPrivacyAnalysis> AnalyzeAsync(PrivacyAnalysisInput input, CancellationToken token)
    {
        var findings = new List<PrivacyFinding>
        {
            new(PrivacyRules.Game, PrivacyRules.Medium, PrivacyRules.ContextReason, [])
        };

        var diagnostics = new List<PrivacyRequestDiagnostic> { new() { RequestBytes = 100, PromptTokens = 20 } };

        input.Checkpoint?.Invoke(new AiPrivacyAnalysis(false, findings) { Diagnostics = diagnostics.ToList() });

        Waiting.TrySetResult();

        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        finally
        {
            diagnostics.Add(new PrivacyRequestDiagnostic { RequestBytes = 100, FailureCode = PrivacyNames.Cancelled });

            input.Checkpoint?.Invoke(new AiPrivacyAnalysis(false, findings, PrivacyNames.Cancelled)
            {
                Diagnostics = diagnostics
            });
        }

        throw new InvalidOperationException("The synthetic request must be cancelled.");
    }
}
