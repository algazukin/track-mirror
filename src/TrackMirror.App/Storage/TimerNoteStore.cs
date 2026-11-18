// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Models;
using TrackMirror.Storage.Sqlite;

namespace TrackMirror.Storage;

internal sealed class TimerNoteStore
{
    private readonly ViewerDatabase _database;

    internal TimerNoteStore(string dataRoot)
    {
        _database = new ViewerDatabase(dataRoot);
    }

    internal List<TimerStopNote> Snapshot()
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, stopped_utc, project_id, task_id, idle_seconds, reason
            FROM timer_stop_notes ORDER BY stopped_utc DESC, id;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<TimerStopNote>();

        while (reader.Read())
        {
            result.Add(new TimerStopNote(reader.GetString(0), new DateTime(reader.GetInt64(1), DateTimeKind.Utc),
                reader.IsDBNull(2) ? null : reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4), reader.GetString(5)));
        }

        return result;
    }

    internal void Add(TimerStopNote note)
    {
        ArgumentNullException.ThrowIfNull(note);

        if (string.IsNullOrEmpty(note.Id) || note.StoppedUtc.Kind != DateTimeKind.Utc
            || note.IdleSeconds < 0 || note.Reason == null || note.Reason.Length > 1000)
        {
            throw new ArgumentException(nameof(note));
        }

        _database.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT OR IGNORE INTO timer_stop_notes(id, stopped_utc, project_id, task_id, idle_seconds, reason)
                VALUES($id, $utc, $project, $task, $idle, $reason);
                """;
            command.Parameters.AddWithValue("$id", note.Id);
            command.Parameters.AddWithValue("$utc", note.StoppedUtc.Ticks);
            command.Parameters.AddWithValue("$project", (object?)note.ProjectId ?? DBNull.Value);
            command.Parameters.AddWithValue("$task", (object?)note.TaskId ?? DBNull.Value);
            command.Parameters.AddWithValue("$idle", note.IdleSeconds);
            command.Parameters.AddWithValue("$reason", note.Reason);
            command.ExecuteNonQuery();
        });
    }

    internal void SetReason(string id, string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (reason.Length > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        _database.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE timer_stop_notes SET reason=$reason WHERE id=$id;";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$reason", string.IsNullOrWhiteSpace(reason) ? string.Empty : reason);
            command.ExecuteNonQuery();
        });
    }

    internal void Delete(string id)
    {
        _database.Write((connection, transaction) =>
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM timer_stop_notes WHERE id=$id;";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        });
    }
}
