// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json.Serialization;

namespace TrackMirror.Privacy.Models;

internal sealed record PrivacyScanRecord
{
    public required string CaptureId { get; init; }

    public required string RequestId { get; init; }

    public required string Profile { get; init; }

    public string ImageHash { get; init; } = string.Empty;

    public PrivacyScanState State { get; init; }

    public List<PrivacyFinding> Findings { get; init; } = [];

    public List<PrivacyCategoryResult> Categories { get; init; } = [];

    public List<PrivacyScanCategory> RequestedCategories { get; init; } = [];

    [JsonIgnore]
    public bool NeedsCheck { get; init; }

    public string FailureCode { get; init; } = string.Empty;

    public PrivacyDisposition Disposition { get; init; }

    public DateTime UpdatedUtc { get; init; } = DateTime.UtcNow;

    public int Attempts { get; init; }

    public double DurationSeconds { get; init; }
}
