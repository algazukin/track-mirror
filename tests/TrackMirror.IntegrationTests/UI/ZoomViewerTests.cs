// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.UI.Forms;
using TrackMirror.UI.Controls;
using System.Reflection;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using System.Globalization;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class ZoomViewerTests
{
    [Fact]
    public void Large_image_opens_with_every_edge_visible_without_manual_window_enlargement()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var form = new ZoomForm("fixture", new Bitmap(2200, 1300), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            // Act
            TestWindow.Show(form);

            form.PerformLayout();

            var picture = ImageControl(form);

            // Assert
            Assert.True(picture.ImageBounds.Left >= 0 && picture.ImageBounds.Top >= 0);

            Assert.True(picture.ImageBounds.Right <= picture.ClientSize.Width,
                "The right image edge is clipped when the viewer first opens.");

            Assert.True(picture.ImageBounds.Bottom <= picture.ClientSize.Height,
                "The bottom image edge is clipped when the viewer first opens.");
        });
    }

    [Theory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    public void Native_size_can_be_selected_and_fit_restores_all_edges_after_resize(string language)
    {
        StaThread.Run(() =>
        {
            // Arrange
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(language);

            using var form = new ZoomForm("fixture", new Bitmap(2200, 1300), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            // Act
            TestWindow.Show(form);

            var buttons = Descendants(form).OfType<Button>().ToList();

            // Assert
            Assert.Contains(buttons, button => button.Text == "100%");

            // Act
            buttons.Single(button => button.Text == "100%").PerformClick();

            // Assert
            Assert.Equal(new Size(2200, 1300), ImageControl(form).ImageBounds.Size);

            // Act
            form.ClientSize = new Size(640, 420);

            UiControls.Field<Button>(form, "_fit").PerformClick();

            var picture = ImageControl(form);

            // Assert
            Assert.True(picture.ClientRectangle.Contains(picture.ImageBounds));

            // Act
            form.ClientSize = new Size(850, 520);

            // Assert
            Assert.True(picture.ClientRectangle.Contains(picture.ImageBounds));
        });
    }

    [Theory]
    [InlineData(3000, 900)]
    [InlineData(900, 3000)]
    public void Fit_paints_all_four_image_corners_for_wide_and_tall_screenshots(int width, int height)
    {
        StaThread.Run(() =>
        {
            // Arrange
            var bitmap = new Bitmap(width, height);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.FillRectangle(Brushes.Red, 0, 0, width / 2, height / 2);

                graphics.FillRectangle(Brushes.Lime, width / 2, 0, width / 2, height / 2);

                graphics.FillRectangle(Brushes.Blue, 0, height / 2, width / 2, height / 2);

                graphics.FillRectangle(Brushes.Gold, width / 2, height / 2, width / 2, height / 2);
            }

            using var form = new ZoomForm("fixture", bitmap, DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            // Act
            TestWindow.Show(form);

            var view = ImageControl(form);

            form.ClientSize = new Size(640, 420);

            using var rendered = new Bitmap(view.Width, view.Height);

            view.DrawToBitmap(rendered, view.ClientRectangle);

            var bounds = view.ImageBounds;

            // Assert
            Assert.Equal(Color.Red.ToArgb(), rendered.GetPixel(bounds.Left + bounds.Width / 4,
                bounds.Top + bounds.Height / 4).ToArgb());

            Assert.Equal(Color.Lime.ToArgb(), rendered.GetPixel(bounds.Left + bounds.Width * 3 / 4,
                bounds.Top + bounds.Height / 4).ToArgb());

            Assert.Equal(Color.Blue.ToArgb(), rendered.GetPixel(bounds.Left + bounds.Width / 4,
                bounds.Top + bounds.Height * 3 / 4).ToArgb());

            Assert.Equal(Color.Gold.ToArgb(), rendered.GetPixel(bounds.Left + bounds.Width * 3 / 4,
                bounds.Top + bounds.Height * 3 / 4).ToArgb());
        });
    }

    [Fact]
    public void Small_image_is_centered_at_native_size_without_unrequested_enlargement()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var form = new ZoomForm("fixture", new Bitmap(320, 180), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            // Act
            TestWindow.Show(form);

            // Assert
            Assert.True(Screen.FromControl(form).WorkingArea.Contains(form.Bounds));

            // Act
            form.ClientSize = new Size(1000, 800);

            var view = ImageControl(form);

            // Assert
            Assert.Equal(new Size(320, 180), view.ImageBounds.Size);

            Assert.Equal((view.Width - 320) / 2, view.ImageBounds.Left);

            Assert.Equal((view.Height - 180) / 2, view.ImageBounds.Top);

            Assert.Equal(1, view.ZoomFactor);
        });
    }

    [Fact]
    public void Zoom_preserves_the_image_point_under_the_cursor_and_drag_moves_the_enlarged_image()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var form = new ZoomForm("fixture", new Bitmap(2200, 1300), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(form);

            form.ClientSize = new Size(640, 420);

            var view = ImageControl(form);

            // Act
            view.ZoomAt(1, view.ViewCenter);

            Command(view, Keys.Home);

            // Assert
            Assert.Equal(Point.Empty, view.ImageBounds.Location);

            // Act
            view.ZoomAt(2, new Point(230, 180));

            // Assert
            Assert.Equal(new Point(-230, -180), view.ImageBounds.Location);

            // Act
            Mouse(view, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 300, 200, 0));

            Mouse(view, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, 260, 170, 0));

            // Assert
            Assert.Equal(new Point(-270, -210), view.ImageBounds.Location);

            // Act
            Mouse(view, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, 260, 170, 0));

            // Assert
            Assert.False(view.Capture);

            // Act
            Mouse(view, "OnMouseMove", new MouseEventArgs(MouseButtons.None, 0, 100, 100, 0));

            // Assert
            Assert.Equal(new Point(-270, -210), view.ImageBounds.Location);

            // Act
            view.Fit();

            // Assert
            Assert.True(view.ClientRectangle.Contains(view.ImageBounds));

            Assert.All(view.Controls.OfType<CaptureScrollBar>(), bar => Assert.False(bar.Visible));
        });
    }

    [Fact]
    public void Keyboard_zoom_buttons_and_bounds_work_while_native_size_survives_window_resize()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var form = new ZoomForm("fixture", new Bitmap(2200, 1300), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(form);

            var view = ImageControl(form);

            // Act
            Command(form, Keys.Control | Keys.D1);

            // Assert
            Assert.Equal(1, view.ZoomFactor);

            // Act
            form.ClientSize = new Size(640, 420);

            // Assert
            Assert.Equal(new Size(2200, 1300), view.ImageBounds.Size);

            // Act
            Command(form, Keys.Control | Keys.Oemplus);

            // Assert
            Assert.Equal(1.25, view.ZoomFactor);

            // Act
            Descendants(form).OfType<Button>().Single(button => button.Text == "−").PerformClick();

            // Assert
            Assert.Equal(1, view.ZoomFactor);

            // Act
            Descendants(form).OfType<Button>().Single(button => button.Text == "+").PerformClick();

            // Assert
            Assert.Equal(1.25, view.ZoomFactor);

            // Act
            Command(form, Keys.Control | Keys.OemMinus);

            // Assert
            Assert.Equal(1, view.ZoomFactor);

            // Act
            view.ZoomAt(100, view.ViewCenter);

            // Assert
            Assert.Equal(4, view.ZoomFactor);

            // Act
            view.ZoomAt(0, view.ViewCenter);

            // Assert
            Assert.True(view.ZoomFactor > 0);

            // Act
            Command(form, Keys.Control | Keys.D0);

            // Assert
            Assert.True(view.IsFit);

            Assert.True(view.ClientRectangle.Contains(view.ImageBounds));
        });
    }

    private static void Command(Control control, Keys keys)
    {
        var method = control.GetType().GetMethod("ProcessCmdKey", BindingFlags.Instance | BindingFlags.NonPublic)!;

        var arguments = new object[] { new Message(), keys };

        Assert.True((bool)method.Invoke(control, arguments)!);
    }

    [Fact]
    public void Partial_wheel_input_zooms_at_the_cursor_and_plain_wheel_scrolls_without_changing_scale()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var form = new ZoomForm("fixture", new Bitmap(2200, 1300), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(form);

            form.ClientSize = new Size(640, 420);

            var view = ImageControl(form);

            view.ZoomAt(1, view.ViewCenter);

            Command(view, Keys.Home);

            // Act
            view.HandleWheel(60, new Point(200, 150), Keys.Control);

            // Assert
            Assert.Equal(1, view.ZoomFactor);

            // Act
            view.HandleWheel(60, new Point(200, 150), Keys.Control);

            // Assert
            Assert.Equal(1.25, view.ZoomFactor);

            Assert.Equal(new Point(-50, -38), view.ImageBounds.Location);

            // Act
            view.HandleWheel(-120, new Point(200, 150), Keys.Control);

            // Assert
            Assert.Equal(1, view.ZoomFactor);

            Assert.Equal(Point.Empty, view.ImageBounds.Location);

            // Act
            view.HandleWheel(-60, view.ViewCenter, Keys.None);

            // Assert
            Assert.Equal(Point.Empty, view.ImageBounds.Location);

            // Act
            view.HandleWheel(-60, view.ViewCenter, Keys.None);

            // Assert
            Assert.True(view.ImageBounds.Top < 0);

            Assert.Equal(0, view.ImageBounds.Left);

            Assert.Equal(1, view.ZoomFactor);
        });
    }

    [Fact]
    public void Wide_native_image_scrolls_horizontally_with_the_wheel_when_it_has_no_vertical_overflow()
    {
        StaThread.Run(() =>
        {
            // Arrange
            using var form = new ZoomForm("fixture", new Bitmap(2200, 80), DateTime.UtcNow)
            {
                Opacity = 0, ShowInTaskbar = false
            };

            TestWindow.Show(form);

            form.ClientSize = new Size(640, 420);

            var view = ImageControl(form);

            view.ZoomAt(1, view.ViewCenter);

            Command(view, Keys.Home);

            var top = view.ImageBounds.Top;

            // Act
            view.HandleWheel(-120, view.ViewCenter, Keys.None);

            // Assert
            Assert.True(view.ImageBounds.Left < 0);

            Assert.Equal(top, view.ImageBounds.Top);

            Assert.Equal(1, view.ZoomFactor);
        });
    }

    private static void Mouse(Control control, string method, MouseEventArgs args) =>
        control.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(control, [args]);

    private static ZoomImageView ImageControl(Control root) => Descendants(root).OfType<ZoomImageView>().Single();

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control child in root.Controls)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
