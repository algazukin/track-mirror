// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class HubstaffIdleControlTests
{
    [Fact]
    public async Task Changed_selection_is_not_automatically_resumed()
    {
        // Arrange
        var cli = new ControlCliDouble();

        var input = new IdleObservation(TimeSpan.FromMinutes(2), 10);

        var controller = new HubstaffIdleController(cli, () => input);

        var options = new HubstaffControlOptions { Enabled = true };

        // Act
        await controller.StepAsync(options, default);

        input = new IdleObservation(TimeSpan.Zero, 11);

        cli.Project = "43";

        await controller.StepAsync(options, default);

        // Assert
        Assert.Equal(new[] { "stop" }, cli.Commands);

        Assert.Equal(HubstaffControlNames.SelectionChanged, controller.Status.FailureCode);
    }

    [Fact]
    public async Task Idle_pause_stops_at_threshold_and_resumes_only_after_new_input()
    {
        // Arrange
        var cli = new ControlCliDouble();

        var input = new IdleObservation(TimeSpan.FromSeconds(59), 10);

        var controller = new HubstaffIdleController(cli, () => input);

        var options = new HubstaffControlOptions { Enabled = true, IdleSeconds = 60 };

        // Act
        await controller.StepAsync(options, default);

        // Assert
        Assert.Empty(cli.Commands);

        // Act
        input = input with { Duration = TimeSpan.FromSeconds(60) };

        await controller.StepAsync(options, default);

        // Assert
        Assert.Equal(new[] { "stop" }, cli.Commands);

        // Act
        await controller.StepAsync(options with { IdleSeconds = 120 }, default);

        // Assert
        Assert.Equal(new[] { "stop" }, cli.Commands);

        // Act
        input = new IdleObservation(TimeSpan.Zero, 11);

        await controller.StepAsync(options, default);

        // Assert
        Assert.Equal(new[] { "stop", "resume" }, cli.Commands);
    }

    [Fact]
    public async Task Restart_countdown_resets_for_input_between_observations()
    {
        // Arrange
        var clock = new ControlClock();

        var cli = new ControlCliDouble();

        var input = new IdleObservation(TimeSpan.FromSeconds(2), 10);

        var controller = new HubstaffIdleController(cli, () => input, clock);

        var options = new HubstaffControlOptions
        {
            Enabled = true, Mode = HubstaffControlMode.RestartWhileIdle, IdleSeconds = 1, RestartSeconds = 30
        };

        // Act
        await controller.StepAsync(options, default);

        clock.Advance(22);

        input = input with { InputTimestamp = 11 };

        await controller.StepAsync(options, default);

        clock.Advance(8);

        await controller.StepAsync(options, default);

        // Assert
        Assert.Empty(cli.Commands);

        // Act
        clock.Advance(22);

        await controller.StepAsync(options, default);

        // Assert
        Assert.Equal(new[] { "stop", "resume" }, cli.Commands);
    }

    [Fact]
    public async Task Manual_pause_is_never_started_by_idle_control()
    {
        // Arrange
        var cli = new ControlCliDouble { Tracking = false };

        var input = new IdleObservation(TimeSpan.FromMinutes(2), 10);

        var controller = new HubstaffIdleController(cli, () => input);

        var options = new HubstaffControlOptions { Enabled = true };

        // Act
        await controller.StepAsync(options, default);

        input = new IdleObservation(TimeSpan.Zero, 11);

        await controller.StepAsync(options, default);

        // Assert
        Assert.Empty(cli.Commands);
    }

    [Fact]
    public async Task Status_failure_revokes_pause_ownership_before_retry()
    {
        // Arrange
        var cli = new ControlCliDouble();

        var input = new IdleObservation(TimeSpan.FromMinutes(2), 10);

        var controller = new HubstaffIdleController(cli, () => input);

        var options = new HubstaffControlOptions { Enabled = true };

        // Act
        await controller.StepAsync(options, default);

        input = new IdleObservation(TimeSpan.Zero, 11);

        cli.Failure = HubstaffControlNames.CommandFailed;

        await controller.StepAsync(options, default);

        // Assert
        Assert.Equal(HubstaffControlState.Error, controller.Status.State);

        // Act
        cli.Failure = null;

        cli.Tracking = false;

        controller.Reset();

        await controller.StepAsync(options, default);

        // Assert
        Assert.Equal(new[] { "stop" }, cli.Commands);
    }

    [Fact]
    public async Task Confirmed_periodic_stop_completes_resume_after_caller_cancellation()
    {
        // Arrange
        var clock = new ControlClock();

        using var cancellation = new CancellationTokenSource();

        var cli = new ControlCliDouble { AfterStop = cancellation.Cancel };

        var controller = new HubstaffIdleController(cli,
            () => new IdleObservation(TimeSpan.FromMinutes(2), 10), clock);

        var options = new HubstaffControlOptions
        {
            Enabled = true, Mode = HubstaffControlMode.RestartWhileIdle, RestartSeconds = 30
        };

        // Act
        await controller.StepAsync(options, cancellation.Token);

        clock.Advance(30);

        await controller.StepAsync(options, cancellation.Token);

        // Assert
        Assert.Equal(new[] { "stop", "resume" }, cli.Commands);

        Assert.True(cli.Tracking);
    }

    [Fact]
    public async Task Activity_during_status_lookup_prevents_stop()
    {
        // Arrange
        var input = new IdleObservation(TimeSpan.FromMinutes(2), 10);

        var cli = new ControlCliDouble
        {
            OnStatus = () => input = new IdleObservation(TimeSpan.Zero, 11)
        };

        var controller = new HubstaffIdleController(cli, () => input);

        // Act
        await controller.StepAsync(new HubstaffControlOptions { Enabled = true }, default);

        // Assert
        Assert.Empty(cli.Commands);
    }
}
