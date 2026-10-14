// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Analysis;

internal static class PrivacyOcrBatches
{
    private static readonly JsonSerializerOptions json = new()
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    internal static string Serialize(IReadOnlyList<OcrBlock> blocks) => JsonSerializer.Serialize(new
    {
        ocr_blocks = blocks.Select(block => new object[] { block.Id, block.Text })
    }, json);

    internal static Queue<IReadOnlyList<OcrBlock>> Create(IReadOnlyList<OcrBlock> blocks)
    {
        var batches = new Queue<IReadOnlyList<OcrBlock>>();

        var current = new List<OcrBlock>();

        foreach (var block in blocks)
        {
            var start = 0;

            do
            {
                var end = Math.Min(block.Text.Length, start + PrivacyProtocol.MaxOcrFragmentCharacters);

                if (end < block.Text.Length && char.IsHighSurrogate(block.Text[end - 1])
                    && char.IsLowSurrogate(block.Text[end]))
                {
                    end--;
                }

                var fragment = block with { Text = block.Text[start..end] };

                current.Add(fragment);

                if (current.Count > PrivacyProtocol.MaxOcrBatchBlocks
                    || Encoding.UTF8.GetByteCount(Serialize(current)) > PrivacyProtocol.MaxOcrBatchBytes)
                {
                    current.RemoveAt(current.Count - 1);

                    if (current.Count == 0)
                        throw new PrivacyScanException(PrivacyNames.AnalysisLimit);

                    batches.Enqueue(current);

                    current = [fragment];
                }

                if (end == block.Text.Length)
                    break;

                start = end - PrivacyProtocol.OcrOverlapCharacters;

                if (start > 0 && char.IsLowSurrogate(block.Text[start])
                    && char.IsHighSurrogate(block.Text[start - 1]))
                {
                    start--;
                }
            }
            while (start < block.Text.Length);
        }

        if (current.Count > 0 || batches.Count == 0)
        {
            batches.Enqueue(current);
        }

        return batches;
    }

    internal static IReadOnlyList<OcrBlock>[] Split(IReadOnlyList<OcrBlock> blocks)
    {
        if (blocks.Count > 1)
        {
            var middle = blocks.Count / 2;

            return [blocks.Take(middle).ToArray(), blocks.Skip(middle).ToArray()];
        }

        if (blocks.Count == 0 || blocks[0].Text.Length < 2)
            return [];

        var block = blocks[0];

        var split = block.Text.Length / 2;

        if (char.IsLowSurrogate(block.Text[split]) && char.IsHighSurrogate(block.Text[split - 1]))
        {
            split++;
        }

        if (split == block.Text.Length)
            return [];

        // Preserve a shared boundary without creating children as large as their parent.
        var overlap = Math.Min(PrivacyProtocol.OcrOverlapCharacters, Math.Min(split, block.Text.Length - split) / 2);

        var leftEnd = split + overlap;

        var rightStart = split - overlap;

        if (leftEnd < block.Text.Length && char.IsLowSurrogate(block.Text[leftEnd])
            && char.IsHighSurrogate(block.Text[leftEnd - 1]))
        {
            leftEnd++;
        }

        if (rightStart > 0 && char.IsLowSurrogate(block.Text[rightStart])
            && char.IsHighSurrogate(block.Text[rightStart - 1]))
        {
            rightStart--;
        }

        if (leftEnd >= block.Text.Length || rightStart <= 0)
        {
            leftEnd = rightStart = split;
        }

        return [[block with { Text = block.Text[..leftEnd] }], [block with { Text = block.Text[rightStart..] }]];
    }
}
