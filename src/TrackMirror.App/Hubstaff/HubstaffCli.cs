// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using TrackMirror.Constants;

namespace TrackMirror.Hubstaff;

internal sealed class HubstaffCli : IHubstaffCli
{
    public async Task<HubstaffTrackingStatus> StatusAsync(string path, CancellationToken token)
    {
        var text = await RunAsync(path, HubstaffControlNames.Status, token);

        try
        {
            using var document = JsonDocument.Parse(text);

            var root = document.RootElement;

            var tracking = root.GetProperty(HubstaffControlNames.Tracking).GetBoolean();

            var project = root.TryGetProperty(HubstaffControlNames.ActiveProject, out var active) ? active : default;

            var task = root.TryGetProperty(HubstaffControlNames.ActiveTask, out var selected) ? selected : default;

            var taskId = Id(task);

            if (project.ValueKind == JsonValueKind.Object
                && project.TryGetProperty(HubstaffControlNames.TaskId, out var nested))
            {
                var nestedId = Id(nested);

                if (taskId != null && nestedId != null && taskId != nestedId)
                    throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);

                taskId ??= nestedId;
            }

            return new HubstaffTrackingStatus(tracking, Id(project), taskId);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);
        }
    }

    public async Task ExecuteAsync(string path, string command, CancellationToken token)
    {
        if (command is not (HubstaffControlNames.Stop or HubstaffControlNames.Resume))
            throw new ArgumentOutOfRangeException(nameof(command));

        _ = await RunAsync(path, command, token);
    }

    private static string? Id(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        var nested = value.ValueKind == JsonValueKind.Object;

        if (nested)
        {
            if (!value.TryGetProperty(HubstaffControlNames.Id, out var id))
                throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);

            value = id;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number) && number > 0)
            return value.GetRawText();

        if (value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            && text != HubstaffControlNames.NoSelectionId)
            return text;

        if (!nested && ((value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out number) && number == 0)
            || (value.ValueKind == JsonValueKind.String
                && value.GetString() is "" or HubstaffControlNames.NoSelectionId)))
        {
            return null;
        }

        throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);
    }

    private static async Task<string> RunAsync(string path, string command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        var executable = path.Length == 0 ? HubstaffCliLocator.Find() : path;

        if (executable == null || !File.Exists(executable))
            throw new HubstaffControlException(HubstaffControlNames.MissingCli);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);

        timeout.CancelAfter(TimeSpan.FromSeconds(HubstaffControlNames.CommandTimeoutSeconds));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            }
        };

        process.StartInfo.ArgumentList.Add(command);

        try
        {
            process.Start();

            var output = ReadAsync(process.StandardOutput, timeout.Token);

            var errors = ReadAsync(process.StandardError, timeout.Token);

            await Task.WhenAll(output, errors, process.WaitForExitAsync(timeout.Token));

            if (process.ExitCode != 0)
                throw new HubstaffControlException(HubstaffControlNames.CommandFailed);

            var text = await output;

            if (text.Length > 0)
            {
                try
                {
                    using var document = JsonDocument.Parse(text);

                    if (document.RootElement.ValueKind == JsonValueKind.Object
                        && document.RootElement.TryGetProperty(HubstaffControlNames.Error, out var error)
                        && error.ValueKind is not (JsonValueKind.Null or JsonValueKind.False)
                        && (error.ValueKind != JsonValueKind.String || error.GetString()!.Length > 0))
                        throw new HubstaffControlException(HubstaffControlNames.CommandFailed);
                }
                catch (JsonException)
                {
                    // Control acknowledgements need not be JSON; StatusAsync validates status separately.
                }
            }

            return text;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new HubstaffControlException(HubstaffControlNames.TimedOut);
        }
        catch (Exception error) when (error is Win32Exception or IOException or JsonException)
        {
            throw new HubstaffControlException(HubstaffControlNames.CommandFailed);
        }
        finally
        {
            try
            {
                if (process.Id != 0 && !process.HasExited)
                {
                    process.Kill(entireProcessTree: true);

                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                    await process.WaitForExitAsync(cleanup.Token);
                }
            }
            catch (Exception error) when (error is Win32Exception or InvalidOperationException
                or OperationCanceledException)
            {
                // The child can exit between checking and terminating it.
            }
        }
    }

    private static async Task<string> ReadAsync(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder();

        var buffer = new char[2048];

        while (await reader.ReadAsync(buffer.AsMemory(), token) is var count && count > 0)
        {
            if (result.Length + count > HubstaffControlNames.MaximumOutputCharacters)
                throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);

            result.Append(buffer, 0, count);
        }

        return result.ToString();
    }
}
