// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ScanOptionsPanel : SettingsSection
{
    private readonly Label _heading = UiTheme.Label(string.Empty, 13, true);
    private readonly ThemedCheckBox _games = Option(PrivacyText.Game);
    private readonly ThemedCheckBox _chats = Option(PrivacyText.PersonalChat);
    private readonly ThemedCheckBox _confidential = Option(PrivacyText.ConfidentialData);
    private readonly ThemedCheckBox _recheck = Option(GalleryText.Recheck);
    private readonly Label _status = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly ActionButton _start = new(GalleryText.StartScan, true)
    {
        AutoSize = true, MinimumSize = new Size(132, 34), Padding = new Padding(12, 0, 12, 0)
    };
    private readonly ActionButton _settings = new(GalleryText.OpenSettings)
    {
        AutoSize = true, MinimumSize = new Size(136, 34), Padding = new Padding(12, 0, 12, 0)
    };
    private readonly ActionButton _cancel = new(GalleryText.Clear)
    {
        AutoSize = true, MinimumSize = new Size(100, 34), Padding = new Padding(12, 0, 12, 0)
    };
    private bool ready;

    internal event Action<IReadOnlyList<PrivacyScanCategory>, bool>? ScanRequested;

    internal event Action? SettingsRequested;

    internal event Action? CancelRequested;

    internal ScanOptionsPanel()
    {
        AutoScaleMode = AutoScaleMode.Dpi;

        AutoScaleDimensions = new SizeF(96, 96);

        BackColor = UiTheme.Surface;

        Font = new Font(UiText.FontFamily, 10);

        Padding = new Padding(18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 9, Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 34, 0, 30, 30, 30, 36, 0, 0, 0 })
        {
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        layout.Controls.Add(_heading, 0, 0);

        layout.Controls.Add(UiTheme.Label(GalleryText.ScanHint, 9, muted: true, wrap: true), 0, 1);

        layout.Controls.Add(_games, 0, 2);

        layout.Controls.Add(_chats, 0, 3);

        layout.Controls.Add(_confidential, 0, 4);

        layout.Controls.Add(_recheck, 0, 5);

        layout.Controls.Add(UiTheme.Label(GalleryText.MissingOnly, 9, muted: true, wrap: true), 0, 6);

        layout.Controls.Add(_status, 0, 7);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true, Margin = new Padding(0, 8, 0, 0)
        };

        actions.Controls.AddRange([_start, _settings, _cancel]);

        layout.Controls.Add(actions, 0, 8);

        Controls.Add(layout);

        foreach (var option in new[] { _games, _chats, _confidential })
        {
            option.CheckedChanged += (_, _) => UpdateReady();
        }

        _start.Click += (_, _) => ScanRequested?.Invoke(SelectedCategories(), _recheck.Checked);

        _settings.Click += (_, _) => SettingsRequested?.Invoke();

        _cancel.Click += (_, _) => CancelRequested?.Invoke();
    }

    internal void Prepare(int count, AppSettings settings, bool modelReady)
    {
        ready = modelReady;

        _heading.Text = string.Format(GalleryText.ScanTitleFormat, count);

        _games.Checked = settings.PrivacyGames;

        _chats.Checked = settings.PrivacyPersonalChats;

        _confidential.Checked = settings.PrivacyConfidentialData;

        _recheck.Checked = false;

        UpdateReady();
    }

    internal List<PrivacyScanCategory> SelectedCategories()
    {
        var categories = new List<PrivacyScanCategory>();

        if (_games.Checked)
        {
            categories.Add(PrivacyScanCategory.Game);
        }

        if (_chats.Checked)
        {
            categories.Add(PrivacyScanCategory.PersonalChat);
        }

        if (_confidential.Checked)
        {
            categories.Add(PrivacyScanCategory.ConfidentialData);
        }

        return categories;
    }

    private void UpdateReady()
    {
        var selected = _games.Checked || _chats.Checked || _confidential.Checked;

        _start.Visible = ready;

        _start.Enabled = ready && selected;

        _settings.Visible = !ready;

        _status.Visible = !ready || !selected;

        _status.Text = ready ? GalleryText.NoCategories : PrivacyText.MissingRuntime;

        _status.ForeColor = UiTheme.Amber;
    }

    private static ThemedCheckBox Option(string text) => new()
    {
        Text = text, AccessibleName = text, Dock = DockStyle.Fill, ForeColor = UiTheme.Text,
        BackColor = UiTheme.Surface, Margin = Padding.Empty, Cursor = Cursors.Hand
    };
}
