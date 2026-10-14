// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Constants;

internal static class NativeWindowConstants
{
    internal const string User32 = "user32.dll";
    internal const int TopMost = -1;
    internal const uint NoSize = 0x0001;
    internal const uint NoMove = 0x0002;
    internal const uint NoActivate = 0x0010;
    internal const int VerticalScrollStyle = 0x00200000;
    internal const int KeyDownMessage = 0x0100;
    internal const int CharacterMessage = 0x0102;
    internal const int SizeMessage = 0x0005;
    internal const int TimerMessage = 0x0113;
    internal const int MouseMoveMessage = 0x0200;
    internal const int LeftButtonDownMessage = 0x0201;
    internal const int LeftButtonUpMessage = 0x0202;
    internal const int VerticalScrollMessage = 0x0115;
    internal const int MouseWheelMessage = 0x020A;
    internal const int SetTextMessage = 0x000C;
    internal const int TextFirstVisibleLine = 0x00CE;
    internal const int TextLineCount = 0x00BA;
    internal const int TextLineScroll = 0x00B6;
    internal const int TextSetSelection = 0x00B1;
    internal const int TextScrollCaret = 0x00B7;
    internal const int ListSetSelectionMessage = 0x0186;
    internal const int ListSetTopIndexMessage = 0x0197;
    internal const int ListSetItemHeightMessage = 0x01A0;
}
