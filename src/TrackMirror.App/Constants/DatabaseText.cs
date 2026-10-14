// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;

namespace TrackMirror.Constants;

internal static class DatabaseText
{
    internal static string Unavailable => UiLocalization.Get(nameof(DatabaseText), nameof(Unavailable));
    internal static string WriteFailed => UiLocalization.Get(nameof(DatabaseText), nameof(WriteFailed));
    internal static string NewerSchema => UiLocalization.Get(nameof(DatabaseText), nameof(NewerSchema));
    internal static string MigrationOrder => UiLocalization.Get(nameof(DatabaseText), nameof(MigrationOrder));
    internal static string LegacyFailedFormat => UiLocalization.Get(nameof(DatabaseText), nameof(LegacyFailedFormat));
    internal static string SkippedFormat => UiLocalization.Get(nameof(DatabaseText), nameof(SkippedFormat));
}
