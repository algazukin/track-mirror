// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Reflection;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class UiControls
{
    internal static T Field<T>(object owner, string name) => (T)owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;

    internal static object? Invoke(object owner, string name, params object[] arguments) => owner.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(owner, arguments);

    internal static IEnumerable<T> Descendants<T>(Control root) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is T matching)
            {
                yield return matching;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    internal static Task NextTurn(Control control)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        control.BeginInvoke(new Action(() => completion.SetResult()));

        return completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
