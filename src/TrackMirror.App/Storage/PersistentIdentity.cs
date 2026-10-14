// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text;

namespace TrackMirror.Storage;

internal static class PersistentIdentity
{
    internal static string Key(string value)
    {
        var key = new StringBuilder(value.Length);

        for (var index = 0; index < value.Length;)
        {
            if (!Rune.TryGetRuneAt(value, index, out var rune))
            {
                key.Append(value[index++]);

                continue;
            }

            var original = rune.ToString();

            var upper = Rune.ToUpperInvariant(rune).ToString();

            // OrdinalIgnoreCase does not equate every Unicode scalar with its invariant uppercase (for example, ſ).
            key.Append(string.Equals(original, upper, StringComparison.OrdinalIgnoreCase) ? upper : original);

            index += rune.Utf16SequenceLength;
        }

        return key.ToString();
    }
}
