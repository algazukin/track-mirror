// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Updates;
using TrackMirror.Constants;
using TrackMirror.UI.Theme;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security;
using System.Text.Json;

using TrackMirror.Diagnostics;

namespace TrackMirror.UI.Controls;

internal sealed class AboutSettingsPanel : SettingsSection
{
    private readonly OptionSelector _channel = new(AboutText.UpdateChannel, AboutText.PreRelease)
    {
        Dock = DockStyle.Fill,
        MaximumSize = new Size(300, 0),
        Margin = Padding.Empty
    };

    private readonly Label _channelHint = UiTheme.Label(AboutText.ChannelHint, 9, muted: true, wrap: true);
    private readonly Label _status = UiTheme.Label(AboutText.ManualCheck, 9, muted: true, wrap: true);
    private readonly ActionButton _check = new(AboutText.CheckForUpdates)
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(200, 40),
        Padding = new Padding(14, 0, 14, 0),
        Margin = Padding.Empty
    };
    private readonly CancellationTokenSource _lifetime = new();
    private readonly UpdateChecker _checker;
    private readonly Action<Uri> _open;
    private readonly string _version;

    private bool checking;

    internal event Action<UpdateChannel>? ChannelChanged;

    private UpdateChannel SelectedChannel => _channel.SelectedValue == AboutText.Stable
        ? UpdateChannel.Stable : UpdateChannel.PreRelease;

    internal AboutSettingsPanel(UpdateChannel channel)
        : this(channel, ProjectInfo.Version, new UpdateChecker(), OpenBrowser) { }

    internal AboutSettingsPanel(UpdateChannel channel, string version, UpdateChecker checker, Action<Uri> open)
    {
        _checker = checker;
        _open = open;
        _version = version;

        BackColor = UiTheme.Surface;

        _channel.SetChoices([AboutText.Stable, AboutText.PreRelease]);
        _channel.SelectValue(channel == UpdateChannel.Stable ? AboutText.Stable : AboutText.PreRelease);

        _status.AccessibleName = AboutText.UpdateStatus;
        _check.AccessibleName = AboutText.CheckForUpdates;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 8,
            Margin = Padding.Empty
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 32, 26, 0, 0, 44, 0, 44, 0 })
        {
            layout.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        var links = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 6)
        };

        links.Controls.Add(ProjectLink("GitHub", ProjectInfo.RepositoryUrl));
        links.Controls.Add(ProjectLink("MPL-2.0", ProjectInfo.LicenseUrl));

        var channelRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };

        channelRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        channelRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var channelLabel = UiTheme.Label(AboutText.UpdateChannel, 10);
        channelLabel.AutoSize = true;
        channelLabel.Margin = new Padding(0, 0, 16, 0);

        channelRow.Controls.Add(channelLabel, 0, 0);
        channelRow.Controls.Add(_channel, 1, 0);

        layout.Controls.Add(UiTheme.Label(AboutText.Heading, 15, true), 0, 0);
        layout.Controls.Add(UiTheme.Label(string.Format(CultureInfo.CurrentCulture, AboutText.Version, version), 10), 0, 1);
        layout.Controls.Add(UiTheme.Label(AboutText.Credits, 9, muted: true, wrap: true), 0, 2);
        layout.Controls.Add(links, 0, 3);
        layout.Controls.Add(channelRow, 0, 4);
        layout.Controls.Add(_channelHint, 0, 5);
        layout.Controls.Add(_check, 0, 6);
        layout.Controls.Add(_status, 0, 7);

        Controls.Add(layout);

        _channel.SelectionChanged += () =>
        {
            SetStatus(AboutText.ManualCheck);

            ChannelChanged?.Invoke(SelectedChannel);
        };

        _check.Click += async (_, _) => await CheckForUpdatesAsync();
    }

    internal async Task CheckForUpdatesAsync()
    {
        if (checking || IsDisposed || Disposing)
            return;

        checking = true;
        _check.Enabled = false;
        _channel.Enabled = false;

        SetStatus(AboutText.Checking);

        var token = _lifetime.Token;

        try
        {
            AppDiagnostics.Write(DiagnosticComponent.Updates, DiagnosticEvent.OperationStarted);
            var result = await _checker.CheckAsync(_version, SelectedChannel, token);
            AppDiagnostics.Write(DiagnosticComponent.Updates, DiagnosticEvent.OperationCompleted,
                metrics: new DiagnosticMetrics { Code = (int)result.Status });

            if (token.IsCancellationRequested || IsDisposed || Disposing)
                return;

            switch (result.Status)
            {
                case UpdateCheckStatus.Available:
                    SetStatus(string.Format(CultureInfo.CurrentCulture, AboutText.Available, result.Version));
                    Open(result.ReleaseUrl!);
                    break;
                case UpdateCheckStatus.UpToDate:
                    SetStatus(AboutText.UpToDate);
                    break;
                case UpdateCheckStatus.NoReleases:
                    SetStatus(AboutText.NoReleases);
                    break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (OperationCanceledException error)
        {
            AppDiagnostics.Write(DiagnosticComponent.Updates, DiagnosticEvent.OperationFailed, error);
            if (!IsDisposed)
                SetStatus(AboutText.TimedOut, error: true);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
        {
            AppDiagnostics.Write(DiagnosticComponent.Updates, DiagnosticEvent.OperationFailed, ex);
            if (!IsDisposed)
                SetStatus(AboutText.RateLimited, error: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or IOException
            or ArgumentException or InvalidOperationException)
        {
            AppDiagnostics.Write(DiagnosticComponent.Updates, DiagnosticEvent.OperationFailed, ex);

            if (!IsDisposed)
                SetStatus(AboutText.Failed, error: true);
        }
        finally
        {
            checking = false;

            if (!IsDisposed && !Disposing)
            {
                _check.Enabled = true;
                _channel.Enabled = true;
            }
        }
    }

    private LinkLabel ProjectLink(string caption, string address)
    {
        var link = new LinkLabel
        {
            Text = caption,
            AutoSize = true,
            LinkColor = UiTheme.AccentText,
            ActiveLinkColor = UiTheme.Text,
            VisitedLinkColor = UiTheme.AccentText,
            DisabledLinkColor = UiTheme.Muted,
            BackColor = Color.Transparent,
            LinkBehavior = LinkBehavior.HoverUnderline,
            AccessibleName = caption,
            AccessibleDescription = address,
            Margin = new Padding(0, 0, 20, 0)
        };

        link.LinkClicked += (_, _) => Open(new Uri(address));

        return link;
    }

    private void Open(Uri address)
    {
        try
        {
            _open(address);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException
            or ArgumentException or SecurityException)
        {
            AppDiagnostics.Write(DiagnosticComponent.Updates, DiagnosticEvent.OperationFailed, ex);

            SetStatus(AboutText.BrowserFailed, error: true);
        }
    }

    private void SetStatus(string message, bool error = false)
    {
        _status.Text = message;
        _status.ForeColor = error ? UiTheme.Error : UiTheme.Muted;
    }

    private static void OpenBrowser(Uri address)
    {
        using var process = Process.Start(new ProcessStartInfo(address.AbsoluteUri) { UseShellExecute = true });
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
            _channel.ClosePopup();

        base.OnVisibleChanged(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !IsDisposed)
        {
            _lifetime.Cancel();
            _lifetime.Dispose();

            _checker.Dispose();
        }

        base.Dispose(disposing);
    }
}
