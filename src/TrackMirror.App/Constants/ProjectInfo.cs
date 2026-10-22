// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Reflection;

namespace TrackMirror.Constants;

internal static class ProjectInfo
{
    internal const string RepositoryUrl = "https://github.com/algazukin/track-mirror";

    internal const string LicenseUrl = RepositoryUrl + "/blob/main/LICENSE";

    internal const string ReleasesApiUrl = "https://api.github.com/repos/algazukin/track-mirror/releases";

    internal static string Version => typeof(ProjectInfo).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(ProjectInfo).Assembly.GetName().Version?.ToString(3) ?? "unknown";
}
