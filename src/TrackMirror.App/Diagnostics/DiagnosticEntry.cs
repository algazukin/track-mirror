// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

namespace TrackMirror.Diagnostics;

internal enum DiagnosticComponent { Application, Storage, Screenshots, Activity, Countdown, AI, Runtime, Installation, TimerControl, Updates, DataOperations, UI, Diagnostics }
internal enum DiagnosticLevel { Information, Warning, Error, Fatal }
internal enum DiagnosticEvent
{
    Started, Stopped, OperationStarted, OperationCompleted, OperationCancelled, OperationFailed,
    SettingsLoaded, SettingsSaved, SettingsRejected, StateChanged, Retry, RequestCompleted,
    UnhandledUI, UnhandledBackground, UnobservedTask, ExportStarted, ExportCompleted, ExportFailed
}

internal sealed record DiagnosticMetrics
{
    public long? Count { get; init; }
    public long? Bytes { get; init; }
    public double? DurationMilliseconds { get; init; }
    public int? Code { get; init; }
    public long? PromptTokens { get; init; }
    public long? ReplyTokens { get; init; }
    public long? OcrBlocks { get; init; }
    public long? OcrCharacters { get; init; }

    internal bool IsValid => Count is null or >= 0 && Bytes is null or >= 0
        && PromptTokens is null or >= 0 && ReplyTokens is null or >= 0
        && OcrBlocks is null or >= 0 && OcrCharacters is null or >= 0
        && (DurationMilliseconds == null || double.IsFinite(DurationMilliseconds.Value) && DurationMilliseconds >= 0);
}

internal sealed record DiagnosticEntry
{
    public DateTimeOffset TimestampUtc { get; init; }
    public Guid SessionId { get; init; }
    public Guid ModuleId { get; init; }
    public string Version { get; init; } = string.Empty;
    public DiagnosticLevel Level { get; init; }
    public DiagnosticComponent Component { get; init; }
    public DiagnosticEvent Event { get; init; }
    public DiagnosticMetrics? Metrics { get; init; }
    public DiagnosticException? Exception { get; init; }
}

internal sealed record DiagnosticLogOptions
{
    internal long MaximumFileBytes { get; init; } = 2 * 1024 * 1024;
    internal int MaximumFiles { get; init; } = 5;
    internal int RetentionDays { get; init; } = 14;

    internal bool IsValid => MaximumFileBytes is >= 4096 and <= 16 * 1024 * 1024
        && MaximumFiles is >= 1 and <= 20 && RetentionDays is >= 1 and <= 90;
}
