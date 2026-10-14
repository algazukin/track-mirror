// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using TrackMirror.Constants;
using System.Security.Cryptography;
using TrackMirror.Models;
using TrackMirror.Storage;
using Microsoft.Data.Sqlite;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Migrations;

public sealed class ImportInterruptionTests
{
    [Fact]
    public void Unreadable_source_preserves_JSON_and_retries_after_handle_is_released()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var capture = workspace.Capture();

        workspace.Json("index.json", new[] { capture });

        var path = Path.Combine(workspace.Root, "index.json");

        var original = File.ReadAllBytes(path);

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            // Act
            // Assert
            var error = Assert.Throws<IOException>(() => new ScreenshotStore(workspace.Root));

            Assert.Contains("index.json", error.Message);
        }

        // Act
        // Assert
        Assert.Equal(capture, Assert.Single(new ScreenshotStore(workspace.Root).Snapshot()));

        Assert.Equal(original, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Process_termination_during_import_rolls_back_and_next_start_imports_every_record()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        const int count = 100000;

        var captures = Enumerable.Range(
            0,
            count
        ).Select(
            index => new CaptureEntry { Id = "preserved-"
                + index, Account = "fixture", FileName = index
                + ".jpg", CapturedUtc = new DateTime(
                    2026,
                    10,
                    13,
                    10,
                    0,
                    0,
                    DateTimeKind.Utc
                ), SavedUtc = new DateTime(
                    2026,
                    10,
                    13,
                    10,
                    0,
                    1,
                    DateTimeKind.Utc
                ), Viewed = index % 2 == 0, Flagged = index % 3 == 0 }
        ).ToArray();

        workspace.Json("index.json", captures);

        var jsonPath = Path.Combine(workspace.Root, "index.json");

        var originalHash = SHA256.HashData(File.ReadAllBytes(jsonPath));

        var configuration = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        var testName = typeof(ImportInterruptionTests).Assembly.GetName().Name!;

        foreach (var argument in new[]
        {
            "exec",
            "--runtimeconfig",
            Path.Combine(AppContext.BaseDirectory, testName + ".runtimeconfig.json"),
            "--depsfile",
            Path.Combine(AppContext.BaseDirectory, testName + ".deps.json"),
            typeof(ScreenshotStore).Assembly.Location,
            "--data-root",
            workspace.Root,
            "--source-root",
            Path.Combine(workspace.Root, "empty-source"),
            "--background",
            "--no-previews"
        }

        )
        {
            configuration.ArgumentList.Add(argument);
        }

        // Act
        using var process = Process.Start(configuration)!;

        try
        {
            var interrupted = false;

            var deadline = DateTime.UtcNow.AddSeconds(30);

            var version = 0;

            while (!interrupted && !process.HasExited && DateTime.UtcNow < deadline)
            {
                await Task.Delay(10);

                if (!File.Exists(Path.Combine(workspace.Root, "viewer.db")))
                    continue;

                using var connection = new SqliteConnection(
                    new SqliteConnectionStringBuilder { DataSource = Path.Combine(
                        workspace.Root,
                        "viewer.db"
                    ), Pooling = false, DefaultTimeout = 1, Mode = SqliteOpenMode.ReadWrite }.ToString()
                );

                try
                {
                    connection.Open();

                    using var command = connection.CreateCommand();

                    command.CommandText = "PRAGMA user_version;";

                    version = Convert.ToInt32(command.ExecuteScalar());

                    if (version != DatabaseNames.CurrentVersion)
                        continue;
                    // Probe the native lock directly: provider retries can outlast a fast import transaction.

                    var status = SQLitePCL.raw.sqlite3_exec(connection.Handle, "BEGIN IMMEDIATE;");

                    if (status == SQLitePCL.raw.SQLITE_OK)
                    {
                        Assert.Equal(
                            SQLitePCL.raw.SQLITE_OK,
                            SQLitePCL.raw.sqlite3_exec(connection.Handle, "ROLLBACK;")
                        );
                    }
                    else if (status == SQLitePCL.raw.SQLITE_BUSY)
                    {
                        command.CommandText = "SELECT COUNT(*) FROM legacy_import;";

                        Assert.Equal(0L, command.ExecuteScalar());

                        process.Kill(entireProcessTree: true);

                        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));

                        interrupted = true;
                    }
                    else
                    {
                        Assert.Fail("Unexpected native lock probe result: " + status);
                    }
                }
                catch (SqliteException ex)when (ex.SqliteErrorCode == 5)
                {
                }
            }

            var logPath = Path.Combine(workspace.Root, "startup-error.log");

            var diagnostic = File.Exists(logPath) ? File.ReadAllText(logPath) : "No startup error was logged.";

            // Assert
            Assert.True(
                interrupted,
                "The test must terminate an active import transaction. Version: "
                    + version
                    + ". Files: "
                    + string.Join(", ", Directory.GetFiles(workspace.Root).Select(Path.GetFileName))
                    + ". "
                    + diagnostic
            );

            using var verification = new SqliteConnection(
                "Pooling=False;Data Source=" + Path.Combine(workspace.Root, "viewer.db")
            );

            verification.Open();

            using var query = verification.CreateCommand();

            query.CommandText = "SELECT (SELECT COUNT(*) FROM captures) + (SELECT COUNT(*) FROM legacy_import);";

            Assert.Equal(0L, query.ExecuteScalar());

            // Act
            // Assert
            Assert.Empty(new ScreenshotStore(workspace.Root).Snapshot());

            query.CommandText = "SELECT COUNT(*) FROM captures;";

            Assert.Equal((long)count, query.ExecuteScalar());

            query.CommandText = "SELECT COUNT(*) FROM legacy_import;";

            Assert.Equal(1L, query.ExecuteScalar());

            query.CommandText = "SELECT COUNT(*) FROM captures WHERE viewed = 1;";

            Assert.Equal((long)count / 2, query.ExecuteScalar());

            query.CommandText = "SELECT COUNT(*) FROM captures WHERE flagged = 1;";

            Assert.Equal(33334L, query.ExecuteScalar());

            query.CommandText = "SELECT id FROM captures WHERE file_name = '99999.jpg';";

            Assert.Equal("preserved-99999", query.ExecuteScalar());

            query.CommandText = "PRAGMA integrity_check;";

            Assert.Equal("ok", query.ExecuteScalar());

            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(jsonPath)));
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
