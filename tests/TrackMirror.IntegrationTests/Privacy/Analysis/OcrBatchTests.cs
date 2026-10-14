// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0/.

using System.Text;
using TrackMirror.Constants;
using TrackMirror.Privacy.Analysis;
using TrackMirror.Privacy.Models;
using Xunit;

namespace TrackMirror.IntegrationTests.Privacy.Analysis;

public sealed class OcrBatchTests
{
    [Fact]
    public void Dense_unicode_text_preserves_tail_evidence_and_fits_serialized_budget()
    {
        // Arrange
        var text = string.Concat(Enumerable.Range(0, 1000).Select(index => $"Unicode\u0416\U0001F600{index:D4};"))
            + "TAIL_SENTINEL";

        var original = new OcrBlock(42, text, new PrivacyRegion(0, 0, 1, 1));

        // Act
        var batches = PrivacyOcrBatches.Create([original]);

        // Assert
        Assert.True(batches.Count > 1);

        var rebuilt = string.Empty;

        foreach (var batch in batches)
        {
            Assert.InRange(Encoding.UTF8.GetByteCount(PrivacyOcrBatches.Serialize(batch)),
                1, PrivacyProtocol.MaxOcrBatchBytes);

            foreach (var fragment in batch)
            {
                Assert.Equal(original.Id, fragment.Id);

                Assert.Equal(original.Region, fragment.Region);

                Assert.False(char.IsLowSurrogate(fragment.Text[0]));

                Assert.False(char.IsHighSurrogate(fragment.Text[^1]));

                var overlap = Math.Min(rebuilt.Length, fragment.Text.Length);

                while (overlap > 0 && !rebuilt.EndsWith(fragment.Text[..overlap], StringComparison.Ordinal))
                {
                    overlap--;
                }

                rebuilt += fragment.Text[overlap..];
            }
        }

        Assert.Equal(text, rebuilt);
    }

    [Fact]
    public void Single_block_context_split_preserves_unicode_and_makes_progress()
    {
        // Arrange
        var block = new OcrBlock(7, new string('x', 250) + "\U0001F600tail", new PrivacyRegion(0, 0, 1, 1));

        // Act
        var smaller = PrivacyOcrBatches.Split([block]);

        // Assert
        Assert.Equal(2, smaller.Length);

        Assert.All(smaller, batch =>
        {
            var fragment = Assert.Single(batch);

            Assert.Equal(block.Id, fragment.Id);

            Assert.InRange(fragment.Text.Length, 1, block.Text.Length - 1);

            Assert.False(char.IsHighSurrogate(fragment.Text[^1]));

            Assert.False(char.IsLowSurrogate(fragment.Text[0]));
        });

        // Act
        // Assert
        Assert.Empty(PrivacyOcrBatches.Split([block with { Text = "\U0001F600" }]));
    }
}
