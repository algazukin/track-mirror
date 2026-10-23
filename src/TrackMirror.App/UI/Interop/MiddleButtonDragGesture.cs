// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.UI.Interop;

internal sealed class MiddleButtonDragGesture
{
    private Rectangle target;
    private Point? offset;

    internal Rectangle Target
    {
        get => target;
        set
        {
            target = value;

            if (value.IsEmpty)
                offset = null;
        }
    }

    internal bool Process(int message, Point pointer, out Point? position, out bool completed)
    {
        position = null;
        completed = false;

        if (message == 0x0207 && Target.Contains(pointer)) // WM_MBUTTONDOWN
        {
            offset = new Point(pointer.X - Target.Left, pointer.Y - Target.Top);
            position = Target.Location;

            return true;
        }

        if (offset is not { } delta || message is not (0x0200 or 0x0208)) // MOVE / MBUTTONUP
            return false;

        position = new Point(pointer.X - delta.X, pointer.Y - delta.Y);
        completed = message == 0x0208;

        if (completed)
            offset = null;

        return completed;
    }
}
