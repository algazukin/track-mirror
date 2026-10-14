// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

namespace TrackMirror.Privacy.Models;

internal sealed record PrivacyRequestDiagnostic
{
    public Guid RequestId { get; init; } = Guid.NewGuid();

    public int RequestBytes { get; init; }

    public int OcrBlocks { get; init; }

    public int OcrCharacters { get; init; }

    public int? PromptTokens { get; init; }

    public int? ReplyTokens { get; init; }

    public double DurationMilliseconds { get; init; }

    public string FailureCode { get; init; } = string.Empty;
}
