// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


namespace TrackMirror.Hubstaff;

public sealed record TimerNotesOptions
{
    public bool Enabled { get; init; }

    public bool ShowPrompt { get; init; } = true;

    public int PromptSeconds { get; init; } = 15;

    public string[] Reasons { get; init; } = ["Finished work", "Break", "Lunch", "Tired"];

    internal bool IsValid => PromptSeconds is >= 5 and <= 60 && Reasons != null && Reasons.Length <= 12
        && Reasons.All(reason => !string.IsNullOrWhiteSpace(reason) && reason.Length <= 80);

    public bool Equals(TimerNotesOptions? other) => other != null && Enabled == other.Enabled
        && ShowPrompt == other.ShowPrompt && PromptSeconds == other.PromptSeconds
        && ((Reasons == null && other.Reasons == null)
            || (Reasons != null && other.Reasons != null && Reasons.SequenceEqual(other.Reasons, StringComparer.Ordinal)));

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Enabled);
        hash.Add(ShowPrompt);
        hash.Add(PromptSeconds);

        if (Reasons != null)
        {
            foreach (var reason in Reasons)
            {
                hash.Add(reason, StringComparer.Ordinal);
            }
        }

        return hash.ToHashCode();
    }
}
