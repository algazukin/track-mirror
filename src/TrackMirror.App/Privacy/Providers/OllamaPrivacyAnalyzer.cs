// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Net;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Analysis;

namespace TrackMirror.Privacy.Providers;

internal sealed class OllamaPrivacyAnalyzer : IAiPrivacyAnalyzer, IDisposable
{
    private static readonly JsonSerializerOptions promptJson = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };
    private readonly LocalModelRuntime _runtime = new();
    private readonly HttpClient _client;
    private readonly Func<HttpClient, string, CancellationToken, Task> _ensureReady;

    internal OllamaPrivacyAnalyzer(HttpClient? client = null,
        Func<HttpClient, string, CancellationToken, Task>? ensureReady = null)
    {
        _client = client ?? new HttpClient(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(PrivacyNames.LocalEndpoint),
            Timeout = TimeSpan.FromSeconds(120),
            MaxResponseContentBufferSize = 256 * 1024
        };

        _ensureReady = ensureReady ?? _runtime.EnsureReadyAsync;
    }

    public async Task<AiPrivacyAnalysis> AnalyzeAsync(PrivacyAnalysisInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!input.Games && !input.PersonalChats && !input.ConfidentialData)
            return new AiPrivacyAnalysis(false, []);

        try
        {
            await _ensureReady(_client, input.Model, cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new PrivacyScanException(PrivacyNames.ModelTimeout);
        }
        catch (HttpRequestException)
        {
            throw new PrivacyScanException(PrivacyNames.ModelConnection);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new PrivacyScanException(PrivacyNames.InvalidResponse);
        }

        var findings = new List<PrivacyFinding>();
        var diagnostics = new List<PrivacyRequestDiagnostic>();
        var image = Convert.ToBase64String(input.Image);
        var uncertain = false;
        var failure = string.Empty;

        void ReportProgress()
        {
            input.Checkpoint?.Invoke(new AiPrivacyAnalysis(uncertain, findings.ToList(),
                cancellationToken.IsCancellationRequested ? PrivacyNames.Cancelled : failure)
            {
                Diagnostics = diagnostics.ToList()
            });
        }

        if (input.Games || input.PersonalChats)
        {
            try
            {
                var schema = JsonNode.Parse(PrivacyProtocol.ActivitySchema)!;

                var properties = schema[PrivacyProtocol.Properties]!.AsObject();

                if (!input.Games)
                {
                    properties.Remove(PrivacyRules.Game);
                }

                if (!input.PersonalChats)
                {
                    properties.Remove(PrivacyRules.PersonalChat);
                }

                schema[PrivacyProtocol.Required] = new JsonArray(properties.Select(property =>
                    (JsonNode?)JsonValue.Create(property.Key)).ToArray());

                var task = JsonSerializer.Serialize(new
                {
                    task = PrivacyProtocol.ActivityTask,
                    game_enabled = input.Games,
                    personal_chat_enabled = input.PersonalChats
                });

                using var activity = await RequestAsync(image, input.Model, PrivacyProtocol.ActivityPrompt,
                    task, schema, [], diagnostics, cancellationToken);

                var root = activity.RootElement;
                var ambiguous = root.GetProperty(PrivacyProtocol.Uncertain).GetBoolean();
                var game = input.Games && root.GetProperty(PrivacyRules.Game).GetBoolean();
                var chat = input.PersonalChats && root.GetProperty(PrivacyRules.PersonalChat).GetBoolean();

                uncertain = ambiguous;

                if (ambiguous)
                {
                    diagnostics[^1] = diagnostics[^1] with { FailureCode = PrivacyNames.ModelUncertain };
                }

                if (game)
                {
                    findings.Add(new PrivacyFinding(PrivacyRules.Game, PrivacyRules.Medium,
                        PrivacyRules.ContextReason, []));
                }

                if (chat)
                {
                    findings.Add(new PrivacyFinding(PrivacyRules.PersonalChat, PrivacyRules.Medium,
                        PrivacyRules.ContextReason, []));
                }
            }
            catch (Exception ex) when (ex is PrivacyScanException or JsonException
                or InvalidOperationException or KeyNotFoundException)
            {
                failure = ex is PrivacyScanException error ? error.Code : PrivacyNames.InvalidResponse;

                if (diagnostics.Count > 0)
                {
                    diagnostics[^1] = diagnostics[^1] with { FailureCode = failure };
                }
            }
            finally
            {
                ReportProgress();
            }
        }

        var batches = input.ConfidentialData ? PrivacyOcrBatches.Create(input.Blocks)
            : new Queue<IReadOnlyList<OcrBlock>>();

        for (var attempt = 0; batches.Count > 0 && attempt < PrivacyProtocol.MaxPrivacyRequests; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var blocks = batches.Dequeue();

            try
            {
                using var privacy = await RequestPrivacyAsync(image, input.Model, blocks, diagnostics,
                    cancellationToken);

                var ambiguous = privacy.RootElement.GetProperty(PrivacyProtocol.Uncertain).GetBoolean();

                uncertain |= ambiguous;

                if (ambiguous)
                {
                    diagnostics[^1] = diagnostics[^1] with { FailureCode = PrivacyNames.ModelUncertain };
                }

                findings = PrivacyFindings.Merge(findings.Concat(ReadPrivateFindings(privacy.RootElement, blocks)));

                if (findings.Count > 400)
                {
                    findings = findings.Take(400).ToList();

                    failure = PrivacyNames.AnalysisLimit;

                    break;
                }
            }
            catch (PrivacyScanException ex) when (ex.Code is PrivacyNames.ModelContext
                or PrivacyNames.TruncatedResponse)
            {
                var smaller = PrivacyOcrBatches.Split(blocks);

                if (smaller.Length == 0)
                {
                    failure = ex.Code;

                    break;
                }

                foreach (var batch in smaller)
                {
                    batches.Enqueue(batch);
                }
            }
            catch (Exception ex) when (ex is PrivacyScanException or JsonException
                or InvalidOperationException or KeyNotFoundException)
            {
                failure = ex is PrivacyScanException error ? error.Code : PrivacyNames.InvalidResponse;

                if (diagnostics.Count > 0)
                {
                    diagnostics[^1] = diagnostics[^1] with { FailureCode = failure };
                }

                break;
            }
            finally
            {
                ReportProgress();
            }
        }

        if (batches.Count > 0 && failure.Length == 0)
        {
            failure = PrivacyNames.AnalysisLimit;
        }

        return new AiPrivacyAnalysis(uncertain, findings, failure) { Diagnostics = diagnostics };
    }

    private Task<JsonDocument> RequestPrivacyAsync(string image, string model, IReadOnlyList<OcrBlock> blocks,
        List<PrivacyRequestDiagnostic> diagnostics, CancellationToken cancellationToken)
    {
        var text = PrivacyOcrBatches.Serialize(blocks);

        var ids = blocks.Select(block => block.Id).Distinct().ToArray();

        var schema = JsonNode.Parse(PrivacyProtocol.Schema)!;
        var evidence = schema[PrivacyProtocol.Properties]![PrivacyProtocol.Findings]![PrivacyProtocol.Items]!
            [PrivacyProtocol.Properties]![PrivacyProtocol.BlockIds]!;

        evidence[PrivacyProtocol.MaxItems] = Math.Min(8, ids.Length);

        if (ids.Length > 0)
        {
            evidence[PrivacyProtocol.Items]![PrivacyProtocol.Enum] = new JsonArray(ids
                .Select(id => (JsonNode?)JsonValue.Create(id)).ToArray());
        }

        return RequestAsync(image, model, PrivacyProtocol.Prompt, text, schema, blocks, diagnostics, cancellationToken);
    }

    private async Task<JsonDocument> RequestAsync(string image, string model, string prompt, string text,
        JsonNode schema, IReadOnlyList<OcrBlock> blocks, List<PrivacyRequestDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model,
            stream = false,
            think = false,
            keep_alive = 120,
            options = new { temperature = 0, num_predict = PrivacyProtocol.ReplyTokens,
                num_ctx = PrivacyProtocol.ContextTokens },
            format = schema,
            messages = new object[]
            {
                new { role = PrivacyProtocol.SystemRole, content = prompt },
                new { role = PrivacyProtocol.UserRole, content = text, images = new[] { image } }
            }
        };

        var payload = JsonSerializer.SerializeToNode(request, promptJson)!.AsObject();

        if (!model.StartsWith(PrivacyModelNames.QwenPrefix, StringComparison.Ordinal))
        {
            payload.Remove(PrivacyProtocol.Think);
        }

        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, promptJson);

        var diagnostic = new PrivacyRequestDiagnostic
        {
            RequestBytes = bytes.Length,
            OcrBlocks = blocks.Select(block => block.Id).Distinct().Count(),
            OcrCharacters = blocks.Sum(block => block.Text.Length)
        };

        var timer = Stopwatch.StartNew();

        try
        {
            using var content = new ByteArrayContent(bytes);

            content.Headers.ContentType = new MediaTypeHeaderValue(PrivacyProtocol.ApplicationJson);

            using var response = await _client.PostAsync(PrivacyProtocol.ChatPath, content, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
                throw new PrivacyScanException(ReadHttpFailure(response.StatusCode, body));

            using var envelope = JsonDocument.Parse(body);
            var root = envelope.RootElement;

            diagnostic = diagnostic with
            {
                PromptTokens = root.TryGetProperty(PrivacyProtocol.PromptTokens, out var input)
                    && input.ValueKind == JsonValueKind.Number && input.TryGetInt32(out var inputCount)
                    && inputCount >= 0 ? inputCount : null,
                ReplyTokens = root.TryGetProperty(PrivacyProtocol.ReplyTokenCount, out var output)
                    && output.ValueKind == JsonValueKind.Number && output.TryGetInt32(out var outputCount)
                    && outputCount >= 0 ? outputCount : null
            };

            var reason = root.GetProperty(PrivacyProtocol.DoneReason).GetString();

            if (reason == PrivacyProtocol.Length)
                throw new PrivacyScanException(PrivacyNames.TruncatedResponse);

            if (!root.GetProperty(PrivacyProtocol.Done).GetBoolean() || reason != PrivacyProtocol.Stop)
                throw new PrivacyScanException(PrivacyNames.InvalidResponse);

            var answer = root.GetProperty(PrivacyProtocol.Message).GetProperty(PrivacyProtocol.Content).GetString();

            if (answer == null || answer.Length > 16000)
                throw new PrivacyScanException(PrivacyNames.InvalidResponse);

            return JsonDocument.Parse(answer);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            diagnostic = diagnostic with { FailureCode = PrivacyNames.ModelTimeout };

            throw new PrivacyScanException(PrivacyNames.ModelTimeout);
        }
        catch (OperationCanceledException)
        {
            diagnostic = diagnostic with { FailureCode = PrivacyNames.Cancelled };

            throw;
        }
        catch (HttpRequestException)
        {
            diagnostic = diagnostic with { FailureCode = PrivacyNames.ModelConnection };

            throw new PrivacyScanException(PrivacyNames.ModelConnection);
        }
        catch (PrivacyScanException ex)
        {
            diagnostic = diagnostic with { FailureCode = ex.Code };

            throw;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            diagnostic = diagnostic with { FailureCode = PrivacyNames.InvalidResponse };

            throw new PrivacyScanException(PrivacyNames.InvalidResponse);
        }
        finally
        {
            diagnostics.Add(diagnostic with { DurationMilliseconds = timer.Elapsed.TotalMilliseconds });
        }
    }

    private static string ReadHttpFailure(HttpStatusCode status, string body)
    {
        var code = status == HttpStatusCode.NotFound ? PrivacyNames.ModelUnavailable
            : (int)status >= 500 ? PrivacyNames.ModelServer : PrivacyNames.ModelRequest;

        try
        {
            using var error = JsonDocument.Parse(body);
            var message = error.RootElement.ValueKind == JsonValueKind.Object
                && error.RootElement.TryGetProperty(PrivacyProtocol.Error, out var value)
                && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

            if (PrivacyProtocol.MemoryErrors.Any(phrase => message.Contains(phrase,
                StringComparison.OrdinalIgnoreCase)))
            {
                code = PrivacyNames.ModelMemory;
            }
            else if (PrivacyProtocol.ContextErrors.Any(phrase =>
                message.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            {
                code = PrivacyNames.ModelContext;
            }
        }
        catch (JsonException)
        {
            // Keep the HTTP failure code when the server does not return a JSON error body.
        }

        return code;
    }

    private static List<PrivacyFinding> ReadPrivateFindings(JsonElement root, IReadOnlyList<OcrBlock> blocks)
    {
        var findings = new List<PrivacyFinding>();

        foreach (var finding in root.GetProperty(PrivacyProtocol.Findings).EnumerateArray())
        {
            var category = finding.GetProperty(PrivacyProtocol.Category).GetString()!;
            var severity = finding.GetProperty(PrivacyProtocol.Severity).GetString()!;

            if (category is not (PrivacyRules.Credentials or PrivacyRules.PersonalData
                or PrivacyRules.SensitiveDocument)
                || severity is not (PrivacyRules.High or PrivacyRules.Medium))
                throw new PrivacyScanException(PrivacyNames.InvalidResponse);

            var ids = new List<int>();

            foreach (var value in finding.GetProperty(PrivacyProtocol.BlockIds).EnumerateArray())
            {
                if (!value.TryGetInt32(out var id) || blocks.All(block => block.Id != id))
                    throw new PrivacyScanException(PrivacyNames.InvalidEvidence);

                ids.Add(id);
            }

            if (ids.Count > 8)
                throw new PrivacyScanException(PrivacyNames.InvalidEvidence);

            findings.Add(new PrivacyFinding(category, severity, PrivacyRules.ContextReason,
                ids.Distinct().Select(id => blocks.First(block => block.Id == id).Region).ToList()));
        }

        if (findings.Count > 12)
            throw new PrivacyScanException(PrivacyNames.InvalidResponse);

        return findings;
    }

    internal void ReleaseRuntime() => _runtime.Dispose();

    public void Dispose()
    {
        _client.Dispose();

        _runtime.Dispose();
    }
}
