// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ScreenshotCountdownPanel : SettingsSection
{
    private readonly ThemedCheckBox _enabled = Toggle(CountdownText.Enabled);
    private readonly ThemedCheckBox _hide = Toggle(CountdownText.HideBefore);
    private readonly ThemedCheckBox _middle = Toggle(CountdownText.MiddleDrag);
    private readonly OptionSelector _mode = new(CountdownText.Mode) { Width = 310 };
    private readonly NumericUpDown _showSeconds = Number(CountdownText.ShowBefore, 1, 3600, 1);
    private readonly NumericUpDown _hideSeconds = Number(CountdownText.HideSeconds, 1, 3600, 1);
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
            ColumnCount = 1, RowCount = 0, Margin = Padding.Empty
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        Add(UiTheme.Label(CountdownText.Heading, 15, true), 32);
        Add(_enabled, 32);
        Add(UiTheme.Label(CountdownText.Hint, 9, muted: true, wrap: true));
        Add(Row(CountdownText.Mode, _mode));
        Add(Row(CountdownText.ShowBefore, _showSeconds, SettingsText.Seconds));
        Add(_hide, 32);
        Add(Row(CountdownText.HideSeconds, _hideSeconds, SettingsText.Seconds));
        Add(Row(CountdownText.Highlight, _highlight, SettingsText.Seconds));
        Add(Row(CountdownText.Scale, _scale, "%"));
        Add(Row(CountdownText.BackgroundOpacity, _opacity, "%"));
        Add(_middle, 32);
        Add(UiTheme.Label(CountdownText.InteractionHint, 9, muted: true, wrap: true));
        Add(UiTheme.Label(CountdownText.Limitation, 9, muted: true, wrap: true));

        Controls.Add(layout);
        _mode.SetChoices([CountdownText.Default, CountdownText.Detailed, CountdownText.BeforeScreenshot]);
        Apply(options);

        _enabled.CheckedChanged += (_, _) => Publish();
        _hide.CheckedChanged += (_, _) => Publish();
        _middle.CheckedChanged += (_, _) => Publish();
        _mode.SelectionChanged += Publish;

        foreach (var number in new[] { _showSeconds, _hideSeconds, _highlight, _scale, _opacity })
            number.ValueChanged += (_, _) => Publish();

        void Add(Control control, int height = 0)
        {
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
            layout.Controls.Add(control, 0, layout.RowCount++);
        }
    }

    internal void Apply(ScreenshotCountdownOptions value)
    {
        loading = true;
        current = value;

        _enabled.Checked = value.Enabled;
        _mode.SelectValue(value.Mode switch
        {
            ScreenshotCountdownMode.Detailed => CountdownText.Detailed,
            ScreenshotCountdownMode.BeforeScreenshot => CountdownText.BeforeScreenshot,
            _ => CountdownText.Default
        }, focus: false);
        _showSeconds.Value = value.ShowBeforeSeconds;
        _hide.Checked = value.HideBeforeScreenshot;
        _hideSeconds.Maximum = value.Mode == ScreenshotCountdownMode.BeforeScreenshot ? Math.Max(1, _showSeconds.Value - 1) : 3600;
        _hideSeconds.Value = Math.Min(value.HideBeforeSeconds, _hideSeconds.Maximum);
        _highlight.Value = value.HighlightSeconds;
        _scale.Value = value.ScalePercent;
        _opacity.Value = value.BackgroundOpacityPercent;
        _middle.Checked = value.MiddleButtonDrag;

        UpdateEnabled();
        loading = false;
    }

    private void Publish()
    {
        if (loading)
            return;

        var mode = _mode.SelectedValue == CountdownText.BeforeScreenshot ? ScreenshotCountdownMode.BeforeScreenshot
            : _mode.SelectedValue == CountdownText.Detailed ? ScreenshotCountdownMode.Detailed : ScreenshotCountdownMode.Default;

        loading = true;

        if (mode == ScreenshotCountdownMode.BeforeScreenshot && _showSeconds.Value == 1)
            _hide.Checked = false;

        _hideSeconds.Maximum = mode == ScreenshotCountdownMode.BeforeScreenshot ? Math.Max(1, _showSeconds.Value - 1) : 3600;
        loading = false;

        current = current with
        {
            Enabled = _enabled.Checked, Mode = mode, ShowBeforeSeconds = (int)_showSeconds.Value,
            HideBeforeScreenshot = _hide.Checked, HideBeforeSeconds = (int)_hideSeconds.Value,
            HighlightSeconds = (int)_highlight.Value, ScalePercent = (int)_scale.Value,
            BackgroundOpacityPercent = (int)_opacity.Value, MiddleButtonDrag = _middle.Checked
        };

        UpdateEnabled();
        Changed?.Invoke(current);
    }

    private void UpdateEnabled()
    {
        _mode.Enabled = current.Enabled;
        var floating = current.Enabled && current.Mode != ScreenshotCountdownMode.Default;

        _showSeconds.Enabled = current.Enabled && current.Mode == ScreenshotCountdownMode.BeforeScreenshot;
        _hide.Enabled = floating && (current.Mode != ScreenshotCountdownMode.BeforeScreenshot || current.ShowBeforeSeconds > 1);
        _hideSeconds.Enabled = floating && current.HideBeforeScreenshot;
        _highlight.Enabled = floating;
        _scale.Enabled = floating;
        _opacity.Enabled = floating;
        _middle.Enabled = floating;
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (!Visible)
            _mode.ClosePopup();
    }

    private static FlowLayoutPanel Row(string caption, Control control, string? unit = null)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, WrapContents = false, Margin = new Padding(0, 4, 0, 4)
        };
        var label = UiTheme.Label(caption, 10);
        label.Dock = DockStyle.None;
        label.AutoSize = true;
        label.Anchor = AnchorStyles.Left;
        label.Margin = new Padding(0, 0, 16, 0);
        control.Anchor = AnchorStyles.Left;
        row.Controls.AddRange([label, control]);

        if (unit != null)
        {
            var units = UiTheme.Label(unit, 10);
            units.AutoSize = true;
            units.Anchor = AnchorStyles.Left;
            row.Controls.Add(units);
        }

        return row;
    }

    private static ThemedCheckBox Toggle(string caption) => new()
    {
        Text = caption, AccessibleName = caption, Dock = DockStyle.Fill,
        ForeColor = UiTheme.Text, BackColor = UiTheme.Surface, Cursor = Cursors.Hand, Margin = Padding.Empty
    };

    private static NumericUpDown Number(string caption, int minimum, int maximum, int increment) => new()
    {
        Minimum = minimum, Maximum = maximum, Increment = increment, Width = 90, MinimumSize = new Size(90, 0),
        BackColor = UiTheme.Raised, ForeColor = UiTheme.Text, BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = caption, Anchor = AnchorStyles.Left, Margin = new Padding(0, 4, 12, 4)
    };
}
