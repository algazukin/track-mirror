// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Models;

public sealed record CaptureEntry
{
    public string Id { get; init; } = string.Empty;
    public string Account { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTime CapturedUtc
    {
        get; init;
    }
    public DateTime SavedUtc
    {
        get; init;
    }
    public bool Viewed
    {
        get; init;
    }
    public bool Flagged
    {
        get; init;
    }
}
