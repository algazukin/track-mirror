// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy.Models;
using TrackMirror.Privacy.Installation;

namespace TrackMirror.Privacy;

internal static class PrivacyCoverage
{
    internal static List<PrivacyScanCategory> Selected(AppSettings settings)
    {
        var selected = new List<PrivacyScanCategory>();

        if (settings.PrivacyGames)
        {
            selected.Add(PrivacyScanCategory.Game);
        }

        if (settings.PrivacyPersonalChats)
        {
            selected.Add(PrivacyScanCategory.PersonalChat);
        }

        if (settings.PrivacyConfidentialData)
        {
            selected.Add(PrivacyScanCategory.ConfidentialData);
        }

        return selected;
    }

    internal static PrivacyScanCategory Category(PrivacyFinding finding) => finding.Category switch
    {
        PrivacyRules.Game => PrivacyScanCategory.Game,
        PrivacyRules.PersonalChat => PrivacyScanCategory.PersonalChat,
        _ => PrivacyScanCategory.ConfidentialData
    };

    internal static string Version(PrivacyScanCategory category, string model) =>
        PrivacyModelCatalog.Version(model) + (category == PrivacyScanCategory.ConfidentialData
            ? PrivacyNames.ConfidentialVersion : string.Empty);

    internal static bool Complete(PrivacyScanRecord record, PrivacyScanCategory category,
        string? version = null) =>
        record.ImageHash.Length > 0 && record.Categories.Any(result => result.Category == category
            && result.Version == (version ?? Version(category, PrivacyNames.Model))
            && result.State == PrivacyScanState.Completed);

    internal static PrivacyScanRecord Project(PrivacyScanRecord record, AppSettings settings)
    {
        var selected = Selected(settings);

        var current = record.Categories.Where(result =>
            result.Version == Version(result.Category, settings.PrivacyModel)
            && record.ImageHash.Length > 0).ToList();

        var missing = selected.Any(category => !Complete(record, category, Version(category, settings.PrivacyModel)));

        missing |= current.Any(result => result.State != PrivacyScanState.Completed);

        var state = record.State;

        var jobFailure = state is PrivacyScanState.Failed or PrivacyScanState.Cancelled
            && record.FailureCode is PrivacyNames.MissingCopy or PrivacyNames.StorageFailure
                or PrivacyNames.Interrupted or PrivacyNames.Cancelled;

        if (!jobFailure && state is not (PrivacyScanState.Queued or PrivacyScanState.Scanning))
        {
            state = !missing ? PrivacyScanState.Completed
                : current.Count > 0 ? PrivacyScanState.Partial : record.State;
        }

        return record with
        {
            State = state,
            Findings = current.SelectMany(result => result.Findings).ToList(),
            NeedsCheck = missing,
            FailureCode = jobFailure ? record.FailureCode
                : current.FirstOrDefault(result => result.FailureCode.Length > 0)?.FailureCode
                    ?? (missing ? record.FailureCode : string.Empty)
        };
    }

    internal static PrivacyScanRecord ImportLegacy(PrivacyScanRecord record)
    {
        if (record.Categories.Count > 0)
            return record;

        for (var mask = 0; mask < 8; mask++)
        {
            var settings = new AppSettings
            {
                PrivacyGames = (mask & 1) != 0,
                PrivacyPersonalChats = (mask & 2) != 0,
                PrivacyConfidentialData = (mask & 4) != 0
            };

            if (record.Profile != PrivacyScanService.Profile(settings))
                continue;

            var selected = Selected(settings);

            return record with
            {
                RequestedCategories = selected,
                Categories = selected.Select(category => new PrivacyCategoryResult
                {
                    Category = category,
                    Version = PrivacyNames.Version,
                    State = record.State == PrivacyScanState.Completed && record.ImageHash.Length > 0
                        ? PrivacyScanState.Completed : PrivacyScanState.Partial,
                    Findings = record.Findings.Where(finding => Category(finding) == category).ToList(),
                    FailureCode = record.FailureCode,
                    UpdatedUtc = record.UpdatedUtc
                }).ToList()
            };
        }

        return record;
    }
}
