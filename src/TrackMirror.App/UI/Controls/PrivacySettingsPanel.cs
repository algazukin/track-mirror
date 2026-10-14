// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Providers;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class PrivacySettingsPanel : SettingsSection
{
    private readonly ThemedCheckBox _automatic = new();
    private readonly ThemedCheckBox _games = new();
    private readonly ThemedCheckBox _chats = new();
    private readonly ThemedCheckBox _confidential = new();
    private readonly Label _readiness = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly PrivacyModelPanel _models;
    private AppSettings current;

    internal event Action<AppSettings>? Changed;

    internal event Action<string>? InstallRequested;

    internal event Action? InstallCancelRequested;

    internal event Action<string>? RemoveRequested;

    internal PrivacySettingsPanel(AppSettings settings)
    {
        current = settings;

        _models = new PrivacyModelPanel(settings.PrivacyModel);

        BackColor = UiTheme.Surface;

        _automatic.Text = PrivacyText.AutoScan;

        _games.Text = PrivacyText.SearchGames;

        _chats.Text = PrivacyText.SearchPersonalChats;

        _confidential.Text = PrivacyText.SearchConfidentialData;

        _automatic.Checked = settings.PrivacyAutoScan;

        _games.Checked = settings.PrivacyGames;

        _chats.Checked = settings.PrivacyPersonalChats;

        _confidential.Checked = settings.PrivacyConfidentialData;

        foreach (var option in new[] { _automatic, _games, _chats, _confidential })
        {
            option.Dock = DockStyle.Fill;

            option.ForeColor = UiTheme.Text;

            option.Margin = Padding.Empty;

            option.Cursor = Cursors.Hand;

            option.AccessibleName = option.Text;
        }

        var rows = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            RowCount = 9, ColumnCount = 1, Margin = Padding.Empty
        };

        rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 34, 0, 0, 30, 30, 30, 30, 0, 0 })
        {
            rows.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        rows.Controls.Add(UiTheme.Label(PrivacyText.Heading, 13, true), 0, 0);

        rows.Controls.Add(UiTheme.Label(PrivacyText.Hint, 9, muted: true, wrap: true), 0, 1);

        rows.Controls.Add(_models, 0, 2);

        rows.Controls.Add(_automatic, 0, 3);

        rows.Controls.Add(_games, 0, 4);

        rows.Controls.Add(_chats, 0, 5);

        rows.Controls.Add(_confidential, 0, 6);

        rows.Controls.Add(_readiness, 0, 7);

        rows.Controls.Add(UiTheme.Label(PrivacyText.Limitations, 9, muted: true, wrap: true), 0, 8);

        Controls.Add(rows);

        RefreshReadiness();

        _models.ModelChanged += value =>
        {
            current = current with { PrivacyModel = value };

            PublishSettings();
        };

        _models.InstallRequested += value => InstallRequested?.Invoke(value);

        _models.CancelRequested += () => InstallCancelRequested?.Invoke();

        _models.RemoveRequested += value => RemoveRequested?.Invoke(value);

        foreach (var option in new[] { _automatic, _games, _chats, _confidential })
        {
            option.CheckedChanged += (_, _) => PublishSettings();
        }
    }

    internal void RefreshReadiness()
    {
        var installed = LocalModelRuntime.IsInstalled(current.PrivacyModel);

        _automatic.Enabled = current.HasPrivacyCategories && installed;

        _readiness.Text = !current.HasPrivacyCategories ? PrivacyText.NoCategories
            : installed ? PrivacyText.Readiness : PrivacyText.MissingRuntime;

        _readiness.ForeColor = current.HasPrivacyCategories && installed ? UiTheme.Muted : UiTheme.Amber;
    }

    internal void SetInstallProgress(PrivacyInstallProgress value)
    {
        _models.SetProgress(value);

        RefreshReadiness();
    }

    private void PublishSettings()
    {
        current = current with
        {
            PrivacyAutoScan = _automatic.Checked,
            PrivacyGames = _games.Checked,
            PrivacyPersonalChats = _chats.Checked,
            PrivacyConfidentialData = _confidential.Checked
        };

        RefreshReadiness();

        Changed?.Invoke(current);
    }
}
