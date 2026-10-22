// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Models;
using TrackMirror.Storage;
using TrackMirror.Storage.Management;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Forms;

public sealed class HistoryForm : Form
{
    private readonly ScreenshotStore _store;
    private readonly Panel _historyPage = new() { Dock = DockStyle.Fill };
    private readonly SettingsPanel _settingsPage;
    private readonly ActivityPanel _activityPage = new();
    private readonly Button _activityTab = CreateTabButton(ActivityText.Tab);
    private readonly Button _historyTab = CreateTabButton(SettingsText.HistoryTab);
    private readonly Button _settingsTab = CreateTabButton(SettingsText.SettingsTab);
    private readonly CaptureListBox _list = new();
    private readonly CaptureScrollBar _scrollBar;
    private readonly CaptureGallery _gallery;
    private readonly SurfacePanel _galleryPage = new()
    {
        Dock = DockStyle.Fill,
        Padding = new Padding(14),
        Margin = Padding.Empty
    };
    private readonly Panel _detailPage = new() { Dock = DockStyle.Fill, Visible = false };
    private readonly ActionButton _back = new(UiText.BackToGallery)
    {
        Width = 140,
        Visible = false
    };
    private readonly PictureBox _picture = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = UiTheme.Viewer,
        Margin = Padding.Empty
    };
    private readonly Label _details = UiTheme.Label(UiText.SelectCapture, 14, true);
    private readonly Label _fileName = UiTheme.Label(string.Empty, 9, muted: true);
    private readonly Label _status = UiTheme.Label(UiText.WaitingForCaptures, 9, muted: true);
    private readonly Label _warning = UiTheme.Label(string.Empty, 9);
    private readonly Label _counts = UiTheme.Label(string.Empty, 10, true);
    private readonly Label _activity = UiTheme.Label(UiText.ActivityWaiting, 10, true);
    private readonly Label _activityDetails = UiTheme.Label(UiText.ActivityWaitingDetails, 8.5f, muted: true);
    private readonly Dictionary<string, HubstaffActivity> _activities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Label _empty = UiTheme.Label(UiText.SelectedCapturePlaceholder, 12, muted: true);
    private readonly CaptureFilterSelector _captureFilter = new();
    private readonly DateFilterControl _dateFilter = new();
    private readonly GalleryActionBar _galleryActions = new();
    private readonly ActionButton _privacyButton = new(PrivacyText.ResultsAction) { Width = 136 };
    private PrivacyResultForm? privacyWindow;
    private Dictionary<string, PrivacyScanRecord> privacyResults = new(StringComparer.OrdinalIgnoreCase);
    private AppSettings privacySettings = new();
    private readonly ActionButton _reviewed = new(UiText.MarkReviewed, true)
    {
        Enabled = false,
        Width = 120
    };
    private readonly ActionButton _flag = new(UiText.Flag)
    {
        Enabled = false,
        Width = 120
    };
    private readonly ActionButton _zoom = new(UiText.OpenFullSize)
    {
        Enabled = false,
        Width = 142
    };
    private readonly ToolTip _tooltips = new()
    {
        InitialDelay = 400,
        ReshowDelay = 150,
        AutoPopDelay = 10000
    };
    private CaptureEntry? selected;
    private bool refreshing;
    private string warningMessage = string.Empty;
    private bool showingGallery = true;
    private bool resetGalleryScroll;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool AllowClose
    {
        get; set;
    }

    public event Action<string>? ReviewRequested;
    public event Action<string>? FlagRequested;
    public event Action<AppSettings>? SettingsChanged;

    internal event Action? HubstaffControlRetryRequested;

    internal event Action<DataOperationKind>? DataOperationRequested;

    internal event Action? DataOperationCancelRequested;

    internal event Action<string>? ModelInstallRequested;

    internal event Action? ModelInstallCancelRequested;

    internal event Action<string>? ModelRemoveRequested;
    internal event Action<IReadOnlyList<string>>? ReviewSelectionRequested;
    internal event Action? UndoReviewRequested;
    internal event Action<IReadOnlyList<CaptureEntry>, bool, IReadOnlyList<PrivacyScanCategory>?>? PrivacyScanRequested;
    internal event Action? PrivacyCancelRequested;
    internal event Action<string, PrivacyDisposition>? PrivacyDispositionRequested;

    public HistoryForm(ScreenshotStore store, AppSettings? settings = null, bool noPreviews = false)
    {
        SuspendLayout();

        _store = store;

        privacySettings = settings ?? new AppSettings();

        _galleryActions.SetSettings(privacySettings);

        _scrollBar = new CaptureScrollBar(_list);

        _gallery = new CaptureGallery(store);

        _settingsPage = new SettingsPanel(settings ?? new AppSettings(), noPreviews, store.DataRoot);

        ConfigureWindow();

        Controls.Add(CreatePages());

        _settingsPage.Changed += value => SettingsChanged?.Invoke(value);

        _settingsPage.HubstaffControlRetryRequested += () => HubstaffControlRetryRequested?.Invoke();

        _settingsPage.ModelInstallRequested += value => ModelInstallRequested?.Invoke(value);

        _settingsPage.ModelInstallCancelRequested += () => ModelInstallCancelRequested?.Invoke();

        _settingsPage.ModelRemoveRequested += value => ModelRemoveRequested?.Invoke(value);

        _settingsPage.DataOperationRequested += value => DataOperationRequested?.Invoke(value);

        _settingsPage.DataOperationCancelRequested += () => DataOperationCancelRequested?.Invoke();

        ConnectEvents();

        ResumeLayout(true);

        RefreshEntries();
    }

    private static Button CreateTabButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Size = new Size(140, 36),
            Margin = new Padding(0, 0, 8, 0),
            FlatStyle = FlatStyle.Flat,
            BackColor = UiTheme.Background,
            ForeColor = UiTheme.Muted,
            Cursor = Cursors.Hand,
            AccessibleRole = AccessibleRole.PageTab,
            UseVisualStyleBackColor = false
        };

        button.FlatAppearance.BorderSize = 0;

        button.FlatAppearance.MouseOverBackColor = UiTheme.Raised;

        return button;
    }

    private TableLayoutPanel CreatePages()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            Margin = Padding.Empty
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var navigation = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 12, 0, 0),
            WrapContents = false,
            Margin = Padding.Empty,
            AccessibleRole = AccessibleRole.PageTabList
        };

        navigation.Controls.AddRange([_historyTab, _activityTab, _settingsTab]);

        var pages = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };

        _historyPage.Controls.Add(CreateLayout());

        pages.Controls.AddRange([_historyPage, _activityPage, _settingsPage]);

        root.Controls.Add(navigation, 0, 0);

        root.Controls.Add(pages, 0, 1);

        _historyTab.Click += (_, _) =>
        {
            SelectPage(ViewerPage.History);

            ShowGallery();
        };

        _settingsTab.Click += (_, _) => SelectPage(ViewerPage.Settings);

        _activityTab.Click += (_, _) => SelectPage(ViewerPage.Activity);

        SelectPage(ViewerPage.History);

        return root;
    }

    private void SelectPage(ViewerPage page)
    {
        _dateFilter.CloseCalendar();

        _activityPage.CloseCalendar();

        foreach (var (control, tab, value) in new (Control, Button, ViewerPage)[]
        {
            (_historyPage, _historyTab, ViewerPage.History),
            (_activityPage, _activityTab, ViewerPage.Activity),
            (_settingsPage, _settingsTab, ViewerPage.Settings)
        })
        {
            var selectedPage = page == value;

            control.Visible = selectedPage;

            tab.BackColor = selectedPage ? UiTheme.Selected : UiTheme.Background;

            tab.ForeColor = selectedPage ? UiTheme.AccentText : UiTheme.Muted;

        }
    }

    public void ShowSettings() => SelectPage(ViewerPage.Settings);

    public void ShowActivity() => SelectPage(ViewerPage.Activity);

    public void AddHubstaffHistory(IReadOnlyList<HubstaffActivity> entries) => _activityPage.AddEntries(entries);

    internal void SetHubstaffControlStatus(HubstaffControlStatus value) =>
        _settingsPage.SetHubstaffControlStatus(value);

    public void SetSettingsStatus(string? error) => _settingsPage.SetSaveStatus(error);

    internal void SetScreenshotCountdown(ScreenshotCountdownOptions value) => _settingsPage.SetScreenshotCountdown(value);

    internal void SetModelInstallProgress(PrivacyInstallProgress value) => _settingsPage.SetModelInstallProgress(value);

    internal void SetDataBusy(bool busy, bool showSettings = true)
    {
        _historyPage.Enabled = !busy;

        _activityPage.Enabled = !busy;

        _historyTab.Enabled = !busy;

        _activityTab.Enabled = !busy;

        _settingsPage.SetDataBusy(busy);

        if (busy)
        {
            if (showSettings)
            {
                ShowSettings();
            }

            foreach (var window in OwnedForms)
            {
                window.Close();
            }
        }
    }

    internal void SetDataProgress(DataOperationProgress value) => _settingsPage.SetDataProgress(value);

    internal void SetDataStatus(string text, bool error = false) => _settingsPage.SetDataStatus(text, error);

    private void ConfigureWindow()
    {
        Text = UiText.HistoryWindowTitle;

        Icon = ApplicationIcon.Main;

        Font = new Font(UiText.FontFamily, 9.5f);

        AutoScaleMode = AutoScaleMode.Dpi;

        AutoScaleDimensions = new SizeF(96, 96);

        ClientSize = new Size(1160, 720);

        MinimumSize = new Size(960, 650);

        StartPosition = FormStartPosition.CenterScreen;

        BackColor = UiTheme.Background;

        ForeColor = UiTheme.Text;

        DoubleBuffered = true;
    }

    private TableLayoutPanel CreateHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };

        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));

        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));

        heading.Controls.Add(UiTheme.Label(UiText.HistoryHeading, 22, true), 0, 0);

        heading.Controls.Add(UiTheme.Label(UiText.GalleryDescription, 10, muted: true), 0, 1);

        var headerActions = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };

        headerActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        headerActions.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));

        _counts.TextAlign = ContentAlignment.MiddleRight;

        _counts.ForeColor = UiTheme.AccentText;

        var folder = new ActionButton(UiText.CopiesFolder)
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Size = new Size(140, 34),
            Margin = new Padding(0, 6, 0, 12)
        };

        headerActions.Controls.Add(_counts, 0, 0);

        headerActions.Controls.Add(folder, 0, 1);

        header.Controls.Add(heading, 0, 0);

        header.Controls.Add(headerActions, 1, 0);

        folder.Click += (_, _) => System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(_store.CapturesRoot) { UseShellExecute = true }
        );

        return header;
    }

    private SurfacePanel CreateSidebar()
    {
        var sidebar = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 18, 0)
        };

        var sidebarLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };

        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));

        sidebarLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        sidebarLayout.Controls.Add(UiTheme.Label(UiText.CapturesHeading, 12, true), 0, 0);

        var captures = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Surface,
            Margin = Padding.Empty
        };

        captures.Controls.AddRange([_list, _scrollBar]);

        sidebarLayout.Controls.Add(captures, 0, 1);

        sidebar.Controls.Add(sidebarLayout);

        return sidebar;
    }

    private SurfacePanel CreateViewer()
    {
        var viewer = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            Margin = Padding.Empty
        };

        var viewerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Margin = Padding.Empty
        };

        viewerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));

        viewerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        viewerLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var imageHeading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };

        imageHeading.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));

        imageHeading.RowStyles.Add(new RowStyle(SizeType.Absolute, 23));

        imageHeading.Controls.Add(_details, 0, 0);

        imageHeading.Controls.Add(_fileName, 0, 1);

        var stage = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UiTheme.Viewer,
            Margin = Padding.Empty
        };

        _empty.TextAlign = ContentAlignment.MiddleCenter;

        _empty.BackColor = UiTheme.Viewer;

        stage.Controls.Add(_picture);

        stage.Controls.Add(_empty);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 16, 0, 0),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Margin = Padding.Empty
        };

        actions.Controls.AddRange([_reviewed, _flag, _zoom, _privacyButton]);

        viewerLayout.Controls.Add(imageHeading, 0, 0);

        viewerLayout.Controls.Add(stage, 0, 1);

        viewerLayout.Controls.Add(actions, 0, 2);

        viewer.Controls.Add(viewerLayout);

        return viewer;
    }

    private TableLayoutPanel CreateFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, RowCount = 4, Padding = new Padding(2, 8, 0, 0), Margin = Padding.Empty
        };

        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        foreach (var label in new[] { _activity, _activityDetails, _status, _warning })
        {
            label.AutoSize = true;

            label.AutoEllipsis = false;

            label.TextAlign = ContentAlignment.TopLeft;

            footer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            footer.Controls.Add(label, 0, footer.Controls.Count);
        }

        _warning.ForeColor = UiTheme.Error;

        _warning.Visible = _warning.Text.Length > 0;

        _activity.ForeColor = UiTheme.AccentText;

        return footer;
    }

    private TableLayoutPanel CreateLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(24),
            BackColor = UiTheme.Background
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };

        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260));

        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        body.Controls.Add(CreateSidebar(), 0, 0);

        body.Controls.Add(CreateViewer(), 1, 0);

        _detailPage.Controls.Add(body);

        _galleryPage.Controls.Add(_gallery);

        var content = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty };

        content.Controls.AddRange([_detailPage, _galleryPage]);

        root.Controls.Add(CreateHeader(), 0, 0);

        root.Controls.Add(CreateFilters(), 0, 1);

        root.Controls.Add(_galleryActions, 0, 2);

        root.Controls.Add(content, 0, 3);

        root.Controls.Add(CreateFooter(), 0, 4);

        return root;
    }

    private FlowLayoutPanel CreateFilters()
    {
        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 16),
            Margin = Padding.Empty
        };

        _captureFilter.Margin = new Padding(0, 0, 8, 8);

        _back.Height = 38;

        _back.Width = 132;

        _back.Margin = new Padding(0, 0, 12, 8);

        _dateFilter.Dock = DockStyle.None;

        _dateFilter.Size = new Size(310, 38);

        _dateFilter.Margin = new Padding(4, 0, 0, 8);

        _dateFilter.BackColor = UiTheme.Background;

        filters.Controls.AddRange([_back, _captureFilter, _dateFilter]);

        return filters;
    }

    private void UpdateGallerySelection()
    {
        var selection = _gallery.SelectedEntries;

        _galleryActions.UpdateSelection(_gallery.Entries.Count, selection.Count, selection.Any(entry => !entry.Viewed));
    }

    internal void ShowReviewUndo(int count)
    {
        _gallery.SelectAll(false);

        _galleryActions.ShowUndo(count);
    }

    internal void ClearReviewUndo() => _galleryActions.ClearUndo();

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (_historyPage.Visible && showingGallery)
        {
            if (keyData == (Keys.Control | Keys.A))
            {
                _gallery.SelectAll(true);

                _gallery.Focus();

                return true;
            }

            if (keyData == Keys.Escape && _gallery.SelectedEntries.Count > 0)
            {
                _gallery.SelectAll(false);

                return true;
            }
        }

        return base.ProcessCmdKey(ref message, keyData);
    }

    private void ShowGallery(bool resetScroll = false)
    {
        showingGallery = true;

        _galleryActions.Visible = true;

        resetGalleryScroll |= resetScroll;

        _galleryPage.Visible = true;

        _detailPage.Visible = false;

        _back.Visible = false;

        selected = null;

        UpdatePrivacySelection();

        var previous = _picture.Image;

        _picture.Image = null;

        previous?.Dispose();

        refreshing = true;

        _list.ClearSelected();

        refreshing = false;

        UpdateActivity();
    }

    private void OpenCapture(string id)
    {
        showingGallery = false;

        _gallery.SelectAll(false);

        _galleryActions.Visible = false;

        _galleryPage.Visible = false;

        _detailPage.Visible = true;

        _back.Visible = true;

        RefreshEntries(id);
    }

    private void ConnectEvents()
    {
        _gallery.SelectionChanged += UpdateGallerySelection;

        _galleryActions.SelectAllRequested += value => _gallery.SelectAll(value);

        _galleryActions.ClearRequested += () => _gallery.SelectAll(false);

        _galleryActions.ReadRequested += () =>
            ReviewSelectionRequested?.Invoke(_gallery.SelectedEntries.Select(entry => entry.Id).ToList());

        _galleryActions.UndoRequested += () => UndoReviewRequested?.Invoke();

        _galleryActions.CancelScanRequested += () => PrivacyCancelRequested?.Invoke();

        _galleryActions.SettingsRequested += ShowSettings;

        _galleryActions.ScanRequested += (categories, force) =>
        {
            PrivacyScanRequested?.Invoke(_gallery.SelectedEntries, force, categories);

            _gallery.SelectAll(false);
        };

        _privacyButton.Click += (_, _) => OpenPrivacyResult();

        _captureFilter.FilterChanged += () =>
        {
            _gallery.SelectAll(false);

            ShowGallery(true);

            RefreshEntries();
        };

        _picture.Paint += DrawPrivacyRegions;

        _dateFilter.RangeChanged += () =>
        {
            _gallery.SelectAll(false);

            ShowGallery(true);

            RefreshEntries();
        };

        _gallery.EntryOpened += OpenCapture;

        _back.Click += (_, _) => ShowGallery();

        VisibleChanged += (_, _) =>
        {
            if (!Visible)
            {
                _dateFilter.CloseCalendar();

                _activityPage.CloseCalendar();
            }
        };

        Resize += (_, _) =>
        {
            if (WindowState == FormWindowState.Minimized)
            {
                _dateFilter.CloseCalendar();

                _activityPage.CloseCalendar();
            }
        };

        _list.SelectedIndexChanged += (_, _) =>
        {
            if (!refreshing && !showingGallery)
            {
                LoadSelection();
            }
        };

        _list.DoubleClick += (_, _) =>
        {
            if (selected != null)
            {
                OpenZoom();
            }
        };

        _reviewed.Click += (_, _) =>
        {
            if (selected != null)
            {
                ReviewRequested?.Invoke(selected.Id);
            }
        };

        _flag.Click += (_, _) =>
        {
            if (selected != null)
            {
                FlagRequested?.Invoke(selected.Id);
            }
        };

        _zoom.Click += (_, _) => OpenZoom();

        Load += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;

            var available = new Size(Math.Max(1, area.Width - 48), Math.Max(1, area.Height - 48));

            MinimumSize = new Size(
                Math.Min(MinimumSize.Width, available.Width),
                Math.Min(MinimumSize.Height, available.Height)
            );

            Size = new Size(Math.Min(Width, available.Width), Math.Min(Height, available.Height));

            CenterToScreen();
        };

        FormClosing += (_, e) =>
        {
            if (!AllowClose && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;

                Hide();
            }
        };
    }

    public void SetStatus(string text) => _status.Text = UiText.StatusPrefix + text;
    public void SetWarning(string text)
    {
        warningMessage = text;

        UpdateWarning();
    }

    private void UpdateWarning()
    {
        var missing = _store.MissingCopyCount;

        _warning.Text = warningMessage.Length > 0 ? warningMessage
            : missing > 0 ? string.Format(Messages.MissingCopiesFormat, missing) : string.Empty;

        _warning.Visible = _warning.Text.Length > 0;
    }

    public void SetHubstaffActivities(IReadOnlyList<HubstaffActivity> snapshot)
    {
        _activities.Clear();

        foreach (var entry in snapshot)
        {
            _activities[entry.Account] = entry;
        }

        UpdateActivity();
    }

    private void UpdateActivity()
    {
        var latest = selected == null
            ? _activities.Values.OrderByDescending(entry => entry.StartedUtc).FirstOrDefault()
            : _activities.GetValueOrDefault(selected.Account);

        if (latest == null)
        {
            _activity.Text = selected == null ? UiText.ActivityWaiting : UiText.ActivityUnavailable;

            _activityDetails.Text = UiText.ActivityWaitingDetails;

            return;
        }

        _activity.Text = string.Format(UiText.ActivityFormat,
            latest.OverallSeconds * 100d / latest.TrackedSeconds,
            latest.MouseSeconds * 100d / latest.TrackedSeconds,
            latest.KeyboardSeconds * 100d / latest.TrackedSeconds);

        _activityDetails.Text = string.Format(UiText.ActivityDetailsFormat,
            latest.StartedUtc.ToLocalTime(), latest.StartedUtc.AddSeconds(latest.TrackedSeconds).ToLocalTime(),
            latest.TrackedSeconds, latest.ObservedUtc.ToLocalTime());
    }
    public void RefreshEntries(string? selectId = null)
    {
        UpdateWarning();

        var selection = selectId ?? selected?.Id;

        var entries = _store.Snapshot();

        _dateFilter.SetAvailableDates(entries.Select(entry => entry.CapturedUtc.ToLocalTime()));

        var range = _dateFilter.SelectedRange;

        var filtered = new List<CaptureEntry>();

        _counts.Text = string.Format(UiText.HistoryCountsFormat, entries.Count(e => !e.Viewed), entries.Count);

        refreshing = true;

        _list.BeginUpdate();

        _list.Items.Clear();

        foreach (var entry in entries)
        {
            if (_captureFilter.SelectedFilter == CaptureFilter.Flagged && !entry.Flagged)
                continue;

            if (_captureFilter.SelectedFilter == CaptureFilter.Unread && entry.Viewed)
                continue;

            if (_captureFilter.SelectedFilter == CaptureFilter.PrivacyRisks
                && (!privacyResults.TryGetValue(entry.Id, out var scan)
                || scan.Findings.Count == 0 || scan.Disposition != PrivacyDisposition.Pending))
                continue;

            if (range != null)
            {
                var date = entry.CapturedUtc.ToLocalTime().Date;

                if (date < range.Start || date > range.End)
                    continue;
            }

            filtered.Add(entry);

            var index = _list.Items.Add(entry);

            if (entry.Id == selection)
            {
                _list.SelectedIndex = index;
            }
        }

        if (!showingGallery && _list.SelectedIndex < 0 && _list.Items.Count > 0)
        {
            _list.SelectedIndex = 0;
        }

        _list.EndUpdate();

        _scrollBar.RefreshViewport();

        refreshing = false;

        _gallery.SetEntries(filtered, resetGalleryScroll);

        UpdateGallerySelection();

        resetGalleryScroll = false;

        if (!showingGallery)
        {
            LoadSelection();
        }
        else if (filtered.Count == 0)
        {
            ShowEmptyState();
        }
    }

    public void ShowHistory(string? id = null)
    {
        SelectPage(ViewerPage.History);

        Show();

        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        BringToFront();

        Activate();

        if (id != null)
        {
            _captureFilter.SetFilter(CaptureFilter.All);

            _dateFilter.SetRange(null);

            OpenCapture(id);
        }
        else
        {
            ShowGallery();
        }

        if (_list.SelectedIndex >= 0)
        {
            _list.TopIndex = _list.SelectedIndex;
        }
    }

    private void LoadSelection()
    {
        selected = _list.SelectedItem as CaptureEntry;

        UpdatePrivacySelection();

        UpdateActivity();

        var previous = _picture.Image;

        _picture.Image = null;

        previous?.Dispose();

        _reviewed.Enabled = selected is { Viewed: false };

        _flag.Enabled = _zoom.Enabled = selected != null;

        _empty.Visible = selected == null;

        if (selected == null)
        {
            ShowEmptyState();

            return;
        }

        _flag.Text = selected.Flagged ? UiText.Unflag : UiText.Flag;

        _flag.ForeColor = selected.Flagged ? UiTheme.Amber : UiTheme.Text;

        _details.Text = selected.CapturedUtc.ToLocalTime().ToString(DateFormats.HistoryDetails);

        _fileName.Text = selected.FileName + UiText.WideSeparator + selected.Account;

        _tooltips.SetToolTip(_fileName, selected.Account + Environment.NewLine + selected.FileName);

        try
        {
            _picture.Image = _store.OpenImage(selected);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or OutOfMemoryException)
        {
            SetWarning(Messages.ImageUnavailablePrefix + ex.Message);

            _empty.Text = Messages.ImageUnavailable;

            _empty.Visible = true;
        }
    }

    private void ShowEmptyState()
    {
        var range = _dateFilter.SelectedRange;

        if (range != null)
        {
            _details.Text = range.Start == range.End
                ? string.Format(CalendarText.NoMatchesFormat, range.Start)
                : string.Format(CalendarText.NoRangeMatchesFormat, range.Start, range.End);

            _empty.Text = CalendarText.NoMatchesHint;
        }
        else if (_captureFilter.SelectedFilter == CaptureFilter.PrivacyRisks)
        {
            _details.Text = PrivacyText.NoRiskMatches;

            _empty.Text = PrivacyText.NoRiskMatchesHint;
        }
        else if (_captureFilter.SelectedFilter == CaptureFilter.Unread)
        {
            _details.Text = UiText.NoUnreadCaptures;

            _empty.Text = UiText.NewCapturesPlaceholder;
        }
        else
        {
            _details.Text = _captureFilter.SelectedFilter == CaptureFilter.Flagged
                ? UiText.NoFlaggedCaptures : UiText.NoCaptures;

            _empty.Text = _captureFilter.SelectedFilter == CaptureFilter.Flagged
                ? UiText.FlaggedCapturesPlaceholder : UiText.WaitingForFirstCapture;
        }

        _fileName.Text = UiText.NewCapturesPlaceholder;

        _gallery.SetEmptyState(_details.Text, _empty.Text);
    }

    internal void SetPrivacyResults(Dictionary<string, PrivacyScanRecord> results, AppSettings settings,
        string? error = null)
    {
        privacyResults = results.ToDictionary(pair => pair.Key, pair => PrivacyCoverage.Project(pair.Value, settings),
            StringComparer.OrdinalIgnoreCase);

        privacySettings = settings;

        _galleryActions.SetSettings(settings);

        _gallery.SetPrivacyResults(privacyResults);

        var queued = results.Values.Count(result => result.State == PrivacyScanState.Queued);

        var scanning = results.Values.Any(result => result.State == PrivacyScanState.Scanning);

        _galleryActions.SetQueue(queued, scanning, error);

        if (_captureFilter.SelectedFilter == CaptureFilter.PrivacyRisks)
        {
            RefreshEntries();
        }

        UpdatePrivacySelection();

        if (privacyWindow is { IsDisposed: false })
        {
            var result = privacyResults.TryGetValue(privacyWindow.CaptureId, out var record) ? record : null;

            privacyWindow.SetResult(result, privacySettings);
        }

        _picture.Invalidate();
    }

    private void UpdatePrivacySelection()
    {
        var result = selected != null && privacyResults.TryGetValue(selected.Id, out var record) ? record : null;

        _privacyButton.Enabled = selected != null;

        _privacyButton.ForeColor = result is { Findings.Count: > 0 } ? UiTheme.Amber : UiTheme.Text;
    }

    private void OpenPrivacyResult()
    {
        if (selected == null)
            return;

        if (privacyWindow is { IsDisposed: false } && privacyWindow.CaptureId == selected.Id)
        {
            ActivateExistingWindow(privacyWindow);

            return;
        }

        privacyWindow?.Close();

        privacyWindow = new PrivacyResultForm(selected.Id, selected.CapturedUtc.ToLocalTime()
            .ToString(DateFormats.HistoryDetails));

        var result = privacyResults.TryGetValue(selected.Id, out var record) ? record : null;

        privacyWindow.SetResult(result, privacySettings);

        privacyWindow.ScanRequested += (id, force) =>
        {
            var entry = _store.Snapshot().FirstOrDefault(entry => entry.Id == id);

            if (entry != null)
            {
                var categories = PrivacyCoverage.Selected(privacySettings);

                if (categories.Count == 0 && privacyResults.TryGetValue(id, out var saved))
                {
                    categories = saved.Categories.Select(category => category.Category).ToList();
                }

                PrivacyScanRequested?.Invoke([entry], force, categories);
            }
        };

        privacyWindow.DispositionRequested += (id, value) => PrivacyDispositionRequested?.Invoke(id, value);

        privacyWindow.Show(this);
    }

    private void DrawPrivacyRegions(object? sender, PaintEventArgs e)
    {
        if (selected == null || _picture.Image == null || !privacyResults.TryGetValue(selected.Id, out var result)
            || result.Disposition == PrivacyDisposition.FalsePositive)
            return;

        var image = _picture.Image;

        var scale = Math.Min(_picture.ClientSize.Width / (float)image.Width,
            _picture.ClientSize.Height / (float)image.Height);

        var width = image.Width * scale;

        var height = image.Height * scale;

        var left = (_picture.ClientSize.Width - width) / 2;

        var top = (_picture.ClientSize.Height - height) / 2;

        using var outline = new Pen(UiTheme.Amber, Math.Max(1.5f, DeviceDpi / 96f * 2));

        using var fill = new SolidBrush(Color.FromArgb(28, UiTheme.Amber));

        foreach (var region in result.Findings.SelectMany(finding => finding.Regions).Distinct())
        {
            var bounds = new RectangleF(left + region.X * width, top + region.Y * height,
                region.Width * width, region.Height * height);

            e.Graphics.FillRectangle(fill, bounds);

            e.Graphics.DrawRectangle(outline, bounds);
        }
    }

    private void OpenZoom()
    {
        if (selected == null)
            return;

        var existing = OwnedForms.OfType<ZoomForm>()
            .FirstOrDefault(form => !form.IsDisposed && form.CaptureId == selected.Id);

        if (existing != null)
        {
            ActivateExistingWindow(existing);

            return;
        }

        try
        {
            var image = _store.OpenImage(selected);

            var form = new ZoomForm(selected.Id, image, selected.CapturedUtc);

            form.Show(this);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or ArgumentException
            or OutOfMemoryException)
        {
            SetWarning(Messages.OpenFailedPrefix + ex.Message);
        }
    }

    private static void ActivateExistingWindow(Form form)
    {
        if (form.WindowState == FormWindowState.Minimized)
        {
            form.WindowState = FormWindowState.Normal;
        }

        form.Show();

        form.BringToFront();

        form.Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picture.Image?.Dispose();

            _tooltips.Dispose();

            privacyWindow?.Dispose();
        }

        base.Dispose(disposing);
    }
}
