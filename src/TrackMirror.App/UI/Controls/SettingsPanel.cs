// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Theme;
using TrackMirror.Storage.Management;
using TrackMirror.Localization;

namespace TrackMirror.UI.Controls;

public sealed class SettingsPanel : UserControl
{
    private readonly OptionSelector _language = new(LanguageText.Language, LanguageText.English, LanguageText.Scroll)
    {
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 0, 0, 6)
    };
    private readonly Label _languageHint = UiTheme.Label(LanguageText.Choose, 9, muted: true, wrap: true);
    private readonly ThemedCheckBox _startup = new()
    {
        Text = SettingsText.StartWithHubstaff,
        Dock = DockStyle.Fill,
        ForeColor = UiTheme.Text,
        BackColor = UiTheme.Surface,
        Cursor = Cursors.Hand,
        Margin = Padding.Empty
    };
    private readonly ThemedCheckBox _notifications = new()
    {
        Text = SettingsText.NotificationsEnabled,
        Dock = DockStyle.Fill,
        ForeColor = UiTheme.Text,
        BackColor = UiTheme.Surface,
        Cursor = Cursors.Hand,
        Margin = Padding.Empty
    };
    private readonly NumericUpDown _timeout = new()
    {
        Minimum = NotificationDefaults.MinimumSeconds,
        Maximum = NotificationDefaults.MaximumSeconds,
        Value = NotificationDefaults.TimeoutSeconds,
        Width = 100,
        MinimumSize = new Size(100, 0),
        BackColor = UiTheme.Raised,
        ForeColor = UiTheme.Text,
        BorderStyle = BorderStyle.FixedSingle,
        AccessibleName = SettingsText.Timeout,
        Anchor = AnchorStyles.Left,
        Margin = new Padding(0, 0, 12, 0)
    };
    private readonly Label _timeoutLabel = UiTheme.Label(SettingsText.Timeout, 10);
    private readonly Label _secondsLabel = UiTheme.Label(SettingsText.Seconds, 10);
    private readonly Label _saveStatus = UiTheme.Label(SettingsText.Saved, 9, muted: true, wrap: true);
    private readonly HubstaffControlPanel _hubstaff;
    private readonly PrivacySettingsPanel _privacy;
    private readonly StorageSettingsPanel _storage;
    private SurfacePanel? optionsCard;
    private readonly bool _noPreviews;
    private AppSettings current;

    public event Action<AppSettings>? Changed;

    internal event Action? HubstaffControlRetryRequested;

    internal event Action<string>? ModelInstallRequested;

    internal event Action? ModelInstallCancelRequested;

    internal event Action<string>? ModelRemoveRequested;

    internal event Action<DataOperationKind>? DataOperationRequested;

    internal event Action? DataOperationCancelRequested;

    public SettingsPanel(AppSettings settings, bool noPreviews) : this(settings, noPreviews, string.Empty)
    {
    }

    internal SettingsPanel(AppSettings settings, bool noPreviews, string dataRoot)
    {
        SuspendLayout();

        current = settings;

        _noPreviews = noPreviews;

        _hubstaff = new HubstaffControlPanel(settings.HubstaffControl);

        _privacy = new PrivacySettingsPanel(settings);

        _storage = new StorageSettingsPanel(settings, dataRoot);

        Dock = DockStyle.Fill;

        BackColor = UiTheme.Background;

        Padding = new Padding(24);

        Font = new Font(UiText.FontFamily, 10);

        _notifications.Checked = settings.NotificationsEnabled && !noPreviews;

        _startup.Checked = settings.StartWithHubstaff;

        _notifications.Enabled = !noPreviews;

        _timeout.Value = settings.NotificationTimeoutSeconds;

        _language.SetChoices([LanguageText.English, LanguageText.Russian]);

        _language.SelectValue(settings.Language == LocalizationNames.Russian
            ? LanguageText.Russian : LanguageText.English);

        _language.SelectionChanged += () => PublishSettings();

        Controls.Add(new ScrollViewport(CreateLayout(noPreviews)));

        UpdateEnabledState();

        _notifications.CheckedChanged += (_, _) => PublishSettings();

        _timeout.ValueChanged += (_, _) => PublishSettings();

        _startup.CheckedChanged += (_, _) => PublishSettings();

        _hubstaff.Changed += value =>
        {
            current = current with { HubstaffControl = value };

            PublishSettings();
        };

        _hubstaff.RetryRequested += () => HubstaffControlRetryRequested?.Invoke();

        _privacy.Changed += value =>
        {
            current = current with
            {
                PrivacyAutoScan = value.PrivacyAutoScan,
                PrivacyModel = value.PrivacyModel,
                PrivacyGames = value.PrivacyGames,
                PrivacyPersonalChats = value.PrivacyPersonalChats,
                PrivacyConfidentialData = value.PrivacyConfidentialData
            };

            PublishSettings();
        };

        _privacy.InstallRequested += value => ModelInstallRequested?.Invoke(value);

        _privacy.InstallCancelRequested += () => ModelInstallCancelRequested?.Invoke();

        _privacy.RemoveRequested += value => ModelRemoveRequested?.Invoke(value);

        _storage.RetentionChanged += (enabled, days, keepFlagged, keepUnread) =>
        {
            current = current with
            {
                RetentionEnabled = enabled,
                RetentionDays = days,
                RetentionKeepFlagged = keepFlagged,
                RetentionKeepUnread = keepUnread
            };

            PublishSettings();
        };

        _storage.OperationRequested += value => DataOperationRequested?.Invoke(value);

        _storage.CancelRequested += () => DataOperationCancelRequested?.Invoke();

        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                _language.ClosePopup();
            }
        };

        ResumeLayout(true);
    }

    private TableLayoutPanel CreateLayout(bool noPreviews)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 7,
            Margin = Padding.Empty
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var card = new SurfacePanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 0, 12)
        };

        optionsCard = card;

        var options = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 9,
            Margin = Padding.Empty
        };

        options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var height in new[] { 44, 0, 30, 0, 30, 0, 42, 0, 0 })
        {
            options.RowStyles.Add(new RowStyle(height == 0 ? SizeType.AutoSize : SizeType.Absolute, height));
        }

        options.Controls.Add(CreateLanguageRow(), 0, 0);

        options.Controls.Add(_languageHint, 0, 1);

        options.Controls.Add(_startup, 0, 2);

        options.Controls.Add(UiTheme.Label(SettingsText.StartupHint, 9, muted: true, wrap: true), 0, 3);

        options.Controls.Add(_notifications, 0, 4);

        options.Controls.Add(UiTheme.Label(SettingsText.NotificationsHint, 9, muted: true, wrap: true), 0, 5);

        options.Controls.Add(CreateTimeoutRow(), 0, 6);

        options.Controls.Add(UiTheme.Label(SettingsText.TimeoutHint, 9, muted: true, wrap: true), 0, 7);

        options.Controls.Add(_saveStatus, 0, 8);

        card.Controls.Add(options);

        root.Controls.Add(UiTheme.Label(SettingsText.Heading, 22, true), 0, 0);

        root.Controls.Add(UiTheme.Label(SettingsText.Description, 10, muted: true, wrap: true), 0, 1);

        root.Controls.Add(card, 0, 2);

        var hubstaffCard = new SurfacePanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 0, 12)
        };

        hubstaffCard.Controls.Add(_hubstaff);

        root.Controls.Add(hubstaffCard, 0, 3);

        var privacyCard = new SurfacePanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 0, 12)
        };

        privacyCard.Controls.Add(_privacy);

        root.Controls.Add(privacyCard, 0, 4);

        var storageCard = new SurfacePanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(20),
            Margin = new Padding(0, 0, 0, 12)
        };

        storageCard.Controls.Add(_storage);

        root.Controls.Add(storageCard, 0, 5);

        if (noPreviews)
        {
            root.Controls.Add(UiTheme.Label(SettingsText.SessionDisabled, 9, muted: true, wrap: true), 0, 6);
        }

        return root;
    }

    private TableLayoutPanel CreateLanguageRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var label = UiTheme.Label(LanguageText.Language, 10);

        label.AutoSize = true;

        label.Margin = new Padding(0, 0, 16, 0);

        row.Controls.Add(label, 0, 0);

        row.Controls.Add(_language, 1, 0);

        return row;
    }

    private TableLayoutPanel CreateTimeoutRow()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Margin = Padding.Empty
        };

        row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));

        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _timeoutLabel.AutoSize = true;

        _timeoutLabel.Margin = new Padding(0, 0, 16, 0);

        row.Controls.Add(_timeoutLabel, 0, 0);

        row.Controls.Add(_timeout, 1, 0);

        row.Controls.Add(_secondsLabel, 2, 0);

        return row;
    }

    private void UpdateEnabledState()
    {
        _timeout.Enabled = _notifications.Checked;

        _timeoutLabel.ForeColor = _notifications.Checked ? UiTheme.Text : UiTheme.Muted;

        _secondsLabel.ForeColor = _timeoutLabel.ForeColor;

        var selectedLanguage = _language.SelectedValue == LanguageText.Russian
            ? LocalizationNames.Russian : LocalizationNames.English;

        var needsRestart = selectedLanguage != UiLocalization.ActiveLanguage;

        _languageHint.Text = needsRestart ? LanguageText.Restart : LanguageText.Choose;

        _languageHint.ForeColor = needsRestart ? UiTheme.AccentText : UiTheme.Muted;
    }

    private void PublishSettings()
    {
        UpdateEnabledState();

        current = current with
        {
            Language = _language.SelectedValue == LanguageText.Russian
                ? LocalizationNames.Russian : LocalizationNames.English,
            NotificationsEnabled = _noPreviews ? current.NotificationsEnabled : _notifications.Checked,
            StartWithHubstaff = _startup.Checked,
            NotificationTimeoutSeconds = (int)_timeout.Value
        };

        Changed?.Invoke(current);
    }

    public void SetSaveStatus(string? error)
    {
        _saveStatus.Text = error ?? SettingsText.Saved;

        _saveStatus.ForeColor = error == null ? UiTheme.Muted : UiTheme.Error;
    }

    internal void SetDataBusy(bool busy)
    {
        if (optionsCard != null)
        {
            optionsCard.Enabled = !busy;
        }

        _hubstaff.Enabled = !busy;

        _privacy.Enabled = !busy;

        _storage.SetBusy(busy);
    }

    internal void SetHubstaffControlStatus(HubstaffControlStatus value) => _hubstaff.SetStatus(value);

    internal void SetDataProgress(DataOperationProgress value) => _storage.SetProgress(value);

    internal void SetDataStatus(string text, bool error) => _storage.SetStatus(text, error);

    internal void SetModelInstallProgress(PrivacyInstallProgress value) => _privacy.SetInstallProgress(value);
}
