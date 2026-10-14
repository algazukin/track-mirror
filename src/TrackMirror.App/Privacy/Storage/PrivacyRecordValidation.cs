// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Storage;

internal static class PrivacyRecordValidation
{
    internal static bool Valid(PrivacyScanRecord? entry) => entry != null
        && !string.IsNullOrEmpty(entry.CaptureId) && Enum.IsDefined(entry.State) && Enum.IsDefined(entry.Disposition)
        && entry.Findings != null && entry.Findings.Count <= 400 && entry.RequestId != null && entry.Profile != null
        && entry.ImageHash != null && entry.FailureCode != null && entry.Categories != null
        && entry.RequestedCategories != null && entry.Findings.All(finding => finding != null && ValidFinding(finding))
        && entry.Categories.Count <= 3 && entry.RequestedCategories.Count <= 3
        && entry.Categories.All(result => result != null && Enum.IsDefined(result.Category)
            && Enum.IsDefined(result.State)
            && result.Version != null && result.FailureCode != null && result.Findings != null
            && result.Diagnostics is { Count: <= 256 } && result.Diagnostics.All(ValidDiagnostic)
            && result.Findings.Count <= 400 && result.Findings.All(finding => finding != null && ValidFinding(finding)
                && PrivacyCoverage.Category(finding) == result.Category))
        && entry.Categories.DistinctBy(result => result.Category).Count() == entry.Categories.Count
        && entry.Categories.Sum(result => result.Findings.Count) <= 400
        && entry.RequestedCategories.All(category => Enum.IsDefined(category))
        && entry.RequestedCategories.Distinct().Count() == entry.RequestedCategories.Count;

    private static bool ValidDiagnostic(PrivacyRequestDiagnostic? diagnostic) => diagnostic != null
        && diagnostic.RequestId != Guid.Empty && diagnostic.RequestBytes >= 0 && diagnostic.OcrBlocks >= 0
        && diagnostic.OcrCharacters >= 0
        && diagnostic.PromptTokens is null or >= 0 && diagnostic.ReplyTokens is null or >= 0
        && double.IsFinite(diagnostic.DurationMilliseconds) && diagnostic.DurationMilliseconds >= 0
        && diagnostic.FailureCode is "" or PrivacyNames.ModelContext or PrivacyNames.ModelTimeout
            or PrivacyNames.ModelConnection or PrivacyNames.ModelMemory or PrivacyNames.ModelServer
            or PrivacyNames.ModelRequest or PrivacyNames.ModelUnavailable or PrivacyNames.InvalidResponse
            or PrivacyNames.InvalidEvidence or PrivacyNames.TruncatedResponse or PrivacyNames.Cancelled
            or PrivacyNames.ModelUncertain;

    private static bool ValidFinding(PrivacyFinding finding) =>
        finding.Category is PrivacyRules.Credentials or PrivacyRules.PersonalData or PrivacyRules.SensitiveDocument
            or PrivacyRules.Game or PrivacyRules.PersonalChat
        && finding.Severity is PrivacyRules.High or PrivacyRules.Medium
        && finding.Reason is PrivacyRules.TokenReason or PrivacyRules.PasswordReason or PrivacyRules.PrivateKeyReason
            or PrivacyRules.ContextReason
        && finding.Regions is { Count: <= 12 }
        && finding.Regions.All(region => region != null && float.IsFinite(region.X) && float.IsFinite(region.Y)
            && float.IsFinite(region.Width) && float.IsFinite(region.Height)
            && region.X >= 0 && region.Y >= 0 && region.Width > 0 && region.Height > 0
            && region.X + region.Width <= 1.001f && region.Y + region.Height <= 1.001f);
}
