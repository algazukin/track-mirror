// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace TrackMirror.Updates;

internal sealed class ReleaseVersion : IComparable<ReleaseVersion>
{
    private static readonly Regex Pattern = new(
        @"^v?(?<core>(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*))(?:-(?<pre>[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private readonly Version _numbers;
    private readonly string[] _preRelease;

    internal string Text { get; }

    internal bool IsPreRelease => _preRelease.Length > 0;

    private ReleaseVersion(Version numbers, string[] preRelease, string text)
    {
        _numbers = numbers;
        _preRelease = preRelease;
        Text = text;
    }

    internal static bool TryParse(string? value, [NotNullWhen(true)] out ReleaseVersion? result)
    {
        result = null;

        if (value is null || value.Length > 200)
            return false;

        var match = Pattern.Match(value);

        if (!match.Success || !Version.TryParse(match.Groups["core"].Value, out var numbers))
            return false;

        var pre = match.Groups["pre"].Value;
        var parts = pre.Length == 0 ? [] : pre.Split('.');

        if (parts.Any(part => part.Length > 1 && part[0] == '0' && part.All(char.IsAsciiDigit)))
            return false;

        result = new ReleaseVersion(numbers, parts, match.Groups["core"].Value + (pre.Length == 0 ? "" : "-" + pre));

        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
            return 1;

        var comparison = _numbers.CompareTo(other._numbers);

        if (comparison != 0)
            return comparison;

        if (!IsPreRelease)
            return other.IsPreRelease ? 1 : 0;

        if (!other.IsPreRelease)
            return -1;

        for (var index = 0; index < Math.Min(_preRelease.Length, other._preRelease.Length); index++)
        {
            var left = _preRelease[index];
            var right = other._preRelease[index];
            var leftNumeric = left.All(char.IsAsciiDigit);
            var rightNumeric = right.All(char.IsAsciiDigit);

            if (leftNumeric && rightNumeric)
            {
                comparison = left.Length.CompareTo(right.Length);

                if (comparison == 0)
                    comparison = string.CompareOrdinal(left, right);
            }
            else
            {
                comparison = leftNumeric != rightNumeric
                    ? (leftNumeric ? -1 : 1) : string.CompareOrdinal(left, right);
            }

            if (comparison != 0)
                return comparison;
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }
}
