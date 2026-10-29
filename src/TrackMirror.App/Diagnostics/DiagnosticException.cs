// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TrackMirror.Hubstaff;
using TrackMirror.Privacy.Models;
using TrackMirror.Constants;

namespace TrackMirror.Diagnostics;

internal enum DiagnosticExceptionKind
{
    Other, IO, AccessDenied, InvalidOperation, Argument, Cancelled, Timeout, HTTP, JSON, XML, SQLite, Windows, Privacy, TimerControl, Aggregate
}

internal enum DiagnosticFailure
{
    None, Unknown, ContextExceeded, ModelTimeout, ModelMemory, ModelServer, ModelRequest, ModelConnection,
    ModelUnavailable, RuntimeUnavailable, RuntimePortUnavailable, RuntimeStopped, InvalidResponse,
    TruncatedResponse, InvalidEvidence, OcrFailed, CopyUnavailable, StorageFailed,
    CliMissing, CliInvalidStatus, CliCommandFailed, CliTimeout, InputUnavailable, SelectionChanged,
    StopUnconfirmed, ResumeUnconfirmed
}

internal sealed record DiagnosticFrame
{
    public int Token { get; init; }
    public int IlOffset { get; init; }
    public string Method { get; init; } = string.Empty;
}

internal sealed record DiagnosticException
{
    public DiagnosticExceptionKind Kind { get; init; }
    public int HResult { get; init; }
    public int? NativeCode { get; init; }
    public bool Truncated { get; init; }
    public DiagnosticFailure Failure { get; init; }
    public List<DiagnosticFrame> Stack { get; init; } = [];
    public List<DiagnosticException> Inner { get; init; } = [];

    internal static DiagnosticException Capture(Exception exception)
    {
        var remaining = 8;
        var frames = 32;

        return Capture(exception, 0, ref remaining, ref frames);
    }

    private static DiagnosticException Capture(Exception error, int depth, ref int remaining, ref int frames)
    {
        remaining--;
        var stack = new List<DiagnosticFrame>();

        foreach (var frame in new StackTrace(error, false).GetFrames())
        {
            if (frames == 0)
                break;

            var method = frame.GetMethod();

            if (method?.Module != typeof(DiagnosticException).Module)
                continue;

            stack.Add(new DiagnosticFrame { Token = method.MetadataToken, IlOffset = frame.GetILOffset(), Method = MethodName(method.MetadataToken) });
            frames--;
        }

        var children = error is AggregateException aggregate ? aggregate.InnerExceptions.AsEnumerable()
            : error.InnerException is { } inner ? [inner] : Enumerable.Empty<Exception>();
        var captured = new List<DiagnosticException>();
        var truncated = frames == 0;

        foreach (var child in children)
        {
            if (remaining == 0 || depth == 3)
            {
                truncated = true;
                break;
            }

            captured.Add(Capture(child, depth + 1, ref remaining, ref frames));
        }

        return new DiagnosticException
        {
            Kind = error switch
            {
                AggregateException => DiagnosticExceptionKind.Aggregate,
                SqliteException => DiagnosticExceptionKind.SQLite,
                HubstaffControlException => DiagnosticExceptionKind.TimerControl,
                PrivacyScanException => DiagnosticExceptionKind.Privacy,
                UnauthorizedAccessException => DiagnosticExceptionKind.AccessDenied,
                IOException => DiagnosticExceptionKind.IO,
                OperationCanceledException => DiagnosticExceptionKind.Cancelled,
                TimeoutException => DiagnosticExceptionKind.Timeout,
                HttpRequestException => DiagnosticExceptionKind.HTTP,
                JsonException => DiagnosticExceptionKind.JSON,
                System.Xml.XmlException => DiagnosticExceptionKind.XML,
                Win32Exception => DiagnosticExceptionKind.Windows,
                ArgumentException => DiagnosticExceptionKind.Argument,
                InvalidOperationException => DiagnosticExceptionKind.InvalidOperation,
                _ => DiagnosticExceptionKind.Other
            },
            HResult = error.HResult,
            Failure = error switch
            {
                PrivacyScanException privacy => PrivacyFailure(privacy.Code),
                HubstaffControlException control => control.Code switch
                {
                    HubstaffControlNames.MissingCli => DiagnosticFailure.CliMissing,
                    HubstaffControlNames.InvalidStatus => DiagnosticFailure.CliInvalidStatus,
                    HubstaffControlNames.CommandFailed => DiagnosticFailure.CliCommandFailed,
                    HubstaffControlNames.TimedOut => DiagnosticFailure.CliTimeout,
                    HubstaffControlNames.InputUnavailable => DiagnosticFailure.InputUnavailable,
                    HubstaffControlNames.SelectionChanged => DiagnosticFailure.SelectionChanged,
                    HubstaffControlNames.StopUnconfirmed => DiagnosticFailure.StopUnconfirmed,
                    HubstaffControlNames.ResumeUnconfirmed => DiagnosticFailure.ResumeUnconfirmed,
                    _ => DiagnosticFailure.Unknown
                },
                _ => DiagnosticFailure.None
            },
            NativeCode = error switch
            {
                Win32Exception windows => windows.NativeErrorCode,
                SqliteException sqlite => sqlite.SqliteExtendedErrorCode,
                HttpRequestException http => (int?)http.StatusCode,
                _ => null
            },
            Stack = stack, Inner = captured, Truncated = truncated
        };
    }

    internal static DiagnosticFailure PrivacyFailure(string code) => code switch
    {
        "" => DiagnosticFailure.None,
        PrivacyNames.ModelContext => DiagnosticFailure.ContextExceeded,
        PrivacyNames.ModelTimeout => DiagnosticFailure.ModelTimeout,
        PrivacyNames.ModelMemory => DiagnosticFailure.ModelMemory,
        PrivacyNames.ModelServer => DiagnosticFailure.ModelServer,
        PrivacyNames.ModelRequest => DiagnosticFailure.ModelRequest,
        PrivacyNames.ModelConnection => DiagnosticFailure.ModelConnection,
        PrivacyNames.ModelUnavailable => DiagnosticFailure.ModelUnavailable,
        PrivacyNames.RuntimeUnavailable => DiagnosticFailure.RuntimeUnavailable,
        PrivacyNames.RuntimePortUnavailable => DiagnosticFailure.RuntimePortUnavailable,
        PrivacyNames.RuntimeStopped => DiagnosticFailure.RuntimeStopped,
        PrivacyNames.InvalidResponse => DiagnosticFailure.InvalidResponse,
        PrivacyNames.TruncatedResponse => DiagnosticFailure.TruncatedResponse,
        PrivacyNames.InvalidEvidence => DiagnosticFailure.InvalidEvidence,
        PrivacyNames.OcrFailure => DiagnosticFailure.OcrFailed,
        PrivacyNames.MissingCopy => DiagnosticFailure.CopyUnavailable,
        PrivacyNames.StorageFailure => DiagnosticFailure.StorageFailed,
        _ => DiagnosticFailure.Unknown
    };

    // Resolve trusted code metadata rather than exporting text supplied by an exception or a log file.
    internal static string MethodName(int token)
    {
        try
        {
            var method = typeof(DiagnosticException).Module.ResolveMethod(token);
            var name = method?.DeclaringType?.FullName + "." + method?.Name;

            return name.Length <= 256 ? name : "[application frame]";
        }
        catch (Exception error) when (error is ArgumentException or BadImageFormatException)
        {
            return "[unresolved frame]";
        }
    }

    internal DiagnosticException? ForExport(Guid moduleId, int depth = 0)
    {
        if (!Enum.IsDefined(Kind) || !Enum.IsDefined(Failure) || depth > 3 || Stack == null || Inner == null || Stack.Count > 32 || Inner.Count > 8)
            return null;

        return this with
        {
            Stack = Stack.Where(frame => frame != null).Take(32).Select(frame => frame with
            {
                Method = moduleId == typeof(DiagnosticException).Module.ModuleVersionId
                    ? MethodName(frame.Token) : "[previous build frame]"
            }).ToList(),
            Inner = Inner.Where(inner => inner != null).Select(inner => inner.ForExport(moduleId, depth + 1))
                .OfType<DiagnosticException>().ToList()
        };
    }
}
