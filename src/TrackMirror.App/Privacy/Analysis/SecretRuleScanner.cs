// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.RegularExpressions;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Analysis;

internal static class SecretRuleScanner
{
    internal static List<PrivacyFinding> Scan(IReadOnlyList<OcrBlock> blocks)
    {
        var result = new List<PrivacyFinding>();

        foreach (var block in blocks)
        {
            var evidence = new List<OcrBlock> { block };

            // OCR may split a password label and its value or a long line at a tile boundary.
            var neighbors = blocks.Where(other => other.Id != block.Id
                && Math.Abs(other.Region.Y - block.Region.Y) < Math.Max(block.Region.Height, 0.015f)
                && other.Region.X >= block.Region.X + block.Region.Width - 0.02f
                && other.Region.X < block.Region.X + block.Region.Width + 0.08f).OrderBy(other => other.Region.X);

            evidence.AddRange(neighbors.Take(2));

            var text = string.Join(' ', evidence.Select(item => item.Text));

            string? reason = null;

            List<PrivacyRegion>? matchedRegions = null;

            foreach (var pattern in new[] { PrivacyRules.TokenPattern, PrivacyRules.BearerPattern,
                PrivacyRules.PasswordPattern })
            {
                var match = Regex.Match(text, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

                if (match.Success && !Regex.IsMatch(match.Value, PrivacyRules.PlaceholderPattern,
                    RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                {
                    reason = pattern == PrivacyRules.PasswordPattern
                        ? PrivacyRules.PasswordReason : PrivacyRules.TokenReason;

                    matchedRegions = [];

                    var offset = 0;

                    foreach (var item in evidence)
                    {
                        if (offset < match.Index + match.Length && offset + item.Text.Length > match.Index)
                        {
                            matchedRegions.Add(item.Region);
                        }

                        offset += item.Text.Length + 1;
                    }

                    break;
                }
            }

            if (reason == null && evidence.Count == 1 && Regex.IsMatch(block.Text, PrivacyRules.PasswordLabelPattern,
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                var value = blocks.Where(other => other.Id != block.Id
                    && other.Region.Y > block.Region.Y
                    && other.Region.Y - block.Region.Y < 0.06f
                    && Math.Abs(other.Region.X - block.Region.X) < 0.04f)
                    .OrderBy(other => other.Region.Y).FirstOrDefault();

                if (value != null && Regex.IsMatch(value.Text, PrivacyRules.RevealedValuePattern,
                    RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
                    && !Regex.IsMatch(value.Text, PrivacyRules.PlaceholderPattern,
                        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
                {
                    evidence.Add(value);

                    reason = PrivacyRules.PasswordReason;
                }
            }

            if (reason == null && Regex.IsMatch(text, PrivacyRules.PrivateKeyPattern,
                RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                var material = blocks.FirstOrDefault(other => Math.Abs(other.Region.Y - block.Region.Y) < 0.2f
                    && Regex.IsMatch(other.Text, PrivacyRules.Base64Pattern, RegexOptions.CultureInvariant,
                        TimeSpan.FromMilliseconds(100)));

                if (material != null)
                {
                    evidence.Add(material);

                    reason = PrivacyRules.PrivateKeyReason;
                }
            }

            if (reason != null)
            {
                var regions = matchedRegions ?? evidence.Select(item => item.Region).Distinct().ToList();

                if (!result.Any(finding => finding.Reason == reason && finding.Regions.SequenceEqual(regions)))
                {
                    result.Add(new PrivacyFinding(PrivacyRules.Credentials, PrivacyRules.High, reason, regions));
                }
            }
        }

        return result;
    }
}
