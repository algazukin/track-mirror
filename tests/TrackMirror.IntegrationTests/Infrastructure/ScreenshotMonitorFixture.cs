// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Threading.Channels;
using TrackMirror.Models;
using TrackMirror.Monitoring;
using TrackMirror.Storage;

namespace TrackMirror.IntegrationTests.Infrastructure;

internal sealed class ScreenshotMonitorFixture : IAsyncDisposable
{
    internal TestWorkspace Source { get; } = new();

    internal TestWorkspace Data { get; } = new();

    internal ScreenshotStore Store { get; }

    internal ScreenshotMonitor Monitor { get; }

    internal byte[] Image { get; }

    internal Channel<(CaptureEntry Entry, bool Notify)> Saved { get; } =
        Channel.CreateUnbounded<(CaptureEntry, bool)>();

    internal Channel<string> Warnings { get; } = Channel.CreateUnbounded<string>();

    internal Channel<string> Retries { get; } = Channel.CreateUnbounded<string>();

    private readonly Channel<string> _statuses = Channel.CreateUnbounded<string>();

    private Task? stop;

    internal ScreenshotMonitorFixture()
    {
        Image = TestImages.Jpeg();

        Store = new ScreenshotStore(Data.Root);

        Monitor = new ScreenshotMonitor(Source.Root, Store);

        Monitor.CaptureSaved += (entry, notify) => Saved.Writer.TryWrite((entry, notify));

        Monitor.Warning += warning => Warnings.Writer.TryWrite(warning);

        Monitor.CopyRetrying += path => Retries.Writer.TryWrite(path);

        Monitor.StatusChanged += status => _statuses.Writer.TryWrite(status);
    }

    internal string Write(string relative, byte[]? content = null)
    {
        var path = Path.GetFullPath(Path.Combine(Source.Root, relative));

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        File.WriteAllBytes(path, content ?? Image);

        return path;
    }

    internal async Task StartAsync()
    {
        Monitor.Start();

        await _statuses.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
    }

    internal Task<(CaptureEntry Entry, bool Notify)> NextAsync() =>
        Saved.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

    internal Task StopAsync() => stop ??= Monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

    internal static async Task WaitForFileAsync(string path)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        while (!File.Exists(path))
        {
            await Task.Delay(20, deadline.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();

        Data.Dispose();

        Source.Dispose();
    }
}
