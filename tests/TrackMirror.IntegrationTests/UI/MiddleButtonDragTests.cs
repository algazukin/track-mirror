// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.UI.Interop;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

public sealed class MiddleButtonDragTests
{
    [Fact]
    public void Native_listener_with_no_target_releases_its_message_loop_on_disposal()
    {
        // Arrange
        var callbacks = 0;
        using var listener = MiddleButtonDragListener.Start(Rectangle.Empty, (_, _) => Interlocked.Increment(ref callbacks));

        // Assert
        Assert.NotNull(listener);
        var thread = UiControls.Field<Thread>(listener, "_thread");
        Assert.True(thread.IsAlive);

        // Act
        listener.Dispose();
        listener.Dispose();

        // Assert
        Assert.False(thread.IsAlive);
        Assert.Equal(0, callbacks);
    }

    [Fact]
    public void Only_a_middle_press_inside_the_widget_starts_a_drag_and_its_release_is_consumed()
    {
        // Arrange
        var gesture = new MiddleButtonDragGesture { Target = new Rectangle(100, 200, 120, 44) };

        // Act
        var outside = gesture.Process(0x207, new Point(99, 210), out _, out _);
        var left = gesture.Process(0x201, new Point(110, 210), out _, out _);
        var right = gesture.Process(0x204, new Point(110, 210), out _, out _);
        var down = gesture.Process(0x207, new Point(110, 210), out _, out _);
        var move = gesture.Process(0x200, new Point(310, 410), out var position, out _);
        var up = gesture.Process(0x208, new Point(330, 430), out var final, out var completed);
        var extraUp = gesture.Process(0x208, new Point(330, 430), out _, out _);

        // Assert
        Assert.False(outside);
        Assert.False(left);
        Assert.False(right);
        Assert.True(down);
        Assert.False(move);
        Assert.Equal(new Point(300, 400), position);
        Assert.True(up);
        Assert.Equal(new Point(320, 420), final);
        Assert.True(completed);
        Assert.False(extraUp);
    }

    [Fact]
    public void Hiding_the_target_cancels_dragging_and_does_not_intercept_later_input()
    {
        // Arrange
        var gesture = new MiddleButtonDragGesture { Target = new Rectangle(-100, 200, 120, 44) };
        gesture.Process(0x207, new Point(-90, 210), out _, out _);

        // Act
        gesture.Target = Rectangle.Empty;
        var move = gesture.Process(0x200, new Point(300, 400), out var position, out _);
        var up = gesture.Process(0x208, new Point(300, 400), out _, out _);
        var down = gesture.Process(0x207, new Point(0, 0), out _, out _);

        // Assert
        Assert.False(move);
        Assert.Null(position);
        Assert.False(up);
        Assert.False(down);
    }
}
