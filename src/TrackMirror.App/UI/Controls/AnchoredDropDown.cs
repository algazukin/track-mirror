// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class AnchoredDropDown : ToolStripDropDown
{
    private Control? anchor;

    internal AnchoredDropDown()
    {
        Padding = new Padding(1);

        BackColor = UiTheme.Surface;

        ForeColor = UiTheme.Text;

        Renderer = new ToolStripSystemRenderer();
    }

    internal void ShowAnchored(Control control, Point location)
    {
        anchor = control;

        Show(control, location);
    }

    protected override void OnClosing(ToolStripDropDownClosingEventArgs e)
    {
        // Let the anchor's Click handler close the popup instead of reopening it after auto-close.
        if (e.CloseReason == ToolStripDropDownCloseReason.AppClicked && anchor is { IsDisposed: false }
            && anchor.RectangleToScreen(anchor.ClientRectangle).Contains(MousePosition))
        {
            e.Cancel = true;
        }

        base.OnClosing(e);
    }
}
