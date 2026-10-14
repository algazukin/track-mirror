// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.Models;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Theme;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

public sealed class CheckBoxRenderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Disabled_option_caption_remains_readable_on_a_dark_background(bool disableParent)
    {
        StaThread.Run(() =>
        {
            // Arrange
            Application.EnableVisualStyles();

            using var settings = new PrivacySettingsPanel(new AppSettings());

            using var host = new Panel { BackColor = UiTheme.Surface, Size = new Size(420, 40) };

            var option = UiControls.Field<CheckBox>(settings, "_automatic");

            option.Dock = DockStyle.Fill;

            using var font = new Font("Segoe UI", 11);

            option.Font = font;

            host.Controls.Add(option);

            using var image = new Bitmap(host.Width, host.Height);

            // Act
            option.Enabled = disableParent;

            host.Enabled = !disableParent;

            host.DrawToBitmap(image, host.ClientRectangle);

            // Assert
            Assert.False(option.Enabled);

            var readablePixels = 0;

            var blackPixels = 0;

            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 40; x < image.Width; x++)
                {
                    var pixel = image.GetPixel(x, y);

                    if (pixel.GetBrightness() >= 0.5f)
                    {
                        readablePixels++;
                    }

                    if (pixel.R <= 16 && pixel.G <= 16 && pixel.B <= 16)
                    {
                        blackPixels++;
                    }
                }
            }

            Assert.True(readablePixels >= 40, "The disabled caption blends into the dark background.");

            Assert.Equal(0, blackPixels);
        });
    }
}
