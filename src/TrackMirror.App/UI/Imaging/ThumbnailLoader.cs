// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Drawing2D;
using System.Threading.Channels;
using TrackMirror.Models;
using TrackMirror.Storage;

namespace TrackMirror.UI.Imaging;

internal sealed class ThumbnailLoader : IDisposable
{
    private readonly ScreenshotStore _store;
    private readonly Channel<(CaptureEntry Entry, int Generation)> _requests =
        Channel.CreateUnbounded<(CaptureEntry, int)>(new UnboundedChannelOptions { SingleReader = true });
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _worker;
    private int generation;
    private int disposed;

    internal event Action<CaptureEntry, int, Bitmap?>? Loaded;

    internal ThumbnailLoader(ScreenshotStore store)
    {
        _store = store;

        _worker = Task.Run(ProcessAsync);
    }

    internal void SetGeneration(int value) => Volatile.Write(ref generation, value);

    internal void Request(CaptureEntry entry, int value) => _requests.Writer.TryWrite((entry, value));

    private async Task ProcessAsync()
    {
        try
        {
            await foreach (var request in _requests.Reader.ReadAllAsync(_stop.Token).ConfigureAwait(false))
            {
                if (request.Generation != Volatile.Read(ref generation))
                    continue;

                Bitmap? thumbnail = null;

                try
                {
                    using var image = _store.OpenImage(request.Entry);

                    var scale = Math.Min(480d / image.Width, 270d / image.Height);

                    thumbnail = new Bitmap(Math.Max(1, (int)(image.Width * scale)),
                        Math.Max(1, (int)(image.Height * scale)));

                    using var graphics = Graphics.FromImage(thumbnail);

                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;

                    graphics.DrawImage(image, new Rectangle(Point.Empty, thumbnail.Size));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                    or ArgumentException or OutOfMemoryException)
                {
                    thumbnail?.Dispose();

                    thumbnail = null;
                }

                if (_stop.IsCancellationRequested || request.Generation != Volatile.Read(ref generation))
                {
                    thumbnail?.Dispose();

                    continue;
                }

                Loaded?.Invoke(request.Entry, request.Generation, thumbnail);
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested)
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
            return;

        _stop.Cancel();

        _requests.Writer.TryComplete();

        _ = _worker.ContinueWith(_ => _stop.Dispose(), TaskScheduler.Default);
    }
}
