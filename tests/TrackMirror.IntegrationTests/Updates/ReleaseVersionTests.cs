// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Updates;
using Xunit;

namespace TrackMirror.IntegrationTests.Updates;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("v1.0.0-preview.2", "1.0.0-preview.10", -1)]
    [InlineData("1.0.0-preview.10", "1.0.0", -1)]
    [InlineData("1.0.0", "1.1.0-preview.1", -1)]
    [InlineData("2.0.0", "1.99.99", 1)]
    [InlineData("1.0.0-alpha", "1.0.0-alpha.1", -1)]
    [InlineData("1.0.0-alpha.1", "1.0.0-alpha.beta", -1)]
    [InlineData("1.0.0+build.1", "v1.0.0+build.2", 0)]
    [InlineData("1.0.0-preview.99999999999999999999", "1.0.0-preview.100000000000000000000", -1)]
    public void Published_versions_follow_semantic_version_ordering(string first, string second, int expected)
    {
        // Arrange
        var parsedFirst = ReleaseVersion.TryParse(first, out var left);
        var parsedSecond = ReleaseVersion.TryParse(second, out var right);

        // Act
        var comparison = left?.CompareTo(right);

        // Assert
        Assert.True(parsedFirst);
        Assert.True(parsedSecond);
        Assert.NotNull(comparison);
        Assert.Equal(expected, Math.Sign(comparison.Value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" 1.0.0")]
    [InlineData("1.0.0 ")]
    [InlineData("1.0.0\n")]
    [InlineData("1.0")]
    [InlineData("1.0.0.0")]
    [InlineData("01.0.0")]
    [InlineData("1.0.0-preview.01")]
    [InlineData("latest")]
    public void Invalid_or_ambiguous_release_tags_are_not_used(string? value)
    {
        // Arrange
        ReleaseVersion? version;

        // Act
        var valid = ReleaseVersion.TryParse(value, out version);

        // Assert
        Assert.False(valid);
        Assert.Null(version);
    }
}
