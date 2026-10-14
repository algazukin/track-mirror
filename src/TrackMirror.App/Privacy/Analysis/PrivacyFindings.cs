// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Analysis;

internal static class PrivacyFindings
{
    internal static List<PrivacyFinding> Merge(IEnumerable<PrivacyFinding> findings)
    {
        var result = new List<PrivacyFinding>();

        foreach (var finding in findings)
        {
            var regions = finding.Regions.Distinct().OrderBy(region => region.Y).ThenBy(region => region.X)
                .ThenBy(region => region.Width).ThenBy(region => region.Height).ToList();

            var previous = result.FindIndex(value => value.Category == finding.Category
                && value.Reason == finding.Reason && value.Regions.SequenceEqual(regions));

            if (previous < 0)
            {
                result.Add(finding with { Regions = regions });
            }
            else if (finding.Severity == PrivacyRules.High)
            {
                result[previous] = result[previous] with { Severity = PrivacyRules.High };
            }
        }

        return result;
    }
}
