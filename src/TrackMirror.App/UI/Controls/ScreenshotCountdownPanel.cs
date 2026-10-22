// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ScreenshotCountdownPanel : SettingsSection
{
    private readonly ThemedCheckBox _enabled = new()
    {
        Text = CountdownText.Enabled, AccessibleName = CountdownText.Enabled, Dock = DockStyle.Fill,
        ForeColor = UiTheme.Text, BackColor = UiTheme.Surface, Cursor = Cursors.Hand, Margin = Padding.Empty
    };

    private readonly NumericUpDown _highlight = Number(CountdownText.Highlight, 1, 120, 1);

    private readonly NumericUpDown _scale = Number(CountdownText.Scale, 50, 200, 5);
    private readonly NumericUpDown _opacity = Number(CountdownText.BackgroundOpacity, 0, 100, 5);

    private ScreenshotCountdownOptions current = new();
    private bool loading;

    internal event Action<ScreenshotCountdownOptions>? Changed;

    internal ScreenshotCountdownPanel(ScreenshotCountdownOptions options)
    {
        BackColor = UiTheme.Surface;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 5, Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 32, 32, 0, 126, 0 })
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));

        var appearance = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 3, Margin = Padding.Empty
        };

        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102));
        appearance.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var rows = new[]
        {
            (CountdownText.Highlight, _highlight, SettingsText.Seconds),
            (CountdownText.Scale, _scale, "%"),
            (CountdownText.BackgroundOpacity, _opacity, "%")
        };

        for (var row = 0; row < rows.Length; row++)
        {
            appearance.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var label = UiTheme.Label(rows[row].Item1, 10);
            label.AutoSize = true;
            label.Margin = new Padding(0, 0, 16, 0);

            appearance.Controls.Add(label, 0, row);
            appearance.Controls.Add(rows[row].Item2, 1, row);
            appearance.Controls.Add(UiTheme.Label(rows[row].Item3, 10), 2, row);
        }

        layout.Controls.Add(UiTheme.Label(CountdownText.Heading, 15, true), 0, 0);
        layout.Controls.Add(_enabled, 0, 1);
        layout.Controls.Add(UiTheme.Label(CountdownText.Hint, 9, muted: true, wrap: true), 0, 2);
        layout.Controls.Add(appearance, 0, 3);
        layout.Controls.Add(UiTheme.Label(CountdownText.Limitation, 9, muted: true, wrap: true), 0, 4);

        Controls.Add(layout);

        Apply(options);

        _enabled.CheckedChanged += (_, _) => Publish();
        _highlight.ValueChanged += (_, _) => Publish();
        _scale.ValueChanged += (_, _) => Publish();
        _opacity.ValueChanged += (_, _) => Publish();
    }

    internal void Apply(ScreenshotCountdownOptions value)
    {
        loading = true;

        current = value;

        _enabled.Checked = value.Enabled;
        _highlight.Value = value.HighlightSeconds;
        _scale.Value = value.ScalePercent;
        _opacity.Value = value.BackgroundOpacityPercent;

        _highlight.Enabled = value.Enabled;
        _scale.Enabled = value.Enabled;
        _opacity.Enabled = value.Enabled;

        loading = false;
    }

    private void Publish()
    {
        if (loading)
            return;

        current = current with
        {
            Enabled = _enabled.Checked, HighlightSeconds = (int)_highlight.Value,
            ScalePercent = (int)_scale.Value, BackgroundOpacityPercent = (int)_opacity.Value
        };

        _highlight.Enabled = current.Enabled;
        _scale.Enabled = current.Enabled;
        _opacity.Enabled = current.Enabled;

        Changed?.Invoke(current);
    }

    private static NumericUpDown Number(string caption, int minimum, int maximum, int increment) => new()
    {
        Minimum = minimum, Maximum = maximum, Increment = increment, Width = 90, MinimumSize = new Size(90, 0),
        BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = caption, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, 12, 0)
    };
}
