// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Privacy.Models;

internal sealed record PrivacyCategoryResult
{
    public PrivacyScanCategory Category { get; init; }

    public string Version { get; init; } = string.Empty;

    public PrivacyScanState State { get; init; }

    public List<PrivacyFinding> Findings { get; init; } = [];

    public List<PrivacyRequestDiagnostic> Diagnostics { get; init; } = [];

    public string FailureCode { get; init; } = string.Empty;

    public DateTime UpdatedUtc { get; init; } = DateTime.UtcNow;
}
