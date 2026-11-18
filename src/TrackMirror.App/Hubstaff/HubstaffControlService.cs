// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

using TrackMirror.Diagnostics;
using TrackMirror.Models;

namespace TrackMirror.Hubstaff;

internal sealed class HubstaffControlService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HubstaffIdleController _controller;
    private readonly IHubstaffCli _cli;
    private readonly TimerNoteTracker _notes = new();
    private readonly TimeProvider _clock;
    private readonly Task _worker;
    private TimerNotesOptions noteOptions = new();
    private long? lastNotePoll;
    private bool? notesAvailable;
    private int noteRevision;
    private string? noteCliPath;
    private HubstaffControlOptions options;
    private HubstaffControlStatus status = new(HubstaffControlState.Disabled);
    private CancellationTokenSource? active;
    private int revision;

    internal event Action? Changed;

    internal event Action<TimerStopNote>? NoteRecorded;

    internal bool NotesAvailable => notesAvailable == true;

    internal HubstaffControlService(HubstaffControlOptions options, HubstaffIdleController? controller = null,
        TimerNotesOptions? notes = null, IHubstaffCli? cli = null, TimeProvider? clock = null,
        Action<TimerStopNote>? onNote = null)
    {
        this.options = options;

        _cli = cli ?? new HubstaffCli();
        _clock = clock ?? TimeProvider.System;
        noteOptions = notes ?? new TimerNotesOptions();
        _notes.SetEnabled(noteOptions.Enabled);
        _controller = controller ?? new HubstaffIdleController(_cli, WindowsIdleTime.Read);
        _controller.IdleStopped += (tracking, seconds) =>
            PublishNote(_notes.IdleStopped(tracking, seconds, _clock.GetUtcNow().UtcDateTime));
        _controller.InternalStopped += _notes.IgnoreStop;
        _controller.TrackingResumed += tracking => _notes.Observe(tracking, _clock.GetUtcNow().UtcDateTime);

        NoteRecorded += onNote;

        _worker = Task.Run(RunAsync);
    }

    internal HubstaffControlStatus Snapshot()
    {
        lock (_gate)
        {
            return status;
        }
    }

    internal void ApplyNotes(TimerNotesOptions value)
    {
        lock (_gate)
        {
            if (noteOptions.Enabled != value.Enabled)
            {
                noteRevision++;
                notesAvailable = null;
            }

            noteOptions = value;
        }
    }

    private void PublishNote(TimerStopNote? note)
    {
        lock (_gate)
        {
            if (!noteOptions.Enabled)
            {
                return;
            }
        }

        if (note != null)
        {
            NoteRecorded?.Invoke(note);
        }
    }

    private async Task ObserveNotesAsync(HubstaffControlOptions control, CancellationToken token)
    {
        TimerNotesOptions current;

        lock (_gate)
        {
            current = noteOptions;
        }

        _notes.SetEnabled(current.Enabled);

        if (!current.Enabled)
        {
            lastNotePoll = null;
            return;
        }

        if (noteCliPath != control.CliPath)
        {
            noteCliPath = control.CliPath;
            lastNotePoll = null;
            _notes.SetEnabled(false);
            _notes.SetEnabled(true);
        }

        if (lastNotePoll != null && _clock.GetElapsedTime(lastNotePoll.Value).TotalSeconds < 5)
        {
            return;
        }

        lastNotePoll = _clock.GetTimestamp();

        try
        {
            var tracking = await _cli.StatusAsync(control.CliPath, token);
            PublishNote(_notes.Observe(tracking, _clock.GetUtcNow().UtcDateTime));

            if (notesAvailable != true)
            {
                notesAvailable = true;
                Changed?.Invoke();
            }
        }
        catch (Exception error) when (error is HubstaffControlException
            || error is OperationCanceledException && !token.IsCancellationRequested)
        {
            if (notesAvailable != false)
            {
                notesAvailable = false;
                AppDiagnostics.Write(DiagnosticComponent.TimerControl, DiagnosticEvent.OperationFailed, error);
                Changed?.Invoke();
            }
        }
    }

    internal void Apply(HubstaffControlOptions value, bool retry = false)
    {
        lock (_gate)
        {
            if (options == value && !retry)
                return;

            options = value;

            revision++;

            active?.Cancel();
        }
    }

    private async Task RunAsync()
    {
        var appliedRevision = -1;
        var appliedNoteRevision = -1;

        try
        {
            while (!_shutdown.IsCancellationRequested)
            {
                HubstaffControlOptions current;

                CancellationToken token;

                lock (_gate)
                {
                    current = options;

                    active = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);

                    token = active.Token;

                    if (appliedRevision != revision)
                    {
                        _controller.Reset();

                        appliedRevision = revision;
                    }
                }

                try
                {
                    TimerNotesOptions currentNotes;

                    lock (_gate)
                    {
                        currentNotes = noteOptions;

                        if (appliedNoteRevision != noteRevision)
                        {
                            _notes.SetEnabled(false);
                            lastNotePoll = null;
                            appliedNoteRevision = noteRevision;
                        }
                    }

                    _notes.SetEnabled(currentNotes.Enabled);

                    await _controller.StepAsync(current, token);

                    await ObserveNotesAsync(current, token);

                    var next = _controller.Status;

                    var changed = false;
                    var stateChanged = false;

                    lock (_gate)
                    {
                        if (!token.IsCancellationRequested && status != next)
                        {
                            stateChanged = status.State != next.State;
                            status = next;

                            changed = true;
                        }
                    }

                    if (changed)
                    {
                        if (stateChanged)
                            AppDiagnostics.Write(DiagnosticComponent.TimerControl, DiagnosticEvent.StateChanged,
                                metrics: new DiagnosticMetrics { Code = (int)next.State });

                        Changed?.Invoke();
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                finally
                {
                    lock (_gate)
                    {
                        active.Dispose();

                        active = null;
                    }
                }

                await Task.Delay(HubstaffControlNames.PollMilliseconds, _shutdown.Token);
            }
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            _shutdown.Cancel();

            active?.Cancel();
        }

        await _worker;

        _shutdown.Dispose();
    }
}
