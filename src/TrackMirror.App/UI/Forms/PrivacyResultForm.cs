// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Forms;

internal sealed class PrivacyResultForm : Form
{
    private readonly PrivacyDetailPanel _details = new();
    private readonly ScrollViewport _viewport;

    internal string CaptureId { get; }

    internal event Action<string, bool>? ScanRequested;

    internal event Action<string, PrivacyDisposition>? DispositionRequested;

    internal PrivacyResultForm(string id, string caption)
    {
        SuspendLayout();

        CaptureId = id;

        Text = PrivacyText.Heading + PrivacyText.DetailSeparator + caption;

        Icon = ApplicationIcon.Main;

        Font = new Font(UiText.FontFamily, 10);

        AutoScaleMode = AutoScaleMode.Dpi;

        AutoScaleDimensions = new SizeF(96, 96);

        ClientSize = new Size(600, 330);

        MinimumSize = new Size(470, 300);

        StartPosition = FormStartPosition.CenterParent;

        BackColor = UiTheme.Surface;

        Padding = new Padding(20);

        _viewport = new ScrollViewport(_details, fillHeight: true) { BackColor = UiTheme.Surface };

        Controls.Add(_viewport);

        _details.ScanRequested += (value, force) => ScanRequested?.Invoke(value, force);

        _details.DispositionRequested += (value, disposition) => DispositionRequested?.Invoke(value, disposition);

        ResumeLayout(true);
    }

    internal void SetResult(PrivacyScanRecord? result, AppSettings settings)
    {
        _details.SetResult(CaptureId, result, settings);

        _viewport.RefreshViewport();
    }
}
