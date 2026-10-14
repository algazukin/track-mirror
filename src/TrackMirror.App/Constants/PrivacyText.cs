// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Localization;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Constants;

internal static class PrivacyText
{
    internal static string Heading => UiLocalization.Get(nameof(PrivacyText), nameof(Heading));
    internal static string Hint => UiLocalization.Get(nameof(PrivacyText), nameof(Hint));
    internal static string Limitations => UiLocalization.Get(nameof(PrivacyText), nameof(Limitations));
    internal static string AutoScan => UiLocalization.Get(nameof(PrivacyText), nameof(AutoScan));
    internal static string SearchGames => UiLocalization.Get(nameof(PrivacyText), nameof(SearchGames));
    internal static string SearchPersonalChats => UiLocalization.Get(nameof(PrivacyText), nameof(SearchPersonalChats));
    internal static string SearchConfidentialData =>
        UiLocalization.Get(nameof(PrivacyText), nameof(SearchConfidentialData));
    internal static string NoCategories => UiLocalization.Get(nameof(PrivacyText), nameof(NoCategories));
    internal static string Readiness => UiLocalization.Get(nameof(PrivacyText), nameof(Readiness));
    internal static string MissingRuntime => UiLocalization.Get(nameof(PrivacyText), nameof(MissingRuntime));
    internal static string ScanSelected => UiLocalization.Get(nameof(PrivacyText), nameof(ScanSelected));
    internal static string RescanSelected => UiLocalization.Get(nameof(PrivacyText), nameof(RescanSelected));
    internal static string ResultsAction => UiLocalization.Get(nameof(PrivacyText), nameof(ResultsAction));
    internal static string ScanRange => UiLocalization.Get(nameof(PrivacyText), nameof(ScanRange));
    internal static string Cancel => UiLocalization.Get(nameof(PrivacyText), nameof(Cancel));
    internal static string RiskFilter => UiLocalization.Get(nameof(PrivacyText), nameof(RiskFilter));
    internal static string NoRiskMatches => UiLocalization.Get(nameof(PrivacyText), nameof(NoRiskMatches));
    internal static string NoRiskMatchesHint => UiLocalization.Get(nameof(PrivacyText), nameof(NoRiskMatchesHint));
    internal static string Unscanned => UiLocalization.Get(nameof(PrivacyText), nameof(Unscanned));
    internal static string QueueFormat => UiLocalization.Get(nameof(PrivacyText), nameof(QueueFormat));
    internal static string FoundFormat => UiLocalization.Get(nameof(PrivacyText), nameof(FoundFormat));
    internal static string NoFindings => UiLocalization.Get(nameof(PrivacyText), nameof(NoFindings));
    internal static string PartialHint => UiLocalization.Get(nameof(PrivacyText), nameof(PartialHint));
    internal static string Acknowledge => UiLocalization.Get(nameof(PrivacyText), nameof(Acknowledge));
    internal static string FalsePositive => UiLocalization.Get(nameof(PrivacyText), nameof(FalsePositive));
    internal static string ResetDisposition => UiLocalization.Get(nameof(PrivacyText), nameof(ResetDisposition));
    internal static string Acknowledged => UiLocalization.Get(nameof(PrivacyText), nameof(Acknowledged));
    internal static string Dismissed => UiLocalization.Get(nameof(PrivacyText), nameof(Dismissed));
    internal static string StorageError => UiLocalization.Get(nameof(PrivacyText), nameof(StorageError));
    internal static string RecoveryError => UiLocalization.Get(nameof(PrivacyText), nameof(RecoveryError));
    internal static string LoadUnavailable => UiLocalization.Get(nameof(PrivacyText), nameof(LoadUnavailable));
    internal static string Cancelled => UiLocalization.Get(nameof(PrivacyText), nameof(Cancelled));
    internal static string Queued => UiLocalization.Get(nameof(PrivacyText), nameof(Queued));
    internal static string Scanning => UiLocalization.Get(nameof(PrivacyText), nameof(Scanning));
    internal static string Completed => UiLocalization.Get(nameof(PrivacyText), nameof(Completed));
    internal static string Partial => UiLocalization.Get(nameof(PrivacyText), nameof(Partial));
    internal static string Failed => UiLocalization.Get(nameof(PrivacyText), nameof(Failed));
    internal static string Risk => UiLocalization.Get(nameof(PrivacyText), nameof(Risk));
    internal static string Stale => UiLocalization.Get(nameof(PrivacyText), nameof(Stale));
    internal static string StaleBadge => UiLocalization.Get(nameof(PrivacyText), nameof(StaleBadge));
    internal static string NotChecked => UiLocalization.Get(nameof(PrivacyText), nameof(NotChecked));
    internal static string Disabled => UiLocalization.Get(nameof(PrivacyText), nameof(Disabled));
    internal static string Outdated => UiLocalization.Get(nameof(PrivacyText), nameof(Outdated));
    internal static string ConfidentialData => UiLocalization.Get(nameof(PrivacyText), nameof(ConfidentialData));
    internal static string Credentials => UiLocalization.Get(nameof(PrivacyText), nameof(Credentials));
    internal static string PersonalData => UiLocalization.Get(nameof(PrivacyText), nameof(PersonalData));
    internal static string SensitiveDocument => UiLocalization.Get(nameof(PrivacyText), nameof(SensitiveDocument));
    internal static string Game => UiLocalization.Get(nameof(PrivacyText), nameof(Game));
    internal static string PersonalChat => UiLocalization.Get(nameof(PrivacyText), nameof(PersonalChat));
    internal static string Token => UiLocalization.Get(nameof(PrivacyText), nameof(Token));
    internal static string Password => UiLocalization.Get(nameof(PrivacyText), nameof(Password));
    internal static string PrivateKey => UiLocalization.Get(nameof(PrivacyText), nameof(PrivateKey));
    internal static string Context => UiLocalization.Get(nameof(PrivacyText), nameof(Context));
    internal const string DetailSeparator = " · ";
    internal const string FindingSeparator = "\n";
    internal static string RuntimeUnavailable => UiLocalization.Get(nameof(PrivacyText), nameof(RuntimeUnavailable));
    internal static string RuntimePortUnavailable =>
        UiLocalization.Get(nameof(PrivacyText), nameof(RuntimePortUnavailable));
    internal static string RuntimeStopped => UiLocalization.Get(nameof(PrivacyText), nameof(RuntimeStopped));
    internal static string ModelUnavailable => UiLocalization.Get(nameof(PrivacyText), nameof(ModelUnavailable));
    internal static string ModelConnection => UiLocalization.Get(nameof(PrivacyText), nameof(ModelConnection));
    internal static string ModelTimeout => UiLocalization.Get(nameof(PrivacyText), nameof(ModelTimeout));
    internal static string ModelMemory => UiLocalization.Get(nameof(PrivacyText), nameof(ModelMemory));
    internal static string ModelServer => UiLocalization.Get(nameof(PrivacyText), nameof(ModelServer));
    internal static string ModelRequest => UiLocalization.Get(nameof(PrivacyText), nameof(ModelRequest));
    internal static string ModelContext => UiLocalization.Get(nameof(PrivacyText), nameof(ModelContext));
    internal static string InvalidResponse => UiLocalization.Get(nameof(PrivacyText), nameof(InvalidResponse));
    internal static string InvalidEvidence => UiLocalization.Get(nameof(PrivacyText), nameof(InvalidEvidence));
    internal static string TruncatedResponse => UiLocalization.Get(nameof(PrivacyText), nameof(TruncatedResponse));
    internal static string OcrIncomplete => UiLocalization.Get(nameof(PrivacyText), nameof(OcrIncomplete));
    internal static string ModelUncertain => UiLocalization.Get(nameof(PrivacyText), nameof(ModelUncertain));
    internal static string AnalysisLimit => UiLocalization.Get(nameof(PrivacyText), nameof(AnalysisLimit));
    internal static string DiagnosticsFormat => UiLocalization.Get(nameof(PrivacyText), nameof(DiagnosticsFormat));
    internal static string MetricUnavailable => UiLocalization.Get(nameof(PrivacyText), nameof(MetricUnavailable));
    internal static string Incomplete => UiLocalization.Get(nameof(PrivacyText), nameof(Incomplete));
    internal static string CopyUnavailable => UiLocalization.Get(nameof(PrivacyText), nameof(CopyUnavailable));
    internal static string OcrFailed => UiLocalization.Get(nameof(PrivacyText), nameof(OcrFailed));
    internal static string LegacyFailure => UiLocalization.Get(nameof(PrivacyText), nameof(LegacyFailure));
    internal static string VisionFailed => UiLocalization.Get(nameof(PrivacyText), nameof(VisionFailed));

    internal static string State(PrivacyScanRecord? result) => result?.State switch
    {
        PrivacyScanState.Queued => Queued,
        PrivacyScanState.Scanning => Scanning,
        PrivacyScanState.Partial => Partial,
        PrivacyScanState.Failed => Failed,
        PrivacyScanState.Cancelled => Cancelled,
        PrivacyScanState.Completed => result.Findings.Count > 0 ? Risk : Completed,
        _ => Unscanned
    };

    internal static string Category(string value) => value switch
    {
        PrivacyRules.Credentials => Credentials,
        PrivacyRules.PersonalData => PersonalData,
        PrivacyRules.SensitiveDocument => SensitiveDocument,
        PrivacyRules.Game => Game,
        PrivacyRules.PersonalChat => PersonalChat,
        _ => Risk
    };

    internal static string Category(PrivacyScanCategory value) => value switch
    {
        PrivacyScanCategory.Game => Game,
        PrivacyScanCategory.PersonalChat => PersonalChat,
        _ => ConfidentialData
    };

    internal static string Coverage(PrivacyCategoryResult? result, string version = PrivacyNames.Version) =>
        result == null ? NotChecked : result.Version != version ? Outdated
        : result.State == PrivacyScanState.Completed ? Completed : Partial;

    internal static string Reason(string value) => value switch
    {
        PrivacyRules.TokenReason => Token,
        PrivacyRules.PasswordReason => Password,
        PrivacyRules.PrivateKeyReason => PrivateKey,
        _ => Context
    };

    internal static string Failure(string? code) => code switch
    {
        PrivacyNames.RuntimeUnavailable => RuntimeUnavailable,
        PrivacyNames.RuntimePortUnavailable => RuntimePortUnavailable,
        PrivacyNames.RuntimeStopped => RuntimeStopped,
        PrivacyNames.ModelUnavailable => ModelUnavailable,
        PrivacyNames.ModelConnection => ModelConnection,
        PrivacyNames.ModelTimeout => ModelTimeout,
        PrivacyNames.ModelMemory => ModelMemory,
        PrivacyNames.ModelServer => ModelServer,
        PrivacyNames.ModelRequest => ModelRequest,
        PrivacyNames.ModelContext => ModelContext,
        PrivacyNames.InvalidResponse => InvalidResponse,
        PrivacyNames.InvalidEvidence => InvalidEvidence,
        PrivacyNames.TruncatedResponse => TruncatedResponse,
        PrivacyNames.OcrIncomplete => OcrIncomplete,
        PrivacyNames.ModelUncertain => ModelUncertain,
        PrivacyNames.AnalysisLimit => AnalysisLimit,
        PrivacyNames.Incomplete => Incomplete,
        PrivacyNames.MissingCopy => CopyUnavailable,
        PrivacyNames.OcrFailure => OcrFailed,
        PrivacyNames.VisionFailure => VisionFailed,
        PrivacyNames.StorageFailure => StorageError,
        PrivacyNames.Interrupted => Cancelled,
        PrivacyNames.Cancelled => Cancelled,
        null or "" => string.Empty,
        _ => LegacyFailure
    };
}
