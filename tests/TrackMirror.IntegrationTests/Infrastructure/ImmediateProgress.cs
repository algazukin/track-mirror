// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Storage.Management;

namespace TrackMirror.IntegrationTests.Infrastructure;

internal sealed class ImmediateProgress(Action<DataOperationProgress> report) : IProgress<DataOperationProgress>
{
    public void Report(DataOperationProgress value) => report(value);
}
