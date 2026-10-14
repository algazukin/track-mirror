// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Privacy.Installation;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Providers;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class PrivacyModelPanel : SettingsSection
{
    private readonly OptionSelector _selector = new(PrivacyInstallText.Model, PrivacyInstallText.NoModel,
        PrivacyInstallText.ModelScroll) { Dock = DockStyle.Fill, Height = 38, Margin = Padding.Empty };
    private readonly ActionButton _install = new(PrivacyInstallText.Install) { Width = 214, Height = 34 };
    private readonly ActionButton _cancel = new(PrivacyInstallText.Cancel) { Width = 184, Height = 34 };
    private readonly ActionButton _remove = new(PrivacyInstallText.Remove) { Width = 184, Height = 34 };
    private readonly Label _status = UiTheme.Label(string.Empty, 9, muted: true, wrap: true);
    private readonly InstallationProgressBar _progressBar = new();
    private readonly TableLayoutPanel _layout = new()
    {
        Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        ColumnCount = 1, RowCount = 6, Margin = Padding.Empty
    };
    private PrivacyInstallProgress progress = new(PrivacyInstallStage.Idle);

    internal event Action<string>? ModelChanged;

    internal event Action<string>? InstallRequested;

    internal event Action? CancelRequested;

    internal event Action<string>? RemoveRequested;

    internal string SelectedModel => PrivacyModelCatalog.All
        .First(model => model.Caption == _selector.SelectedValue).Tag;

    internal PrivacyModelPanel(string selected)
    {
        BackColor = UiTheme.Surface;

        _selector.SetChoices(PrivacyModelCatalog.All.Select(model => model.Caption));

        _selector.SelectValue(PrivacyModelCatalog.Get(selected).Caption);

        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 24, 38, 0, 0, 0, 0 })
        {
            _layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Margin = Padding.Empty, Padding = new Padding(0, 8, 0, 0), WrapContents = true
        };

        foreach (var button in new[] { _install, _remove, _cancel })
        {
            button.Margin = new Padding(0, 0, 10, 8);

            buttons.Controls.Add(button);
        }

        _status.Margin = new Padding(0, 4, 0, 6);

        _status.TextAlign = ContentAlignment.TopLeft;

        var hint = UiTheme.Label(PrivacyInstallText.DownloadHint, 9, muted: true, wrap: true);

        hint.Margin = new Padding(0, 6, 0, 0);

        hint.TextAlign = ContentAlignment.TopLeft;

        _layout.Controls.Add(UiTheme.Label(PrivacyInstallText.Model, 9, muted: true), 0, 0);

        _layout.Controls.Add(_selector, 0, 1);

        _layout.Controls.Add(buttons, 0, 2);

        _layout.Controls.Add(_progressBar, 0, 3);

        _layout.Controls.Add(_status, 0, 4);

        _layout.Controls.Add(hint, 0, 5);

        Controls.Add(_layout);

        _selector.SelectionChanged += () =>
        {
            progress = new PrivacyInstallProgress(PrivacyInstallStage.Idle);

            RefreshStatus();

            ModelChanged?.Invoke(SelectedModel);
        };

        _install.Click += (_, _) => InstallRequested?.Invoke(SelectedModel);

        _cancel.Click += (_, _) => CancelRequested?.Invoke();

        _remove.Click += (_, _) => RemoveRequested?.Invoke(SelectedModel);

        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                _selector.ClosePopup();
            }
        };

        RefreshStatus();
    }

    internal void SetProgress(PrivacyInstallProgress value)
    {
        if (!value.Busy && value.Model.Length > 0 && value.Model != SelectedModel)
            return;

        progress = value;

        RefreshStatus();
    }

    private void RefreshStatus()
    {
        var model = PrivacyModelCatalog.Get(SelectedModel);

        var installed = LocalModelRuntime.IsInstalled(SelectedModel);

        var removable = PrivacyModelCatalog.Removable(LocalModelRuntime.Root, model);

        var removing = File.Exists(PrivacyModelCatalog.RemovalPath(LocalModelRuntime.Root, model));

        _selector.Enabled = !progress.Busy;

        _install.Enabled = !progress.Busy && !installed && !removing;

        _install.Visible = !installed && !removing && progress.Stage != PrivacyInstallStage.ModelRemove;

        _remove.Enabled = !progress.Busy && removable;

        _remove.Visible = progress.Stage == PrivacyInstallStage.ModelRemove || removable && !progress.Busy;

        _cancel.Visible = progress.Busy;

        _cancel.Text = progress.Stage == PrivacyInstallStage.ModelRemove
            ? PrivacyInstallText.CancelOperation : PrivacyInstallText.Cancel;

        _progressBar.Visible = progress.Busy;

        _layout.RowStyles[3].SizeType = SizeType.Absolute;

        _layout.RowStyles[3].Height = progress.Busy ? 22 * DeviceDpi / 96f : 0;

        _progressBar.SetProgress(progress);

        _status.Text = progress.Stage == PrivacyInstallStage.Idle
            ? removing ? PrivacyInstallText.RemovalIncomplete
                : installed ? PrivacyInstallText.Installed : PrivacyInstallText.Missing
            : PrivacyInstallText.Describe(progress);

        _status.ForeColor = progress.Stage == PrivacyInstallStage.Failed ? UiTheme.Error
            : installed ? UiTheme.AccentText : UiTheme.Muted;
    }
}
