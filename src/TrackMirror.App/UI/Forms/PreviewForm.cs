// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Interop;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Forms;

public sealed class PreviewForm : Form
{
    private readonly ScreenshotStore _store;
    private readonly System.Windows.Forms.Timer _dismissTimer = new()
    {
        Interval = NotificationDefaults.TimeoutSeconds * 1000
    };
    private readonly PictureBox _picture = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.Zoom,
        BackColor = UiTheme.Viewer,
        Margin = Padding.Empty
    };
    private readonly Label _caption = UiTheme.Label(string.Empty, 9, muted: true);
    private readonly ActionButton _reviewed = new(UiText.MarkReviewed)
    {
        Enabled = false,
        Width = 140
    };
    private readonly ActionButton _flag = new(UiText.Flag)
    {
        Width = 132,
        Margin = Padding.Empty
    };
    private CaptureEntry? entry;
    public event Action<string>? OpenRequested;
    public event Action<string>? ReviewRequested;
    public event Action<string>? FlagRequested;
    protected override bool ShowWithoutActivation => true;

    public PreviewForm(ScreenshotStore store)
    {
        SuspendLayout();

        _store = store;

        ConfigureWindow();

        Controls.Add(CreateLayout());

        _dismissTimer.Tick += (_, _) => Hide();

        FormClosing += (_, e) =>
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;

                Hide();
            }
        };

        ResumeLayout(true);
    }

    private void ConfigureWindow()
    {
        Text = UiText.PreviewWindowTitle;

        Icon = ApplicationIcon.Main;

        Font = new Font(UiText.FontFamily, 9.5f);

        AutoScaleMode = AutoScaleMode.Dpi;

        AutoScaleDimensions = new SizeF(96, 96);

        ClientSize = new Size(440, 330);

        BackColor = UiTheme.Background;

        ForeColor = UiTheme.Text;

        FormBorderStyle = FormBorderStyle.FixedToolWindow;

        ShowInTaskbar = false;

        StartPosition = FormStartPosition.Manual;

        DoubleBuffered = true;
    }

    private FlowLayoutPanel CreateActions()
    {
        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(0, 16, 0, 0),
            WrapContents = false,
            Margin = Padding.Empty
        };

        var open = new ActionButton(UiText.Open, true)
        {
            Width = 120
        };

        actions.Controls.AddRange([open, _reviewed, _flag]);

        open.Click += (_, _) =>
        {
            if (entry != null)
            {
                OpenRequested?.Invoke(entry.Id);
            }
        };

        _reviewed.Click += (_, _) =>
        {
            if (entry != null)
            {
                ReviewRequested?.Invoke(entry.Id);
            }

            Hide();
        };

        _flag.Click += (_, _) =>
        {
            if (entry != null)
            {
                FlagRequested?.Invoke(entry.Id);
            }
        };

        return actions;
    }

    private TableLayoutPanel CreateLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16)
        };

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));

        var heading = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = Padding.Empty
        };

        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));

        heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));

        heading.Controls.Add(UiTheme.Label(UiText.PreviewHeading, 13, true), 0, 0);

        heading.Controls.Add(_caption, 0, 1);

        var imageFrame = new SurfacePanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(5),
            Margin = Padding.Empty
        };

        imageFrame.Controls.Add(_picture);

        root.Controls.Add(heading, 0, 0);

        root.Controls.Add(imageFrame, 0, 1);

        root.Controls.Add(CreateActions(), 0, 2);

        return root;
    }

    public void ShowCapture(CaptureEntry capture)
    {
        var image = _store.OpenImage(capture);

        var previous = _picture.Image;

        _picture.Image = image;

        previous?.Dispose();

        entry = capture;

        UpdateCaption();

        var area = Screen.PrimaryScreen!.WorkingArea;

        Location = new Point(area.Right - Width - 16, area.Bottom - Height - 16);

        Show();

        // Form.TopMost makes WinForms focus an action button when showing the popup.
        WindowPosition.KeepAboveWithoutActivation(Handle);

        _dismissTimer.Stop();

        _dismissTimer.Start();
    }

    public void SetNotificationTimeout(int seconds)
    {
        if (seconds is < NotificationDefaults.MinimumSeconds or > NotificationDefaults.MaximumSeconds)
            throw new ArgumentOutOfRangeException(nameof(seconds));

        _dismissTimer.Stop();

        _dismissTimer.Interval = seconds * 1000;

        if (Visible)
        {
            _dismissTimer.Start();
        }
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        if (!Visible)
        {
            _dismissTimer.Stop();
        }

        base.OnVisibleChanged(e);
    }

    public void UpdateCaption()
    {
        if (entry == null)
            return;

        var entries = _store.Snapshot();

        entry = entries.FirstOrDefault(e => e.Id == entry.Id) ?? entry;

        _reviewed.Enabled = !entry.Viewed;

        _caption.Text = entry.CapturedUtc.ToLocalTime().ToString(
            DateFormats.Time
        ) + string.Format(
            UiText.PreviewUnreadFormat,
            entries.Count(e => !e.Viewed)
        );

        _flag.Text = entry.Flagged ? UiText.Unflag : UiText.Flag;

        _flag.ForeColor = entry.Flagged ? UiTheme.Amber : UiTheme.Text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _dismissTimer.Dispose();

            _picture.Image?.Dispose();
        }

        base.Dispose(disposing);
    }
}
