// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using Xunit;

namespace TrackMirror.IntegrationTests.Infrastructure.Windows;

internal static class UiScenarioProcess
{
    internal static async Task RunAsync(string scenario, string language = "en", string dpi = "native")
    {
        using var workspace = new TestWorkspace();

        var assembly = typeof(UiScenarioProcess).Assembly;

        var name = assembly.GetName().Name!;

        var info = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        };

        foreach (var argument in new[]
        {
            "exec", "--runtimeconfig", Path.Combine(AppContext.BaseDirectory, name + ".runtimeconfig.json"),
            "--depsfile", Path.Combine(AppContext.BaseDirectory, name + ".deps.json"), assembly.Location,
            "--ui-scenario", scenario, workspace.Root, language, dpi
        })
        {
            info.ArgumentList.Add(argument);
        }

        using var process = Process.Start(info)!;

        var output = process.StandardOutput.ReadToEndAsync();

        var error = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.True(process.ExitCode == 0,
                $"UI scenario {scenario} failed ({process.ExitCode}).\n{await output}\n{await error}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);

                await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
    }
}
