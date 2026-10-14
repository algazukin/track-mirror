// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class CancellationGate
{
    internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal async Task WaitAsync(CancellationToken token)
    {
        Entered.TrySetResult();

        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            Cancelled.TrySetResult();

            // Shutdown must keep dispatching UI callbacks while it awaits cancellation.
            await Released.Task.WaitAsync(TimeSpan.FromSeconds(5));

            throw;
        }
    }
}
