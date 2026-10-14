// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.UI.Controls;

internal sealed class CaptureGalleryAccessibleObject(CaptureGallery gallery) : Control.ControlAccessibleObject(gallery)
{
    public override int GetChildCount() => gallery.Entries.Count;

    public override AccessibleObject? GetChild(int index) => index >= 0 && index < gallery.Entries.Count
        ? new CaptureTileAccessibleObject(gallery, index)
        : null;

    public override AccessibleObject? GetFocused() => gallery.Focused && gallery.FocusedIndex >= 0
        ? GetChild(gallery.FocusedIndex)
        : base.GetFocused();

    public override AccessibleObject? GetSelected()
    {
        for (var index = 0; index < gallery.Entries.Count; index++)
        {
            if (gallery.IsSelected(index))
                return GetChild(index);
        }

        return null;
    }

    public override AccessibleObject? HitTest(int x, int y)
    {
        var index = gallery.IndexAt(gallery.PointToClient(new Point(x, y)));

        return index >= 0 ? GetChild(index) : base.HitTest(x, y);
    }
}
