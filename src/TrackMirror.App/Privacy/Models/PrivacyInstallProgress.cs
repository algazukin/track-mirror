// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Privacy.Models;

internal sealed record PrivacyInstallProgress(PrivacyInstallStage Stage, long Completed = 0, long Total = 0,
    string FailureCode = "")
{
    internal string Model { get; init; } = string.Empty;

    internal bool Busy => Stage is PrivacyInstallStage.RuntimeDownload or PrivacyInstallStage.RuntimeVerify
        or PrivacyInstallStage.RuntimeExtract or PrivacyInstallStage.ModelDownload or PrivacyInstallStage.ModelVerify
        or PrivacyInstallStage.ModelRemove;
}
