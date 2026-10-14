// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Runtime.ExceptionServices;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class StaThread
{
    internal static void Run(Action action)
    {
        ExceptionDispatchInfo? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

                action();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        }) { IsBackground = true };

        thread.SetApartmentState(ApartmentState.STA);

        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(15)))
            throw new TimeoutException("The isolated UI scenario did not finish.");

        failure?.Throw();
    }
}
