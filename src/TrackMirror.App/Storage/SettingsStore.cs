// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Text.Json;
using TrackMirror.Hubstaff;
using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Localization;
using TrackMirror.Privacy.Installation;

namespace TrackMirror.Storage;

public sealed class SettingsStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public AppSettings Current { get; private set; } = new();

    public string? InitialWarning { get; }

    public SettingsStore(string dataRoot)
    {
        _path = Path.Combine(dataRoot, StorageNames.SettingsFile);

        if (!File.Exists(_path))
            return;

        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);

            using var reader = new StreamReader(stream);

            using var document = JsonDocument.Parse(reader.ReadToEnd());

            var root = document.RootElement;

            var saved = root.Deserialize<AppSettings>();

            if (saved == null || saved.NotificationTimeoutSeconds is < NotificationDefaults.MinimumSeconds
                or > NotificationDefaults.MaximumSeconds)
            {
                InitialWarning = SettingsText.LoadFailed;

                return;
            }

            if (root.TryGetProperty(StorageNames.LegacyPrivacyPersonalActivity, out var personal))
            {
                if (personal.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    throw new JsonException();

                saved = saved with
                {
                    PrivacyGames = root.TryGetProperty(nameof(AppSettings.PrivacyGames), out _)
                        ? saved.PrivacyGames : personal.GetBoolean(),
                    PrivacyPersonalChats = root.TryGetProperty(nameof(AppSettings.PrivacyPersonalChats), out _)
                        ? saved.PrivacyPersonalChats : personal.GetBoolean()
                };
            }

            if (!PrivacyModelCatalog.Known(saved.PrivacyModel))
            {
                saved = saved with { PrivacyModel = PrivacyNames.Model };

                InitialWarning = PrivacyInstallText.InvalidSavedModel;
            }

            if (saved.RetentionDays is < 1 or > DataManagementNames.MaximumDays)
            {
                saved = saved with { RetentionEnabled = false, RetentionDays = DataManagementNames.DefaultDays };

                InitialWarning = DataManagementText.InvalidRetention;
            }

            if (saved.HubstaffControl is not { IsValid: true })
            {
                saved = saved with { HubstaffControl = new HubstaffControlOptions() };

                InitialWarning ??= HubstaffControlText.InvalidSettings;
            }

            if (!UiLocalization.Supported(saved.Language))
            {
                saved = saved with { Language = LocalizationNames.English };

                InitialWarning ??= LanguageText.InvalidSaved;
            }

            if (!Enum.IsDefined(saved.UpdateChannel))
            {
                saved = saved with { UpdateChannel = UpdateChannel.PreRelease };

                InitialWarning ??= AboutText.InvalidChannel;
            }

            if (saved.ScreenshotCountdown is { Enabled: true } legacyCountdown
                && root.TryGetProperty(nameof(AppSettings.ScreenshotCountdown), out var countdownElement)
                && countdownElement.ValueKind == JsonValueKind.Object
                && !countdownElement.TryGetProperty(nameof(ScreenshotCountdownOptions.Mode), out _))
            {
                saved = saved with { ScreenshotCountdown = legacyCountdown with { Mode = ScreenshotCountdownMode.Detailed } };
            }

            if (saved.ScreenshotCountdown is not { IsValid: true })
            {
                saved = saved with { ScreenshotCountdown = new ScreenshotCountdownOptions() };

                InitialWarning ??= CountdownText.InvalidSettings;
            }

            Current = saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            InitialWarning = SettingsText.LoadFailed;
        }
    }

    public void Save(AppSettings settings)
    {
        if (!Enum.IsDefined(settings.UpdateChannel))
            throw new ArgumentException(nameof(settings));

        if (!UiLocalization.Supported(settings.Language))
            throw new ArgumentException(nameof(settings));

        if (!PrivacyModelCatalog.Known(settings.PrivacyModel))
            throw new ArgumentException(nameof(settings));

        if (settings.NotificationTimeoutSeconds is < NotificationDefaults.MinimumSeconds
            or > NotificationDefaults.MaximumSeconds)
            throw new ArgumentOutOfRangeException(nameof(settings));

        if (settings.RetentionDays is < 1 or > DataManagementNames.MaximumDays)
            throw new ArgumentOutOfRangeException(nameof(settings));

        if (settings.HubstaffControl is not { IsValid: true })
            throw new ArgumentException(nameof(settings));

        if (settings.ScreenshotCountdown is not { IsValid: true })
            throw new ArgumentException(nameof(settings));

        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        var temporaryPath = _path + StorageNames.TemporarySuffix;

        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));

        if (File.Exists(_path))
        {
            File.Replace(temporaryPath, _path, destinationBackupFileName: null);
        }
        else
        {
            File.Move(temporaryPath, _path);
        }

        Current = settings;
    }
}
