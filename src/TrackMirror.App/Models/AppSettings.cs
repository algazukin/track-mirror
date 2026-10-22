// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Hubstaff;
using System.Text.Json.Serialization;

namespace TrackMirror.Models;

public sealed record AppSettings
{
    public string Language { get; init; } = LocalizationNames.English;

    public bool StartWithHubstaff { get; init; }

    public HubstaffControlOptions HubstaffControl { get; init; } = new();

    public ScreenshotCountdownOptions ScreenshotCountdown { get; init; } = new();

    public bool RetentionEnabled { get; init; }

    public int RetentionDays { get; init; } = DataManagementNames.DefaultDays;

    public bool RetentionKeepFlagged { get; init; } = true;

    public bool RetentionKeepUnread { get; init; } = true;

    public bool NotificationsEnabled { get; init; } = true;

    public int NotificationTimeoutSeconds { get; init; } = NotificationDefaults.TimeoutSeconds;

    public bool PrivacyAutoScan { get; init; }

    public string PrivacyModel { get; init; } = PrivacyNames.Model;

    public bool PrivacyGames { get; init; }

    public bool PrivacyPersonalChats { get; init; }

    public bool PrivacyConfidentialData { get; init; } = true;

    [JsonIgnore]
    public bool HasPrivacyCategories => PrivacyGames || PrivacyPersonalChats || PrivacyConfidentialData;
}
