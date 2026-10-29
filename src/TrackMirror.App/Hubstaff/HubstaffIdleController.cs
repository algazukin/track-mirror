// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

using TrackMirror.Diagnostics;

namespace TrackMirror.Hubstaff;

internal sealed class HubstaffIdleController(IHubstaffCli cli, Func<IdleObservation?> idle, TimeProvider? clock = null)
{
    private readonly IHubstaffCli _cli = cli;
    private readonly Func<IdleObservation?> _idle = idle;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private HubstaffControlOptions? previous;
    private HubstaffTrackingStatus? paused;
    private uint? inputTimestamp;
    private uint? pausedInputTimestamp;
    private long? restartStarted;
    private bool faulted;

    internal HubstaffControlStatus Status { get; private set; } = new(HubstaffControlState.Disabled);

    internal void Reset()
    {
        faulted = false;

        restartStarted = null;
    }

    internal async Task StepAsync(HubstaffControlOptions options, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        if (previous != options)
        {
            if (previous == null || previous.Mode != options.Mode || previous.CliPath != options.CliPath
                || !options.Enabled)
            {
                paused = null;
            }

            Reset();

            previous = options;
        }

        if (!options.Enabled)
        {
            Status = new(HubstaffControlState.Disabled);

            return;
        }

        if (faulted)
            return;

        try
        {
            var observation = Observe();

            var elapsed = observation.Duration;

            if (inputTimestamp != observation.InputTimestamp)
            {
                restartStarted = null;

                inputTimestamp = observation.InputTimestamp;
            }

            if (elapsed.TotalSeconds < options.IdleSeconds)
            {
                restartStarted = null;

                if (paused != null)
                {
                    if (pausedInputTimestamp == observation.InputTimestamp)
                    {
                        Status = new(HubstaffControlState.Paused);

                        return;
                    }

                    if (!await ResumeAsync(options, paused, token, requireStopped: false))
                    {
                        Status = new(HubstaffControlState.Paused);

                        return;
                    }

                    paused = null;
                }

                Status = new(HubstaffControlState.WaitingForIdle,
                    (int)Math.Ceiling(options.IdleSeconds - elapsed.TotalSeconds));

                return;
            }

            if (options.Mode == HubstaffControlMode.PauseUntilActive)
            {
                await PauseAsync(options, token);
            }
            else
            {
                await RestartAsync(options, token);
            }
        }
        catch (OperationCanceledException error) when (!token.IsCancellationRequested)
        {
            AppDiagnostics.Write(DiagnosticComponent.TimerControl, DiagnosticEvent.OperationFailed, error);
            paused = null;

            faulted = true;

            Status = new(HubstaffControlState.Error, FailureCode: HubstaffControlNames.TimedOut);
        }
        catch (HubstaffControlException error)
        {
            AppDiagnostics.Write(DiagnosticComponent.TimerControl, DiagnosticEvent.OperationFailed, error);
            paused = null;

            faulted = true;

            Status = new(HubstaffControlState.Error, FailureCode: error.Code);
        }
    }

    private async Task PauseAsync(HubstaffControlOptions options, CancellationToken token)
    {
        if (paused == null)
        {
            var current = await _cli.StatusAsync(options.CliPath, token);

            if (!current.Tracking)
            {
                Status = new(HubstaffControlState.WaitingForTracking);

                return;
            }

            if (current.ProjectId == null)
                throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);

            var latest = Observe();

            if (latest.Duration.TotalSeconds < options.IdleSeconds)
            {
                Status = new(HubstaffControlState.WaitingForIdle, options.IdleSeconds);

                return;
            }

            await _cli.ExecuteAsync(options.CliPath, HubstaffControlNames.Stop, token);

            paused = current;

            pausedInputTimestamp = latest.InputTimestamp;

            var stopped = await _cli.StatusAsync(options.CliPath, token);

            if (stopped.Tracking)
            {
                paused = null;

                throw new HubstaffControlException(HubstaffControlNames.StopUnconfirmed);
            }
        }

        Status = new(HubstaffControlState.Paused);

        return;
    }

    private async Task RestartAsync(HubstaffControlOptions options, CancellationToken token)
    {
        restartStarted ??= _clock.GetTimestamp();

        var remaining = options.RestartSeconds - _clock.GetElapsedTime(restartStarted.Value).TotalSeconds;

        if (remaining > 0)
        {
            Status = new(HubstaffControlState.WaitingForRestart, (int)Math.Ceiling(remaining));

            return;
        }

        var tracking = await _cli.StatusAsync(options.CliPath, token);

        if (tracking.Tracking)
        {
            if (tracking.ProjectId == null)
                throw new HubstaffControlException(HubstaffControlNames.InvalidStatus);

            var latest = Observe();

            if (latest.InputTimestamp != inputTimestamp || latest.Duration.TotalSeconds < options.IdleSeconds)
            {
                inputTimestamp = latest.InputTimestamp;

                restartStarted = latest.Duration.TotalSeconds >= options.IdleSeconds
                    ? _clock.GetTimestamp() : null;

                Status = restartStarted == null ? new(HubstaffControlState.WaitingForIdle, options.IdleSeconds)
                    : new(HubstaffControlState.WaitingForRestart, options.RestartSeconds);

                return;
            }

            await _cli.ExecuteAsync(options.CliPath, HubstaffControlNames.Stop, token);

            // Finish this confirmed mutation even if configuration changes or shutdown cancels the caller.
            using var finish = new CancellationTokenSource(
                TimeSpan.FromSeconds(HubstaffControlNames.CommandTimeoutSeconds));

            await ResumeAsync(options, tracking, finish.Token, requireStopped: true);
        }

        restartStarted = _clock.GetTimestamp();

        Status = tracking.Tracking ? new(HubstaffControlState.WaitingForRestart, options.RestartSeconds)
            : new(HubstaffControlState.WaitingForTracking);
    }

    private IdleObservation Observe() => _idle()
        ?? throw new HubstaffControlException(HubstaffControlNames.InputUnavailable);

    private async Task<bool> ResumeAsync(HubstaffControlOptions options, HubstaffTrackingStatus expected,
        CancellationToken token, bool requireStopped)
    {
        var current = await _cli.StatusAsync(options.CliPath, token);

        if (!expected.SameSelection(current))
        {
            paused = null;

            throw new HubstaffControlException(HubstaffControlNames.SelectionChanged);
        }

        if (!current.Tracking)
        {
            if (!requireStopped)
            {
                var latest = Observe();

                if (latest.InputTimestamp == pausedInputTimestamp
                    || latest.Duration.TotalSeconds >= options.IdleSeconds)
                    return false;
            }

            await _cli.ExecuteAsync(options.CliPath, HubstaffControlNames.Resume, token);

            var resumed = await _cli.StatusAsync(options.CliPath, token);

            if (!resumed.Tracking || !expected.SameSelection(resumed))
                throw new HubstaffControlException(HubstaffControlNames.ResumeUnconfirmed);
        }
        else if (requireStopped)
        {
            throw new HubstaffControlException(HubstaffControlNames.StopUnconfirmed);
        }

        return true;
    }
}
