// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Drawing.Imaging;

namespace TrackMirror.IntegrationTests.Infrastructure;

internal static class TestImages
{
    // The explicit codec avoids the unsynchronized first-use cache in .NET 10's ImageFormat encoder lookup.
    internal static readonly ImageCodecInfo JpegEncoder = ImageCodecInfo.GetImageEncoders()
        .Single(encoder => encoder.FormatID == ImageFormat.Jpeg.Guid);

    internal static byte[] Jpeg(int width = 160, int height = 100)
    {
        using var image = new Bitmap(width, height);

        using (var graphics = Graphics.FromImage(image))
        {
            graphics.Clear(Color.CornflowerBlue);
        }

        using var stream = new MemoryStream();

        image.Save(stream, JpegEncoder, null);

        return stream.ToArray();
    }
}
