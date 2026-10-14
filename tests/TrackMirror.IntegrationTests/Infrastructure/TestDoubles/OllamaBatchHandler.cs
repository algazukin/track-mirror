// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using System.Net;
using System.Text;
using System.Text.Json;

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class OllamaBatchHandler(Func<int, int[], HttpResponseMessage> respond) : HttpMessageHandler
{
    internal int Requests { get; private set; }

    internal int BlockAfter { get; init; } = int.MaxValue;

    internal TaskCompletionSource Blocked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));

        using var content = JsonDocument.Parse(body.RootElement.GetProperty("messages")[1]
            .GetProperty("content").GetString()!);

        var ids = content.RootElement.GetProperty("ocr_blocks").EnumerateArray()
            .Select(block => block[0].GetInt32()).ToArray();

        Requests++;

        if (Requests > BlockAfter)
        {
            Blocked.TrySetResult();

            await Task.Delay(Timeout.Infinite, token);
        }

        return respond(Requests, ids);
    }

    internal static HttpResponseMessage ContextError() => new(HttpStatusCode.BadRequest)
    {
        Content = new StringContent("{\"error\":\"exceeds the available context size\"}")
    };

    internal static HttpResponseMessage Success(int[] ids, bool metrics = true)
    {
        var envelope = new Dictionary<string, object>
        {
            ["done"] = true,
            ["done_reason"] = "stop",
            ["message"] = new
            {
                content = JsonSerializer.Serialize(new
                {
                    uncertain = false,
                    findings = ids.Select(id => new
                    {
                        category = "credentials", severity = "high", block_ids = new[] { id }
                    })
                })
            }
        };

        if (metrics)
        {
            envelope["prompt_eval_count"] = 123;

            envelope["eval_count"] = 20;
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(envelope), Encoding.UTF8, "application/json")
        };
    }
}
