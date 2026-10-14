// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.Hubstaff;

internal sealed class HubstaffControlService : IAsyncDisposable
{
    private readonly object _gate = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly HubstaffIdleController _controller;
    private readonly Task _worker;
    private HubstaffControlOptions options;
    private HubstaffControlStatus status = new(HubstaffControlState.Disabled);
    private CancellationTokenSource? active;
    private int revision;

    internal event Action? Changed;

    internal HubstaffControlService(HubstaffControlOptions options, HubstaffIdleController? controller = null)
    {
        this.options = options;

        _controller = controller ?? new HubstaffIdleController(new HubstaffCli(), WindowsIdleTime.Read);

        _worker = Task.Run(RunAsync);
    }

    internal HubstaffControlStatus Snapshot()
    {
        lock (_gate)
        {
            return status;
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
                    await _controller.StepAsync(current, token);

                    var next = _controller.Status;

                    var changed = false;

                    lock (_gate)
                    {
                        if (!token.IsCancellationRequested && status != next)
                        {
                            status = next;

                            changed = true;
                        }
                    }

                    if (changed)
                    {
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
