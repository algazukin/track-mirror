// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.App;
using TrackMirror.Constants;

namespace TrackMirror.Storage.Management;

internal sealed class DataLocationStore
{
    private readonly string _applicationRoot;
    private readonly string _profileRoot;

    internal DataLocationStore(string applicationRoot, string? profileRoot = null)
    {
        _applicationRoot = DataFolderRules.Canonical(applicationRoot);

        _profileRoot = DataFolderRules.Canonical(profileRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            DataManagementNames.ApplicationFolder, DataManagementNames.Installations,
            SingleInstance.Identifier(applicationRoot)));
    }

    internal string Resolve(string? explicitRoot = null)
    {
        if (explicitRoot != null)
            return ResolveRedirects(DataFolderRules.Canonical(explicitRoot));

        var preference = Path.Combine(_profileRoot, DataManagementNames.LocationFile);

        if (File.Exists(preference))
        {
            var saved = AtomicJson.Read<DataLocationRecord>(preference);

            if (!Path.IsPathFullyQualified(saved.Root) || !Directory.Exists(saved.Root))
                throw new IOException(DataManagementText.UnavailableFolder);

            ValidateMarker(saved);

            return ResolveRedirects(saved.Root);
        }

        var portable = Path.Combine(_applicationRoot, StorageNames.DataDirectory);

        return ResolveRedirects(Directory.Exists(portable) ? portable
            : Path.Combine(_profileRoot, StorageNames.DataDirectory));
    }

    internal void RecordSelection(string root)
    {
        var full = DataFolderRules.Canonical(root);

        if (!Directory.Exists(full))
            throw new IOException(DataManagementText.UnavailableFolder);

        var marker = Path.Combine(full, DataManagementNames.MarkerFile);

        var id = File.Exists(marker) ? AtomicJson.Read<DataFolderMarker>(marker).TransferId : (Guid?)null;

        AtomicJson.Write(Path.Combine(_profileRoot, DataManagementNames.LocationFile),
            new DataLocationRecord(full, id));
    }

    internal static string ResolveRedirects(string root)
    {
        var current = DataFolderRules.Canonical(root);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (File.Exists(Path.Combine(current, DataManagementNames.RedirectFile)))
        {
            if (!seen.Add(current) || seen.Count > 16)
                throw new IOException(DataManagementText.RedirectCycle);

            var redirect = AtomicJson.Read<DataLocationRecord>(Path.Combine(current, DataManagementNames.RedirectFile));

            if (!Path.IsPathFullyQualified(redirect.Root) || redirect.TransferId == null)
                throw new IOException(DataManagementText.InvalidRedirect);

            ValidateMarker(redirect);

            current = DataFolderRules.Canonical(redirect.Root);
        }

        return current;
    }

    private static void ValidateMarker(DataLocationRecord record)
    {
        if (record.TransferId == null)
            return;

        DataFolderRules.CheckLocal(record.Root);

        var marker = Path.Combine(record.Root, DataManagementNames.MarkerFile);

        if (record.TransferId == Guid.Empty || !File.Exists(marker)
            || !File.Exists(Path.Combine(record.Root, DatabaseNames.FileName))
            || AtomicJson.Read<DataFolderMarker>(marker).TransferId != record.TransferId)
            throw new IOException(DataManagementText.InvalidRedirect);
    }
}
