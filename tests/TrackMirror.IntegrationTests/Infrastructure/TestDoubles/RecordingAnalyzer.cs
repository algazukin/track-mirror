// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class RecordingAnalyzer : IAiPrivacyAnalyzer
{
    internal List<PrivacyAnalysisInput> Calls { get; } = [];

    public Task<AiPrivacyAnalysis> AnalyzeAsync(PrivacyAnalysisInput input, CancellationToken cancellationToken)
    {
        Calls.Add(input);

        return Task.FromResult(new AiPrivacyAnalysis(false, []));
    }
}
