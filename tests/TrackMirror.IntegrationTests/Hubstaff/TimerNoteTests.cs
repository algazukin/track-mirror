// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.IntegrationTests.Infrastructure;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure.TestDoubles;
using TrackMirror.Diagnostics;
using TrackMirror.Storage.Management;
using System.IO.Compression;

namespace TrackMirror.IntegrationTests.Hubstaff;

public sealed class TimerNoteTests
{
    [Fact]
    public async Task External_stop_after_a_confirmed_internal_resume_is_not_lost_between_polls()
    {
        // Arrange
        var cli = new ControlCliDouble();
        var clock = new ControlClock();
        var controller = new HubstaffIdleController(cli,
            () => new IdleObservation(TimeSpan.FromMinutes(2), 1), clock);
        var completion = new TaskCompletionSource<TimerStopNote>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var service = new HubstaffControlService(new HubstaffControlOptions
        {
            Enabled = true, Mode = HubstaffControlMode.RestartWhileIdle, RestartSeconds = 2
        }, controller, new TimerNotesOptions { Enabled = true }, cli, clock,
            note => completion.TrySetResult(note));

        // Act
        await WaitForAsync(() => service.NotesAvailable);
        clock.Advance(1);
        await WaitForAsync(() => service.Snapshot().Seconds == 1);
        clock.Advance(1);
        await WaitForAsync(() => cli.Commands.Count == 2 && service.Snapshot().Seconds == 2);
        cli.Tracking = false;
        clock.Advance(3);
        await Task.WhenAny(completion.Task, Task.Delay(3000));

        // Assert
        Assert.True(completion.Task.IsCompletedSuccessfully, "The external stop after resume was not recorded.");
        var note = await completion.Task;
        Assert.Equal(0, note.IdleSeconds);
        Assert.Equal(["stop", "resume"], cli.Commands);
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    [Fact]
    public async Task Notes_only_mode_observes_external_stops_without_issuing_timer_commands()
    {
        // Arrange
        var cli = new ControlCliDouble();
        var clock = new ControlClock();
        var completion = new TaskCompletionSource<TimerStopNote>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var service = new HubstaffControlService(new HubstaffControlOptions(),
            notes: new TimerNotesOptions { Enabled = true }, cli: cli, clock: clock,
            onNote: note => completion.TrySetResult(note));

        // Act
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        while (!service.NotesAvailable)
        {
            await Task.Delay(10, timeout.Token);
        }

        cli.Tracking = false;
        clock.Advance(5);
        var note = await completion.Task.WaitAsync(timeout.Token);

        // Assert
        Assert.Equal("42", note.ProjectId);
        Assert.Equal(0, note.IdleSeconds);
        Assert.Empty(cli.Commands);
    }

    [Fact]
    public async Task Disabled_notes_do_not_add_status_requests()
    {
        // Arrange
        var cli = new ControlCliDouble();
        var controller = new HubstaffIdleController(cli, () => new IdleObservation(TimeSpan.Zero, 1));
        await using var service = new HubstaffControlService(new HubstaffControlOptions { Enabled = true },
            controller: controller, cli: cli);

        // Act
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));

        while (service.Snapshot().State != HubstaffControlState.WaitingForIdle)
        {
            await Task.Delay(10, timeout.Token);
        }

        // Assert
        Assert.Equal(0, cli.Calls);
        Assert.Empty(cli.Commands);
    }

    [Fact]
    public async Task Database_snapshot_preserves_notes_and_diagnostics_exclude_their_text()
    {
        // Arrange
        using var source = new TestWorkspace();
        using var target = new TestWorkspace();
        var note = new TimerStopNote("synthetic-private", Now, "project", null, Reason: "NOTE-PRIVATE-CANARY");
        new TimerNoteStore(source.Root).Add(note);
        new SettingsStore(source.Root).Save(new AppSettings
        {
            TimerNotes = new TimerNotesOptions { Reasons = ["PRESET-PRIVATE-CANARY"] }
        });
        using var log = new DiagnosticLog(Path.Combine(source.Root, "diagnostics"));
        log.Write(DiagnosticComponent.Storage, DiagnosticEvent.SettingsSaved);
        var export = Path.Combine(source.Root, "export.zip");

        // Act
        await DataSnapshot.CreateAsync(source.Root, target.Root, default, null);
        log.Export(export);
        using var archive = ZipFile.OpenRead(export);
        var content = string.Join("\n", archive.Entries.Select(item =>
        {
            using var reader = new StreamReader(item.Open());
            return reader.ReadToEnd();
        }));

        // Assert
        Assert.Equal(note, Assert.Single(new TimerNoteStore(target.Root).Snapshot()));
        Assert.DoesNotContain("NOTE-PRIVATE-CANARY", content);
        Assert.DoesNotContain("PRESET-PRIVATE-CANARY", content);
        Assert.DoesNotContain(archive.Entries, entry => entry.FullName.Contains("viewer.db", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Idle_controller_announces_only_confirmed_stops_once()
    {
        // Arrange
        var cli = new ControlCliDouble();
        var controller = new HubstaffIdleController(cli, () => new IdleObservation(TimeSpan.FromMinutes(2), 1));
        var stops = new List<int>();
        controller.IdleStopped += (_, seconds) => stops.Add(seconds);

        // Act
        await controller.StepAsync(new HubstaffControlOptions { Enabled = true, IdleSeconds = 61 }, default);
        await controller.StepAsync(new HubstaffControlOptions { Enabled = true, IdleSeconds = 61 }, default);

        // Assert
        Assert.Equal([61], stops);
        Assert.Equal(["stop"], cli.Commands);
    }

    [Fact]
    public async Task Unconfirmed_stop_does_not_create_an_idle_note()
    {
        // Arrange
        var cli = new ControlCliDouble();
        cli.AfterStop = () => cli.Tracking = true;
        var controller = new HubstaffIdleController(cli, () => new IdleObservation(TimeSpan.FromMinutes(2), 1));
        var stops = new List<int>();
        controller.IdleStopped += (_, seconds) => stops.Add(seconds);

        // Act
        await controller.StepAsync(new HubstaffControlOptions { Enabled = true }, default);

        // Assert
        Assert.Empty(stops);
    }

    private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
    private static readonly HubstaffTrackingStatus Active = new(true, "synthetic-project", "synthetic-task");
    private static readonly HubstaffTrackingStatus Stopped = new(false, null, null);

    [Fact]
    public void Startup_and_repeated_stopped_observations_do_not_create_notes()
    {
        // Arrange
        var tracker = new TimerNoteTracker();
        tracker.SetEnabled(true);

        // Act
        var first = tracker.Observe(Stopped, Now);
        var second = tracker.Observe(Stopped, Now.AddSeconds(5));

        // Assert
        Assert.Null(first);
        Assert.Null(second);
    }

    [Fact]
    public void External_stop_creates_one_unknown_note_for_the_previous_selection()
    {
        // Arrange
        var tracker = new TimerNoteTracker();
        tracker.SetEnabled(true);
        tracker.Observe(Active, Now);

        // Act
        var note = tracker.Observe(Stopped, Now.AddSeconds(5));
        var repeated = tracker.Observe(Stopped, Now.AddSeconds(10));

        // Assert
        Assert.NotNull(note);
        Assert.Equal("synthetic-project", note.ProjectId);
        Assert.Equal("synthetic-task", note.TaskId);
        Assert.Equal(Now.AddSeconds(5), note.StoppedUtc);
        Assert.Equal(string.Empty, note.Reason);
        Assert.Equal(0, note.IdleSeconds);
        Assert.Null(repeated);
    }

    [Fact]
    public void Confirmed_idle_stop_is_not_recorded_again_as_unknown()
    {
        // Arrange
        var tracker = new TimerNoteTracker();
        tracker.SetEnabled(true);
        tracker.Observe(Active, Now);

        // Act
        var note = tracker.IdleStopped(Active, 123, Now.AddSeconds(1));
        var polled = tracker.Observe(Stopped, Now.AddSeconds(5));

        // Assert
        Assert.NotNull(note);
        Assert.Equal(123, note.IdleSeconds);
        Assert.Equal(Active.ProjectId, note.ProjectId);
        Assert.Null(polled);
    }

    [Fact]
    public void Internal_restart_is_ignored_but_a_later_external_stop_is_recorded()
    {
        // Arrange
        var tracker = new TimerNoteTracker();
        tracker.SetEnabled(true);
        tracker.Observe(Active, Now);

        // Act
        tracker.IgnoreStop(Active);
        var internalStop = tracker.Observe(Stopped, Now.AddSeconds(1));
        tracker.Observe(Active, Now.AddSeconds(2));
        var externalStop = tracker.Observe(Stopped, Now.AddSeconds(7));

        // Assert
        Assert.Null(internalStop);
        Assert.NotNull(externalStop);
    }

    [Fact]
    public void Disabled_and_reenabled_tracking_start_with_a_new_baseline()
    {
        // Arrange
        var tracker = new TimerNoteTracker();
        tracker.SetEnabled(true);
        tracker.Observe(Active, Now);

        // Act
        tracker.SetEnabled(false);
        var disabled = tracker.IdleStopped(Active, 60, Now.AddSeconds(1));
        tracker.SetEnabled(true);
        var reenabled = tracker.Observe(Stopped, Now.AddSeconds(5));

        // Assert
        Assert.Null(disabled);
        Assert.Null(reenabled);
    }

    [Fact]
    public void Local_notes_survive_reload_and_can_be_edited_and_skipped()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var store = new TimerNoteStore(workspace.Root);
        var note = new TimerStopNote("synthetic-stop", Now, "project", "task");

        // Act
        store.Add(note);
        store.Add(note);
        var reloaded = new TimerNoteStore(workspace.Root);
        reloaded.SetReason(note.Id, "Lunch — synthetic note");
        var saved = new TimerNoteStore(workspace.Root).Snapshot();
        reloaded.Delete(note.Id);

        // Assert
        Assert.Single(saved);
        Assert.Equal("Lunch — synthetic note", saved[0].Reason);
        Assert.Equal(Now, saved[0].StoppedUtc);
        Assert.Empty(new TimerNoteStore(workspace.Root).Snapshot());
    }

    [Fact]
    public void Notes_options_survive_reload_without_enabling_timer_control()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var store = new SettingsStore(workspace.Root);

        // Act
        store.Save(new AppSettings
        {
            TimerNotes = new TimerNotesOptions
            {
                Enabled = true, ShowPrompt = false, PromptSeconds = 25, Reasons = ["Lunch", "Finished"]
            }
        });
        var saved = new SettingsStore(workspace.Root).Current;

        // Assert
        Assert.True(saved.TimerNotes.Enabled);
        Assert.False(saved.TimerNotes.ShowPrompt);
        Assert.Equal(25, saved.TimerNotes.PromptSeconds);
        Assert.Equal(["Lunch", "Finished"], saved.TimerNotes.Reasons);
        Assert.False(saved.HubstaffControl.Enabled);
    }

    [Fact]
    public void Serialized_presets_retain_value_equality_for_application_settings()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        var expected = new AppSettings
        {
            TimerNotes = new TimerNotesOptions { Enabled = true, Reasons = ["Lunch", "Finished"] }
        };

        // Act
        new SettingsStore(workspace.Root).Save(expected);
        var actual = new SettingsStore(workspace.Root).Current;

        // Assert
        Assert.Equal(expected, actual);
        Assert.Equal(expected.TimerNotes.GetHashCode(), actual.TimerNotes.GetHashCode());
    }

    [Fact]
    public void Invalid_note_options_are_disabled_without_discarding_other_settings()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        workspace.Json("settings.json", new AppSettings
        {
            NotificationsEnabled = false,
            TimerNotes = new TimerNotesOptions { Enabled = true, PromptSeconds = 0 }
        });

        // Act
        var store = new SettingsStore(workspace.Root);

        // Assert
        Assert.False(store.Current.TimerNotes.Enabled);
        Assert.False(store.Current.NotificationsEnabled);
        Assert.NotNull(store.InitialWarning);
    }
}
