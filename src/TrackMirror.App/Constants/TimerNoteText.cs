// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Localization;
using TrackMirror.Models;

namespace TrackMirror.Constants;

internal static class TimerNoteText
{
    internal static string Heading => UiLocalization.Get(nameof(TimerNoteText), nameof(Heading));
    internal static string Description => UiLocalization.Get(nameof(TimerNoteText), nameof(Description));
    internal static string Enabled => UiLocalization.Get(nameof(TimerNoteText), nameof(Enabled));
    internal static string ShowPrompt => UiLocalization.Get(nameof(TimerNoteText), nameof(ShowPrompt));
    internal static string PromptSeconds => UiLocalization.Get(nameof(TimerNoteText), nameof(PromptSeconds));
    internal static string Reasons => UiLocalization.Get(nameof(TimerNoteText), nameof(Reasons));
    internal static string ReasonsHint => UiLocalization.Get(nameof(TimerNoteText), nameof(ReasonsHint));
    internal static string Other => UiLocalization.Get(nameof(TimerNoteText), nameof(Other));
    internal static string Save => UiLocalization.Get(nameof(TimerNoteText), nameof(Save));
    internal static string Skip => UiLocalization.Get(nameof(TimerNoteText), nameof(Skip));
    internal static string Unknown => UiLocalization.Get(nameof(TimerNoteText), nameof(Unknown));
    internal static string IdleFormat => UiLocalization.Get(nameof(TimerNoteText), nameof(IdleFormat));
    internal static string StoppedHeading => UiLocalization.Get(nameof(TimerNoteText), nameof(StoppedHeading));
    internal static string HistoryEmpty => UiLocalization.Get(nameof(TimerNoteText), nameof(HistoryEmpty));
    internal static string EditHint => UiLocalization.Get(nameof(TimerNoteText), nameof(EditHint));
    internal static string Delete => UiLocalization.Get(nameof(TimerNoteText), nameof(Delete));
    internal static string SaveFailed => UiLocalization.Get(nameof(TimerNoteText), nameof(SaveFailed));
    internal static string Unavailable => UiLocalization.Get(nameof(TimerNoteText), nameof(Unavailable));
    internal static string Disabled => UiLocalization.Get(nameof(TimerNoteText), nameof(Disabled));
    internal static string Available => UiLocalization.Get(nameof(TimerNoteText), nameof(Available));
    internal static string NoteColumn => UiLocalization.Get(nameof(TimerNoteText), nameof(NoteColumn));
    internal static string TimeColumn => UiLocalization.Get(nameof(TimerNoteText), nameof(TimeColumn));
    internal static string LocalHint => UiLocalization.Get(nameof(TimerNoteText), nameof(LocalHint));
    internal static string DefaultReasons => UiLocalization.Get(nameof(TimerNoteText), nameof(DefaultReasons));
    internal static string InvalidReasons => UiLocalization.Get(nameof(TimerNoteText), nameof(InvalidReasons));

    internal static string Reason(TimerStopNote note) => note.Reason.Length > 0 ? note.Reason
        : note.IdleSeconds > 0 ? string.Format(IdleFormat, note.IdleSeconds) : Unknown;
}

