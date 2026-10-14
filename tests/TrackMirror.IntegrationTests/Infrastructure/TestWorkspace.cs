// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Models;

namespace TrackMirror.IntegrationTests.Infrastructure;

public sealed class TestWorkspace : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(),
        "TrackMirror.Tests",
        Guid.NewGuid().ToString("N")
    );

    public TestWorkspace()
    {
        Directory.CreateDirectory(Root);
    }

    public void Json<T>(string name, T value) => File.WriteAllText(
        Path.Combine(Root, name),
        JsonSerializer.Serialize(value)
    );
    public CaptureEntry Capture(string name = "2026-10-13T103000-0.jpg", bool viewed = false, bool flagged = true)
    {
        var directory = Path.Combine(Root, "captures", "fixture");

        Directory.CreateDirectory(directory);

        File.WriteAllBytes(Path.Combine(directory, name), [0xff, 0xd8, 0xff, 0xd9]);

        return new CaptureEntry
        {
            Id = "fixture/" + name,
            Account = "fixture",
            FileName = name,
            CapturedUtc = new DateTime(2026, 10, 13, 10, 30, 0, DateTimeKind.Utc),
            SavedUtc = new DateTime(2026, 10, 13, 10, 30, 1, DateTimeKind.Utc),
            Viewed = viewed,
            Flagged = flagged
        };
    }

    internal CaptureEntry ImageCapture(string name = "2026-10-13T103000-0.jpg",
        bool viewed = false, bool flagged = false)
    {
        var entry = Capture(name, viewed, flagged);

        File.WriteAllBytes(Path.Combine(Root, "captures", entry.Account, entry.FileName), TestImages.Jpeg());

        return entry;
    }

    public void Dispose()
    {
        var expected = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "TrackMirror.Tests")
        ) + Path.DirectorySeparatorChar;

        var target = Path.GetFullPath(Root);

        if (!target.StartsWith(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The test cleanup escaped its temporary workspace.");

        Directory.Delete(target, recursive: true);
    }
}
