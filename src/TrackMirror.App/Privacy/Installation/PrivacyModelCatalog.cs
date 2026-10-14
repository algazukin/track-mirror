// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Security.Cryptography;
using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Installation;

internal static class PrivacyModelCatalog
{
    internal static IReadOnlyList<PrivacyModelDefinition> All { get; } =
    [
        new(PrivacyModelNames.Qwen2B,
            PrivacyModelNames.Qwen2BDigest, PrivacyModelNames.Qwen2BLocalDigest,
            PrivacyModelNames.Qwen2BManifest, 1889519232),
        new(PrivacyModelNames.Qwen4B, PrivacyNames.ModelDigest,
            PrivacyNames.LocalManifestHash, PrivacyModelNames.Qwen4BManifest, 3295635680),
        new(PrivacyModelNames.Qwen8B,
            PrivacyModelNames.Qwen8BDigest, PrivacyModelNames.Qwen8BLocalDigest,
            PrivacyModelNames.Qwen8BManifest, 6140415424),
        new(PrivacyModelNames.Gemma4B,
            PrivacyModelNames.Gemma4BDigest, PrivacyModelNames.Gemma4BLocalDigest,
            PrivacyModelNames.Gemma4BManifest, 3350272259)
    ];

    internal static PrivacyModelDefinition Get(string tag) => All.First(model => model.Tag == tag);

    internal static bool Known(string? tag) => All.Any(model => model.Tag == tag);

    internal static string Version(string tag) => tag == PrivacyNames.Model ? PrivacyNames.Version
        : PrivacyNames.Version + PrivacyModelNames.VersionSeparator + Get(tag).ManifestDigest;

    internal static string ManifestPath(string root, PrivacyModelDefinition model)
    {
        var parts = model.Tag.Split(':');

        return Path.Combine(root, PrivacyNames.ModelsDirectory, PrivacyModelNames.ManifestDirectory,
            PrivacyModelNames.RegistryDirectory, PrivacyModelNames.LibraryDirectory, parts[0], parts[1]);
    }

    internal static bool Installed(string root, string tag)
    {
        if (!Known(tag) || !File.Exists(Path.Combine(root, PrivacyNames.RuntimeExecutable)))
            return false;

        var model = Get(tag);

        return Installed(root, model);
    }

    internal static bool Installed(string root, PrivacyModelDefinition model)
        => ManifestComplete(root, model) && !File.Exists(PendingPath(root, model))
            && !File.Exists(RemovalPath(root, model));

    internal static bool Removable(string root, PrivacyModelDefinition model) =>
        File.Exists(ManifestPath(root, model)) || File.Exists(RemovalPath(root, model));

    internal static string RemovalPath(string root, PrivacyModelDefinition model) =>
        Path.Combine(root, PrivacyNames.ModelsDirectory, PrivacyInstallNames.RemovalPendingPrefix
            + model.Tag.Replace(':', '-') + PrivacyInstallNames.PendingSuffix);

    internal static string PendingPath(string root, PrivacyModelDefinition model) =>
        Path.Combine(root, PrivacyNames.ModelsDirectory, PrivacyInstallNames.ModelPendingPrefix
            + model.Tag.Replace(':', '-') + PrivacyInstallNames.PendingSuffix);

    internal static bool ManifestComplete(string root, PrivacyModelDefinition model)
    {
        if (!File.Exists(Path.Combine(root, PrivacyNames.RuntimeExecutable))
            || File.Exists(Path.Combine(root, PrivacyInstallNames.RuntimePendingFile)))
            return false;

        var path = ManifestPath(root, model);

        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 65536)
                return false;

            var bytes = File.ReadAllBytes(path);

            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            if (hash != model.ManifestDigest && hash != model.LocalManifestDigest)
                return false;

            using var parsed = JsonDocument.Parse(bytes);

            return Parts(parsed).All(part =>
            {
                var blob = BlobPath(root, part);

                return File.Exists(blob)
                    && new FileInfo(blob).Length == part.GetProperty(PrivacyProtocol.Size).GetInt64();
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return false;
        }
    }

    internal static IEnumerable<JsonElement> Parts(JsonDocument manifest) =>
        manifest.RootElement.GetProperty(PrivacyProtocol.Layers).EnumerateArray()
            .Append(manifest.RootElement.GetProperty(PrivacyProtocol.Config));

    internal static string BlobPath(string root, JsonElement part) => Path.Combine(root, PrivacyNames.ModelsDirectory,
        PrivacyNames.BlobsDirectory, part.GetProperty(PrivacyProtocol.Digest).GetString()!
            .Replace(PrivacyProtocol.Sha256Prefix, PrivacyProtocol.BlobPrefix));
}
