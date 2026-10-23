// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json.Serialization;

namespace TrackMirror.Models;

public sealed record ScreenshotCountdownOptions
{
    public bool Enabled { get; init; }

    public ScreenshotCountdownMode Mode { get; init; }

    public int ShowBeforeSeconds { get; init; } = 30;

    public bool HideBeforeScreenshot { get; init; }

    public int HideBeforeSeconds { get; init; } = 3;

    public bool MiddleButtonDrag { get; init; }

    public int HighlightSeconds { get; init; } = 10;

    public int ScalePercent { get; init; } = 100;

    public int BackgroundOpacityPercent { get; init; } = 80;

    public int? Left { get; init; }
    public int? Top { get; init; }

    [JsonIgnore]
    public bool IsValid => Enum.IsDefined(Mode)
        && ShowBeforeSeconds is >= 1 and <= 3600
        && HideBeforeSeconds is >= 1 and <= 3600
        && (!HideBeforeScreenshot || Mode != ScreenshotCountdownMode.BeforeScreenshot || HideBeforeSeconds < ShowBeforeSeconds)
        && HighlightSeconds is >= 1 and <= 120
        && ScalePercent is >= 50 and <= 200
        && BackgroundOpacityPercent is >= 0 and <= 100
        && Left.HasValue == Top.HasValue
        && (Left is null or >= -100000 and <= 100000)
        && (Top is null or >= -100000 and <= 100000);
}
