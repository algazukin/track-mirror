// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.UI.Controls;

internal sealed class CaptureTileAccessibleObject(CaptureGallery gallery, int index) : AccessibleObject
{
    public override AccessibleRole Role => AccessibleRole.ListItem;

    public override AccessibleObject? Parent => gallery.AccessibilityObject;

    public override string? DefaultAction => UiText.Open;

    public override string? Name
    {
        get
        {
            if (index >= gallery.Entries.Count)
                return string.Empty;

            var entry = gallery.Entries[index];

            return entry.CapturedUtc.ToLocalTime().ToString(DateFormats.HistoryDetails) + UiText.Separator
                + (entry.Viewed ? UiText.Reviewed : UiText.New)
                + (entry.Flagged ? UiText.FlaggedSuffix : string.Empty) + UiText.Separator
                + entry.Account + UiText.Separator + entry.FileName;
        }
        set { }
    }

    public override Rectangle Bounds => index < gallery.Entries.Count
        ? gallery.RectangleToScreen(gallery.TileBounds(index))
        : Rectangle.Empty;

    public override AccessibleStates State
    {
        get
        {
            var state = AccessibleStates.Focusable | AccessibleStates.Selectable;

            if (gallery.IsSelected(index))
            {
                state |= AccessibleStates.Selected;
            }

            if (index == gallery.FocusedIndex && gallery.Focused)
            {
                state |= AccessibleStates.Focused;
            }

            if (index >= gallery.Entries.Count || !gallery.Visible
                || !gallery.ClientRectangle.IntersectsWith(gallery.TileBounds(index)))
            {
                state |= AccessibleStates.Offscreen;
            }

            return state;
        }
    }

    public override void DoDefaultAction() => gallery.OpenIndex(index);

    public override void Select(AccessibleSelection flags)
    {
        if (flags.HasFlag(AccessibleSelection.TakeSelection))
        {
            gallery.SelectAll(false);
        }

        if (flags.HasFlag(AccessibleSelection.AddSelection) || flags.HasFlag(AccessibleSelection.TakeSelection))
        {
            gallery.SetSelected(index, true);
        }

        if (flags.HasFlag(AccessibleSelection.RemoveSelection))
        {
            gallery.SetSelected(index, false);
        }
    }
}
