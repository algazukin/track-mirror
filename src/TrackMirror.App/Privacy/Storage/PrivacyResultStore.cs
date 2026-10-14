// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Privacy.Models;
using TrackMirror.Storage.Sqlite;

namespace TrackMirror.Privacy.Storage;

internal sealed class PrivacyResultStore
{
    private readonly object _gate = new();
    private readonly ViewerDatabase _database;
    private readonly Dictionary<string, PrivacyScanRecord> _entries = new(StringComparer.OrdinalIgnoreCase);

    internal string? InitialWarning { get; }

    internal PrivacyResultStore(string dataRoot, IReadOnlySet<string> captures)
    {
        _database = new ViewerDatabase(dataRoot);

        InitialWarning = _database.InitialWarning;

        Reload();
    }

    internal void Reload()
    {
        lock (_gate)
        {
            var loaded = new Dictionary<string, PrivacyScanRecord>(StringComparer.OrdinalIgnoreCase);

            var recovered = new List<PrivacyScanRecord>();

            foreach (var entry in PrivacyRecords.Read(_database))
            {
                if (!PrivacyRecordValidation.Valid(entry))
                    throw new InvalidDataException(DatabaseText.Unavailable);

                var record = entry;

                if (record.State == PrivacyScanState.Scanning)
                {
                    record = record with
                    {
                        State = record.Attempts < 2 ? PrivacyScanState.Queued : PrivacyScanState.Failed,
                        FailureCode = PrivacyNames.Interrupted
                    };

                    recovered.Add(record);
                }

                loaded[record.CaptureId] = record;
            }

            if (recovered.Count > 0)
            {
                _database.Write((connection, transaction) => PrivacyRecords.Write(connection, transaction, recovered));
            }

            _entries.Clear();

            foreach (var pair in loaded)
            {
                _entries.Add(pair.Key, pair.Value);
            }
        }
    }

    internal Dictionary<string, PrivacyScanRecord> Snapshot()
    {
        lock (_gate)
        {
            return new Dictionary<string, PrivacyScanRecord>(_entries, StringComparer.OrdinalIgnoreCase);
        }
    }

    internal void Put(IEnumerable<PrivacyScanRecord> changed)
    {
        lock (_gate)
        {
            var records = changed.ToList();

            _database.Write((connection, transaction) => PrivacyRecords.Write(connection, transaction, records));

            foreach (var record in records)
            {
                _entries[record.CaptureId] = record;
            }
        }
    }

    internal void SetTransientFailure(PrivacyScanRecord record)
    {
        lock (_gate)
        {
            // The durable Scanning checkpoint is retained so restart can recover an interrupted job.
            _entries[record.CaptureId] = record with
            {
                State = PrivacyScanState.Failed,
                FailureCode = PrivacyNames.StorageFailure
            };
        }
    }
}
