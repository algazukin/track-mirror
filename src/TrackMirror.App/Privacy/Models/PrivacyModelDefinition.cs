// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;

namespace TrackMirror.Privacy.Models;

internal sealed record PrivacyModelDefinition(string Tag, string ManifestDigest,
    string LocalManifestDigest, string ManifestResource, long DownloadBytes)
{
    internal string Caption => Tag switch
    {
        PrivacyModelNames.Qwen2B => PrivacyInstallText.Qwen2B,
        PrivacyModelNames.Qwen4B => PrivacyInstallText.Qwen4B,
        PrivacyModelNames.Qwen8B => PrivacyInstallText.Qwen8B,
        PrivacyModelNames.Gemma4B => PrivacyInstallText.Gemma4B,
        _ => Tag
    };
}
