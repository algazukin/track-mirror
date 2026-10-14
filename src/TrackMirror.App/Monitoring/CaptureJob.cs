// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Monitoring;

internal sealed record CaptureJob(
    string Path,
    string Account,
    string FileName,
    DateTime Time,
    bool Notify,
    FileStream? Input
);
