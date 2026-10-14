// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class UiBlockingAnalyzer(CancellationGate gate) : IAiPrivacyAnalyzer
{
    public async Task<AiPrivacyAnalysis> AnalyzeAsync(PrivacyAnalysisInput input, CancellationToken token)
    {
        await gate.WaitAsync(token);

        return new AiPrivacyAnalysis(false, []);
    }
}
