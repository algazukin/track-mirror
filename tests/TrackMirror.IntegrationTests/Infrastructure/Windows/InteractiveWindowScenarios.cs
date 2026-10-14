// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.InteropServices;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Forms;
using Xunit;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class InteractiveWindowScenarios
{
    internal static async Task RunAsync(HistoryForm history, PreviewForm preview, ScreenshotStore store,
        CaptureEntry entry, string scenario)
    {
        // Arrange
        if (scenario == "focus")
        {
            await CheckFocusAsync(preview, entry);

            return;
        }

        if (scenario.StartsWith("preview-", StringComparison.Ordinal))
        {
            if (scenario != "preview-startup")
            {
                history.ShowHistory();

                if (scenario == "preview-hidden")
                {
                    history.Close();
                }
                else
                {
                    history.WindowState = FormWindowState.Minimized;
                }
            }

            preview.ShowCapture(entry);

            var open = UiControls.Descendants<Button>(preview).Single(button => button.Text == UiText.Open);

            // Act
            open.PerformClick();

            await UiControls.NextTurn(history);

            // Assert
            Assert.True(history.Visible);

            Assert.Equal(FormWindowState.Normal, history.WindowState);

            Assert.Equal(entry.Id, UiControls.Field<CaptureEntry>(history, "selected").Id);

            Assert.True(Assert.Single(store.Snapshot()).Viewed);

            Assert.False(UiControls.Field<Button>(history, "_reviewed").Enabled);

            Assert.False(preview.Visible);

            return;
        }

        history.ShowHistory(entry.Id);

        // Act
        UiControls.Field<Button>(history, "_zoom").PerformClick();

        UiControls.Field<Button>(history, "_privacyButton").PerformClick();

        // Assert
        var zoom = Assert.Single(history.OwnedForms.OfType<ZoomForm>());

        var result = Assert.Single(history.OwnedForms.OfType<PrivacyResultForm>());

        var position = new Point(Screen.FromControl(history).WorkingArea.Left + 80,
            Screen.FromControl(history).WorkingArea.Top + 80);

        zoom.Location = result.Location = position;

        // Act
        for (var i = 0; i < 3; i++)
        {
            UiControls.Field<Button>(history, "_zoom").PerformClick();

            UiControls.Field<Button>(history, "_privacyButton").PerformClick();
        }

        // Assert
        Assert.Same(zoom, Assert.Single(history.OwnedForms.OfType<ZoomForm>()));

        Assert.Same(result, Assert.Single(history.OwnedForms.OfType<PrivacyResultForm>()));

        Assert.Equal(position, zoom.Location);

        Assert.Equal(position, result.Location);

        Assert.False(zoom.IsDisposed || result.IsDisposed);

        // Act
        zoom.WindowState = result.WindowState = FormWindowState.Minimized;

        UiControls.Field<Button>(history, "_zoom").PerformClick();

        UiControls.Field<Button>(history, "_privacyButton").PerformClick();

        // Assert
        Assert.Equal(FormWindowState.Normal, zoom.WindowState);

        Assert.Equal(FormWindowState.Normal, result.WindowState);
    }

    private static async Task CheckFocusAsync(PreviewForm preview, CaptureEntry entry)
    {
        // Arrange
        var previous = GetForegroundWindow();

        using var editor = new Form { ClientSize = new Size(280, 100), Text = "Synthetic focus fixture" };

        using var input = new TextBox { Text = "Synthetic editable text", Dock = DockStyle.Top };

        editor.Controls.Add(input);

        try
        {
            editor.Show();

            editor.Activate();

            input.Focus();

            input.Select(4, 0);

            Assert.Equal(editor.Handle, GetForegroundWindow());

            // Act
            preview.ShowCapture(entry);

            await UiControls.NextTurn(editor);

            // Assert
            Assert.Equal(editor.Handle, GetForegroundWindow());

            Assert.True(input.Focused);

            Assert.Equal(4, input.SelectionStart);

            Assert.Equal("Synthetic editable text", input.Text);
        }
        finally
        {
            editor.Close();

            if (previous != IntPtr.Zero)
            {
                SetForegroundWindow(previous);
            }
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr window);
}
