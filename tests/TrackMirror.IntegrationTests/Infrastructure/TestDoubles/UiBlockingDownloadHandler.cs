// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class UiBlockingDownloadHandler(CancellationGate gate) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        await gate.WaitAsync(token);

        return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
    }
}
