// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using RapidOcrNet;
using SkiaSharp;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using OcrResult = TrackMirror.Privacy.Models.OcrResult;

namespace TrackMirror.Privacy.Ocr;

internal sealed class LocalOcrEngine : IDisposable
{
    private readonly RapidOcr _ocr = new();

    internal LocalOcrEngine()
    {
        var defaults = Path.Combine(AppContext.BaseDirectory, PrivacyNames.ModelsDirectory,
            PrivacyNames.DefaultOcrDirectory);

        var recognition = Path.Combine(AppContext.BaseDirectory, PrivacyNames.OcrDirectory);

        using var options = RapidOcr.GetDefaultSessionOptions(2);

        try
        {
            _ocr.InitModels(Path.Combine(defaults, PrivacyNames.DetectionModel),
                Path.Combine(defaults, PrivacyNames.AngleModel),
                Path.Combine(recognition, PrivacyNames.RecognitionModel),
                Path.Combine(recognition, PrivacyNames.Dictionary), options);
        }
        catch
        {
            _ocr.Dispose();

            throw;
        }
    }

    internal OcrResult Read(byte[] bytes, CancellationToken cancellationToken)
    {
        using var image = SKBitmap.Decode(bytes);

        if (image == null || image.Width < 1 || image.Height < 1 || (long)image.Width * image.Height > 40_000_000)
            throw new InvalidDataException(PrivacyNames.OcrFailure);

        var result = new List<OcrBlock>();

        var tiles = 0;

        for (var y = 0; y < image.Height; y += 820)
        {
            for (var x = 0; x < image.Width; x += 1140)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (++tiles > 40)
                    return new OcrResult(result, true);

                using var tile = new SKBitmap();

                var bounds = new SKRectI(x, y, Math.Min(image.Width, x + 1280), Math.Min(image.Height, y + 960));

                if (!image.ExtractSubset(tile, bounds))
                    throw new InvalidDataException(PrivacyNames.OcrFailure);

                var detected = _ocr.Detect(tile, RapidOcrOptions.Default with
                {
                    ImgResize = 1280,
                    DoAngle = false,
                    TextScore = 0.45f
                });

                foreach (var block in detected.TextBlocks)
                {
                    if (result.Count >= 300)
                        return new OcrResult(result, true);

                    var left = Math.Clamp(x + block.BoxPoints.Min(point => point.X), 0, image.Width - 1);

                    var top = Math.Clamp(y + block.BoxPoints.Min(point => point.Y), 0, image.Height - 1);

                    var right = Math.Clamp(x + block.BoxPoints.Max(point => point.X), left + 1, image.Width);

                    var bottom = Math.Clamp(y + block.BoxPoints.Max(point => point.Y), top + 1, image.Height);

                    if (result.Any(previous => previous.Text == block.Text
                        && Math.Abs(previous.Region.X * image.Width - left) < 30
                        && Math.Abs(previous.Region.Y * image.Height - top) < 30))
                        continue;

                    var region = new PrivacyRegion((float)left / image.Width, (float)top / image.Height,
                        (float)(right - left) / image.Width, (float)(bottom - top) / image.Height);

                    result.Add(new OcrBlock(result.Count, block.Text, region));
                }
            }
        }

        return new OcrResult(result.OrderBy(block => block.Region.Y).ThenBy(block => block.Region.X).ToList(),
            false);
    }

    public void Dispose() => _ocr.Dispose();
}
