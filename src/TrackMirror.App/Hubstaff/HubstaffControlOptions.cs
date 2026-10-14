// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.Hubstaff;

public sealed record HubstaffControlOptions
{
    public bool Enabled { get; init; }

    public HubstaffControlMode Mode { get; init; }

    public int IdleSeconds { get; init; } = HubstaffControlNames.DefaultIdleSeconds;

    public int RestartSeconds { get; init; } = HubstaffControlNames.DefaultRestartSeconds;

    public string CliPath { get; init; } = string.Empty;

    internal bool IsValid => Enum.IsDefined(Mode) && IdleSeconds is >= 1 and <= HubstaffControlNames.MaximumSeconds
        && RestartSeconds is >= 1 and <= HubstaffControlNames.MaximumSeconds
        && CliPath != null && (CliPath.Length == 0 || Path.IsPathFullyQualified(CliPath));
}
