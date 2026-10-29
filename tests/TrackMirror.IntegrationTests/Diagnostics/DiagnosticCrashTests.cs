// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.Text.Json;
using TrackMirror.IntegrationTests.Infrastructure;
using Xunit;

namespace TrackMirror.IntegrationTests.Diagnostics;

public sealed class DiagnosticCrashTests
{
    [Theory]
    [InlineData("diagnostics-ui", "UnhandledUI", false)]
    [InlineData("diagnostics-ui-shutdown", "UnhandledUI", false)]
    [InlineData("diagnostics-background", "UnhandledBackground", true)]
    [InlineData("diagnostics-task", "UnobservedTask", false)]
    public async Task Managed_failures_are_recorded_in_an_isolated_process_without_exception_messages(string scenario, string expected, bool terminating)
    {
        // Arrange
        using var workspace = new TestWorkspace();

        // Act
        var exitCode = await RunAsync(workspace.Root, scenario);
        var text = string.Join("\n", Directory.GetFiles(Path.Combine(workspace.Root, "diagnostics"), "*.jsonl").Select(File.ReadAllText));

        // Assert
        Assert.Equal(terminating, exitCode != 0);
        Assert.Contains(expected, text);
        Assert.DoesNotContain("private-crash-canary", text);
        Assert.DoesNotContain(workspace.Root, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Separate_processes_share_bounded_rotation_without_corrupting_records()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        // Act
        var exits = await Task.WhenAll(RunAsync(workspace.Root, "diagnostics-write"), RunAsync(workspace.Root, "diagnostics-write"));
        var files = Directory.GetFiles(Path.Combine(workspace.Root, "diagnostics"), "*.jsonl");

        // Assert
        Assert.All(exits, exit => Assert.Equal(0, exit));
        Assert.InRange(files.Length, 1, 3);
        Assert.All(files, file => Assert.InRange(new FileInfo(file).Length, 1, 4096));

        foreach (var line in files.SelectMany(File.ReadAllLines))
        {
            using var record = JsonDocument.Parse(line);
            Assert.Equal("Started", record.RootElement.GetProperty("Event").GetString());
        }
    }

    private static async Task<int> RunAsync(string root, string scenario)
    {
        var assembly = typeof(DiagnosticCrashTests).Assembly;
        var name = assembly.GetName().Name!;
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true, RedirectStandardError = true
        };

        foreach (var argument in new[]
        {
            "exec", "--runtimeconfig", Path.Combine(AppContext.BaseDirectory, name + ".runtimeconfig.json"),
            "--depsfile", Path.Combine(AppContext.BaseDirectory, name + ".deps.json"), assembly.Location,
            "--ui-scenario", scenario, root, "en", "native"
        })
            start.ArgumentList.Add(argument);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(25));
            await Task.WhenAll(stdout, stderr);
            return process.ExitCode;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }
        }
    }
}
