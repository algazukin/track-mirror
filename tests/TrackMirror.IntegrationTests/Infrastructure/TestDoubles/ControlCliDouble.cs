// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Hubstaff;

namespace TrackMirror.IntegrationTests.Infrastructure.TestDoubles;

internal sealed class ControlCliDouble : IHubstaffCli
{
    internal bool Tracking { get; set; } = true;
    internal string Project { get; set; } = "42";
    internal List<string> Commands { get; } = [];
    internal int Calls { get; private set; }
    internal string? Failure { get; set; }
    internal Action? AfterStop { get; set; }
    internal Action? OnStatus { get; set; }

    public Task<HubstaffTrackingStatus> StatusAsync(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Calls++;

        if (Failure != null) throw new HubstaffControlException(Failure);
        OnStatus?.Invoke();

        return Task.FromResult(new HubstaffTrackingStatus(Tracking, Project, null));
    }
    public Task ExecuteAsync(string path, string command, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();

        Calls++;

        if (Failure != null) throw new HubstaffControlException(Failure);
        Commands.Add(command);

        Tracking = command == "resume";

        if (command == "stop")
        {
            AfterStop?.Invoke();
        }

        return Task.CompletedTask;
    }
}
