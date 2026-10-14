// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Storage.Management;

internal sealed record DataOperationProgress(string Stage, long CompletedBytes = 0, long TotalBytes = 0);
