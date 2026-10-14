// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Models;
using TrackMirror.Storage.Sqlite;

namespace TrackMirror.Storage;

internal sealed class HubstaffActivityStore
{
    private readonly ViewerDatabase _database;
    private readonly Dictionary<string, SortedDictionary<DateTime, HubstaffActivity>> _accounts =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Account, DateTime Started), HubstaffActivity> _dirty = new();

    internal string? InitialWarning { get; }

    internal HubstaffActivityStore(string dataRoot)
    {
        _database = new ViewerDatabase(dataRoot);

        InitialWarning = _database.InitialWarning;

        Merge(ActivityRecords.Read(_database));

        _dirty.Clear();
    }

    internal List<HubstaffActivity> Snapshot() => _accounts.Values.SelectMany(values => values.Values).ToList();

    internal List<HubstaffActivity> LatestSnapshot() => _accounts.Values.Select(values => values.Last().Value).ToList();

    internal List<HubstaffActivity> RetryLoadHistory() => [];

    internal List<HubstaffActivity> Merge(IEnumerable<HubstaffActivity> entries)
    {
        var changed = new List<HubstaffActivity>();

        foreach (var entry in entries)
        {
            if (!HubstaffActivityValidation.Valid(entry))
                continue;

            if (!_accounts.TryGetValue(entry.Account, out var records))
            {
                records = [];

                _accounts.Add(entry.Account, records);
            }

            if (records.TryGetValue(entry.StartedUtc, out var previous))
            {
                if (entry.TrackedSeconds < previous.TrackedSeconds || entry.ObservedUtc < previous.ObservedUtc)
                    continue;

                if ((entry with { ObservedUtc = previous.ObservedUtc }) == previous)
                    continue;
            }

            records[entry.StartedUtc] = entry;

            changed.Add(entry);

            _dirty[(PersistentIdentity.Key(entry.Account), entry.StartedUtc)] = entry;
        }

        return changed;
    }

    internal void Save()
    {
        if (_dirty.Count == 0)
            return;

        _database.Write((connection, transaction) => ActivityRecords.Write(connection, transaction, _dirty.Values));

        _dirty.Clear();
    }
}
