// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.IO.Pipes;
using TrackMirror.App;
using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using TrackMirror.IntegrationTests.Infrastructure;
using Xunit;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class HubstaffCliProcessTests
{
    [Fact]
    public async Task Ordinary_child_launch_waits_until_temporary_inheritable_handles_are_closed()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, "hold");
        using var release = new ManualResetEventSlim();
        var ready = new TaskCompletionSource<AnonymousPipeServerStream>(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = Task.Run(() =>
        {
            lock (ChildProcessLaunch.Gate)
            {
                var pipe = new AnonymousPipeServerStream(PipeDirection.In, HandleInheritability.Inheritable);
                ready.TrySetResult(pipe);

                if (!release.Wait(TimeSpan.FromSeconds(5)))
                {
                    throw new TimeoutException("The synthetic handle window was not released.");
                }

                pipe.DisposeLocalCopyOfClientHandle();
            }
        });
        using var pipe = await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var launching = Task.Run(() =>
        {
            attempting.TrySetResult();
            return ChildProcessLaunch.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            })!;
        });
        Process? process = null;
        Task<int>? read = null;

        try
        {
            // Act
            await attempting.Task;
            await Task.Delay(100);
            release.Set();
            await holder;
            process = await launching;
            read = pipe.ReadAsync(new byte[1]).AsTask();

            // Assert
            Assert.Equal(0, await read.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            release.Set();
            process ??= await launching;

            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync();
            }

            if (read != null)
            {
                await read;
            }

            process.Dispose();
            await holder;
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task GUI_cli_receives_no_feedback_flags_and_preserves_UTF8_status()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, "status");

        // Act
        var status = await new HubstaffCli().StatusAsync(path, default);

        // Assert
        Assert.True(status.Tracking);
        Assert.Equal("проект-demo", status.ProjectId);
        Assert.Equal("7", status.TaskId);
    }

    [Fact]
    public async Task Large_stderr_does_not_block_stdout_or_control_acknowledgements()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, "stderr");
        var cli = new HubstaffCli();

        // Act
        var status = await cli.StatusAsync(path, default).WaitAsync(TimeSpan.FromSeconds(8));
        await cli.ExecuteAsync(path, "stop", default).WaitAsync(TimeSpan.FromSeconds(8));
        await cli.ExecuteAsync(path, "resume", default).WaitAsync(TimeSpan.FromSeconds(8));

        // Assert
        Assert.True(status.Tracking);
    }

    [Theory]
    [InlineData("nonzero", HubstaffControlNames.CommandFailed)]
    [InlineData("error", HubstaffControlNames.CommandFailed)]
    [InlineData("invalid", HubstaffControlNames.InvalidStatus)]
    [InlineData("large", HubstaffControlNames.InvalidStatus)]
    public async Task Process_failures_and_oversized_output_remain_bounded(string mode, string expected)
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, mode);

        // Act
        var error = await Assert.ThrowsAsync<HubstaffControlException>(() =>
            new HubstaffCli().StatusAsync(path, default).WaitAsync(TimeSpan.FromSeconds(8)));

        // Assert
        Assert.Equal(expected, error.Code);
    }

    [Fact]
    public async Task Cancellation_terminates_the_synthetic_child_and_releases_its_files()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, "hang");
        var marker = Path.Combine(Path.GetDirectoryName(path)!, "pid.txt");
        using var cancellation = new CancellationTokenSource();
        var request = new HubstaffCli().StatusAsync(path, cancellation.Token);
        using var startupTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        while (!File.Exists(marker))
        {
            await Task.Delay(10, startupTimeout.Token);
        }

        var pid = int.Parse(await File.ReadAllTextAsync(marker));

        // Act
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request.WaitAsync(TimeSpan.FromSeconds(8)));
        var alive = false;

        foreach (var child in Process.GetProcessesByName("TrackMirror.CliFixture"))
        {
            using (child)
            {
                alive |= child.Id == pid && !child.HasExited;
            }
        }

        // Assert
        Assert.False(alive);
    }

    [Fact]
    public async Task Timeout_terminates_a_hung_synthetic_child()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, "hang");

        // Act
        var error = await Assert.ThrowsAsync<HubstaffControlException>(() =>
            new HubstaffCli().StatusAsync(path, default).WaitAsync(TimeSpan.FromSeconds(22)));

        // Assert
        Assert.Equal(HubstaffControlNames.TimedOut, error.Code);
    }

    [Fact]
    public async Task Concurrent_launches_do_not_inherit_other_requests_pipe_handles()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var path = CreateFixture(workspace, "status");

        // Act
        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => new HubstaffCli().StatusAsync(path, default))).WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.Equal(8, results.Length);
        Assert.All(results, status => Assert.True(status.Tracking));
    }

    private static string CreateFixture(TestWorkspace workspace, string mode)
    {
        var directory = Path.Combine(workspace.Root, "synthetic CLI путь");
        Directory.CreateDirectory(directory);

        foreach (var source in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "cli-fixture")))
        {
            File.Copy(source, Path.Combine(directory, Path.GetFileName(source)));
        }

        File.WriteAllText(Path.Combine(directory, "behavior.txt"), mode);
        return Path.Combine(directory, "TrackMirror.CliFixture.exe");
    }
}
