// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Windows.Forms.VisualStyles;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class ThemedCheckBox : CheckBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if (Enabled)
        {
            base.OnPaint(e);

            return;
        }

        // The standard disabled renderer ignores ForeColor and embosses dark text on the surface.
        base.OnPaintBackground(e);

        var state = CheckState switch
        {
            CheckState.Checked => CheckBoxState.CheckedDisabled,
            CheckState.Indeterminate => CheckBoxState.MixedDisabled,
            _ => CheckBoxState.UncheckedDisabled
        };

        var glyph = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);

        var bounds = Rectangle.FromLTRB(Padding.Left, Padding.Top,
            Math.Max(Padding.Left, Width - Padding.Right), Math.Max(Padding.Top, Height - Padding.Bottom));

        var position = new Point(bounds.Left, bounds.Top + (bounds.Height - glyph.Height) / 2);

        CheckBoxRenderer.DrawCheckBox(e.Graphics, position, state);

        var offset = glyph.Width + LogicalToDeviceUnits(3);

        bounds.X += offset;

        bounds.Width = Math.Max(0, bounds.Width - offset);

        var flags = TextFormatFlags.VerticalCenter
            | (AutoEllipsis ? TextFormatFlags.EndEllipsis : TextFormatFlags.WordBreak);

        if (!UseMnemonic)
        {
            flags |= TextFormatFlags.NoPrefix;
        }
        else if (!ShowKeyboardCues)
        {
            flags |= TextFormatFlags.HidePrefix;
        }

        TextRenderer.DrawText(e.Graphics, Text, Font, bounds, UiTheme.Muted, flags);
    }
}
