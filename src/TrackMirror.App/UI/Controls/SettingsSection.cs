// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.UI.Controls;

internal abstract class SettingsSection : UserControl
{
    protected SettingsSection()
    {
        AutoSize = true;

        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        Dock = DockStyle.Top;

        Margin = Padding.Empty;
    }

    public override Size GetPreferredSize(Size proposedSize)
    {
        if (Controls.Count == 0)
            return base.GetPreferredSize(proposedSize);

        // Measure wrapped text and button rows at the width available in the settings viewport.
        var width = proposedSize.Width > 0 ? proposedSize.Width : Width;

        var content = Controls[0].GetPreferredSize(new Size(Math.Max(1, width - Padding.Horizontal), 0));

        return new Size(width, content.Height + Padding.Vertical);
    }
}
