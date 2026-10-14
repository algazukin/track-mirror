// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Storage.Sqlite;
using TrackMirror.Storage.Management;

namespace TrackMirror.Storage;

public sealed class ScreenshotStore
{
    private readonly object _gate = new();
    private readonly ViewerDatabase _database;
    private readonly CaptureRetention _retention;
    private readonly Dictionary<string, CaptureEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CaptureEntry> _stored = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _sourceIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _dirty = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _deleted = new(StringComparer.OrdinalIgnoreCase);
    public string DataRoot { get; }
    public string CapturesRoot { get; }
    public string? InitialWarning { get; private set; }

    internal int MissingCopyCount
    {
        get
        {
            lock (_gate)
            {
                return _stored.Count - _entries.Count;
            }
        }
    }

    public event Action<string>? Warning;
    public ScreenshotStore(string dataRoot)
    {
        DataRoot = Path.GetFullPath(dataRoot);

        CapturesRoot = Path.Combine(DataRoot, StorageNames.CapturesDirectory);

        Directory.CreateDirectory(CapturesRoot);

        _database = new ViewerDatabase(DataRoot);

        _retention = new CaptureRetention(_database);

        Reload();
    }

    internal void Reload()
    {
        lock (_gate)
        {
            if (_dirty.Count > 0)
                throw new IOException(DataManagementText.Unsaved);

            _retention.RecoverPending(default);

            _entries.Clear();

            _stored.Clear();

            _sourceIds.Clear();

            _deleted.Clear();

            foreach (var entry in _retention.Deleted())
            {
                _deleted.Add(entry.Id);

                _deleted.Add(entry.Account + StorageNames.EntrySeparator + entry.FileName);
            }

            InitialWarning = _database.InitialWarning;

            foreach (var entry in CaptureRecords.Read(_database))
            {
                _stored[entry.Id] = entry;

                _sourceIds[entry.Account + StorageNames.EntrySeparator + entry.FileName] = entry.Id;

                if (File.Exists(CopyPath(entry)))
                {
                    _entries[entry.Id] = entry;
                }
            }

            foreach (var accountDir in Directory.EnumerateDirectories(CapturesRoot))
            {
                foreach (var file in Directory.EnumerateFiles(accountDir, StorageNames.ScreenshotPattern))
                {
                    var account = Path.GetFileName(accountDir);

                    var name = Path.GetFileName(file);

                    var id = account + StorageNames.EntrySeparator + name;

                    if (_deleted.Contains(id))
                        continue;

                    var storedId = _sourceIds.GetValueOrDefault(id) ?? id;

                    if (_entries.ContainsKey(storedId))
                        continue;

                    var entry = _stored.GetValueOrDefault(storedId) ?? new CaptureEntry
                    {
                        Id = id,
                        Account = account,
                        FileName = name,
                        CapturedUtc = CaptureFileMetadata.CaptureTime(name, File.GetLastWriteTimeUtc(file)),
                        SavedUtc = File.GetLastWriteTimeUtc(file),
                        Viewed = true
                    };

                    _entries[entry.Id] = entry;

                    _stored[entry.Id] = entry;

                    _sourceIds[id] = entry.Id;

                    _dirty.Add(entry.Id);
                }
            }

            SaveChanges();
        }
    }

    public bool Contains(string id)
    {
        lock (_gate)
        {
            return _deleted.Contains(id) || _entries.ContainsKey(id)
                || _sourceIds.TryGetValue(id, out var storedId) && _entries.ContainsKey(storedId);
        }
    }

    public List<CaptureEntry> Snapshot()
    {
        lock (_gate)
        {
            return _entries.Values.OrderByDescending(entry => entry.CapturedUtc)
                .ThenByDescending(entry => entry.SavedUtc).ToList();
        }
    }

    public string CopyPath(CaptureEntry entry) => Path.Combine(CapturesRoot, entry.Account, entry.FileName);
    public Bitmap OpenImage(CaptureEntry entry)
    {
        using var stream = new MemoryStream(File.ReadAllBytes(CopyPath(entry)));

        using var image = Image.FromStream(stream, false, true);

        return new Bitmap(image);
    }

    public CaptureEntry Add(string account, string fileName, DateTime fallbackTime, bool notify)
    {
        lock (_gate)
        {
            var id = account + StorageNames.EntrySeparator + fileName;

            if (_deleted.Contains(id))
                throw new IOException(DataManagementText.UnsafePath);

            var entry = _stored.GetValueOrDefault(_sourceIds.GetValueOrDefault(id) ?? id) ?? new CaptureEntry
            {
                Id = id,
                Account = account,
                FileName = fileName,
                CapturedUtc = CaptureFileMetadata.CaptureTime(fileName, fallbackTime),
                Viewed = !notify
            };

            entry = entry with
            {
                SavedUtc = DateTime.UtcNow
            };

            _entries[entry.Id] = entry;

            _stored[entry.Id] = entry;

            _sourceIds[id] = entry.Id;

            _dirty.Add(entry.Id);

            SaveChanges();

            return entry;
        }
    }

    internal void FlushPending()
    {
        lock (_gate)
        {
            SaveChanges();

            if (_dirty.Count != 0)
                throw new IOException(DataManagementText.Unsaved);
        }
    }

    public void MarkViewed(string id)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return;

            _entries[entry.Id] = _stored[entry.Id] = entry with
            {
                Viewed = true
            };

            _dirty.Add(entry.Id);

            SaveChanges();
        }
    }

    public void ToggleFlag(string id)
    {
        lock (_gate)
        {
            if (!_entries.TryGetValue(id, out var entry))
                return;

            _entries[entry.Id] = _stored[entry.Id] = entry with
            {
                Flagged = !entry.Flagged
            };

            _dirty.Add(entry.Id);

            SaveChanges();
        }
    }

    internal List<string> SetViewed(IEnumerable<string> ids, bool viewed)
    {
        lock (_gate)
        {
            FlushPending();

            var changes = ids.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => _entries.GetValueOrDefault(id))
                .Where(entry => entry != null && entry.Viewed != viewed)
                .Select(entry => entry! with { Viewed = viewed }).ToList();

            if (changes.Count == 0)
                return [];

            _database.Write((connection, transaction) => CaptureRecords.Write(connection, transaction, changes));

            foreach (var entry in changes)
            {
                _entries[entry.Id] = _stored[entry.Id] = entry;
            }

            return changes.Select(entry => entry.Id).ToList();
        }
    }

    private void SaveChanges()
    {
        if (_dirty.Count == 0)
            return;

        try
        {
            _database.Write(
                (connection, transaction) => CaptureRecords.Write(connection, transaction,
                    _dirty.Select(id => _stored[id]))
            );

            _dirty.Clear();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            InitialWarning = DatabaseText.WriteFailed;

            Warning?.Invoke(DatabaseText.WriteFailed);
        }
    }
}
