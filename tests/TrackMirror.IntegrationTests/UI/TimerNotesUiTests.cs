// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.


using System.Reflection;
using TrackMirror.Hubstaff;
using TrackMirror.Models;
using TrackMirror.Storage;
using TrackMirror.UI.Controls;
using TrackMirror.UI.Forms;
using TrackMirror.IntegrationTests.Infrastructure;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class TimerNotesUiTests
{
    [Fact]
    public void Reason_editor_grows_for_text_and_uses_themed_scrolling_only_for_overflow()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var host = new QuietHost();
            using var panel = new TimerNotesPanel();
            host.Controls.Add(panel);
            panel.SetEntries([new TimerStopNote("compact-editor", DateTime.UtcNow, null, null, Reason: "Break")]);
            host.Show();
            Application.DoEvents();
            var editor = UiControls.Field<TextBox>(panel, "_reason");

            // Act
            var initialHeight = editor.Parent!.Height;

            // Assert
            Assert.Equal(ScrollBars.None, editor.ScrollBars);
            var field = Assert.IsType<ScrollableTextField>(editor.Parent);
            var scrollbar = UiControls.Descendants<CaptureScrollBar>(field).Single();
            Assert.False(editor.ReadOnly);
            Assert.False(scrollbar.Visible);
            Assert.True(initialHeight <= editor.Font.Height * 2 + 16);

            // Act
            editor.Text = string.Join(Environment.NewLine, Enumerable.Repeat("Synthetic reason", 20));
            Application.DoEvents();

            // Assert
            Assert.True(field.Height > initialHeight);
            Assert.True(field.Height <= editor.Font.Height * 4 + 16);
            Assert.True(scrollbar.Visible);

            // Act
            editor.Text = "Break";
            Application.DoEvents();

            // Assert
            Assert.Equal(initialHeight, field.Height);
            Assert.False(scrollbar.Visible);
        });
    }

    [Fact]
    public void Quick_reasons_fit_their_lines_and_scroll_only_when_the_editor_is_full()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var host = new QuietHost();
            using var panel = new TimerNotesSettingsPanel(new TimerNotesOptions());
            host.Controls.Add(panel);
            host.Show();
            Application.DoEvents();
            var editor = UiControls.Field<TextBox>(panel, "_presets");

            // Act
            var initialHeight = editor.Parent!.Height;

            // Assert
            Assert.Equal(ScrollBars.None, editor.ScrollBars);
            var field = Assert.IsType<ScrollableTextField>(editor.Parent);
            var scrollbar = UiControls.Descendants<CaptureScrollBar>(field).Single();
            Assert.False(editor.ReadOnly);
            Assert.True(editor.AcceptsReturn);
            Assert.False(scrollbar.Visible);
            Assert.True(initialHeight <= editor.Font.Height * 5 + 16);

            // Act
            editor.Lines = Enumerable.Range(1, 12).Select(index => "Synthetic reason " + index).ToArray();
            Application.DoEvents();

            // Assert
            Assert.True(field.Height > initialHeight);
            Assert.True(field.Height <= editor.Font.Height * 7 + 16);
            Assert.True(scrollbar.Visible);
        });
    }

    [Fact]
    public void A_new_stop_does_not_replace_an_unsaved_reason_for_the_selected_note()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var panel = new TimerNotesPanel();
            var grid = UiControls.Field<DataGridView>(panel, "_grid");
            var editor = UiControls.Field<TextBox>(panel, "_reason");
            var note = new TimerStopNote("editing", DateTime.UtcNow, "project", null, Reason: "Saved reason");
            panel.SetEntries([note]);
            grid.CurrentCell = grid.Rows[0].Cells[0];
            editor.Text = "Unsaved user draft";

            // Act
            panel.SetEntries([note with { Id = "new-stop", StoppedUtc = note.StoppedUtc.AddMinutes(1) }, note]);

            // Assert
            Assert.Equal("editing", grid.CurrentRow!.Tag);
            Assert.Equal("Unsaved user draft", editor.Text);
        });
    }

    [Fact]
    public void Choosing_a_preset_updates_the_existing_note_and_skip_deletes_it()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        StaThread.Run(() =>
        {
            var store = new TimerNoteStore(workspace.Root);
            var note = new TimerStopNote("prompt-fixture", DateTime.UtcNow, "project", null);
            store.Add(note);
            using var prompt = new TimerNotePromptForm { Opacity = 0 };
            prompt.ReasonSelected += (id, reason) =>
            {
                store.SetReason(id, reason);
                return true;
            };
            prompt.Skipped += id =>
            {
                store.Delete(id);
                return true;
            };
            var options = new TimerNotesOptions { Enabled = true, Reasons = ["Synthetic lunch"] };

            // Act
            prompt.Present(note, options);
            UiControls.Descendants<Button>(prompt).Single(button => button.Text == "Synthetic lunch").PerformClick();

            // Assert
            Assert.Equal("Synthetic lunch", Assert.Single(store.Snapshot()).Reason);
            Assert.False(prompt.Visible);

            // Act
            prompt.Present(note, options);
            UiControls.Descendants<Button>(prompt).Single(button => button.Name == "timerNoteSkip").PerformClick();

            // Assert
            Assert.Empty(store.Snapshot());
            Assert.False(prompt.Visible);
        });
    }

    [Fact]
    public void Dismissing_the_prompt_keeps_unknown_and_custom_text_can_be_saved_later()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        StaThread.Run(() =>
        {
            var store = new TimerNoteStore(workspace.Root);
            var note = new TimerStopNote("custom-fixture", DateTime.UtcNow, "project", null);
            store.Add(note);
            using var prompt = new TimerNotePromptForm { Opacity = 0 };
            prompt.ReasonSelected += (id, reason) =>
            {
                store.SetReason(id, reason);
                return true;
            };

            // Act
            prompt.Present(note, new TimerNotesOptions());
            prompt.Close();

            // Assert
            Assert.False(prompt.Visible);
            Assert.Equal(string.Empty, Assert.Single(store.Snapshot()).Reason);

            // Act
            // Exercise handlers on a hidden form so synthetic typing cannot steal desktop focus.
            var other = UiControls.Descendants<Button>(prompt).Single(button => button.Name == "timerNoteOther");
            UiControls.Invoke(other, "OnClick", EventArgs.Empty);
            UiControls.Descendants<TextBox>(prompt).Single().Text = "Synthetic custom break";
            var save = UiControls.Field<Button>(prompt, "_save");
            UiControls.Invoke(save, "OnClick", EventArgs.Empty);

            // Assert
            Assert.Equal("Synthetic custom break", Assert.Single(store.Snapshot()).Reason);
        });
    }

    [Fact]
    public void Settings_expose_independent_enable_and_prompt_switches()
    {
        // Arrange
        StaThread.Run(() =>
        {
            using var panel = new SettingsPanel(new AppSettings(), false);

            // Act
            var switches = UiControls.Descendants<CheckBox>(panel).ToList();

            // Assert
            Assert.Contains(switches, control => control.Name == "timerNotesEnabled" && !control.Checked);
            Assert.Contains(switches, control => control.Name == "timerNotesShowPrompt");
            Assert.Contains(UiControls.Descendants<TextBox>(panel), control => control.Name == "timerNotesPresets");
        });
    }

    [Fact]
    public void History_exposes_a_notes_page_with_saved_local_entries()
    {
        // Arrange
        using var workspace = new TestWorkspace();
        StaThread.Run(() =>
        {
            using var form = new HistoryForm(new ScreenshotStore(workspace.Root));
            var method = typeof(HistoryForm).GetMethod("SetTimerNotes", BindingFlags.Instance | BindingFlags.NonPublic);

            // Act
            var grid = UiControls.Descendants<DataGridView>(form).FirstOrDefault(control => control.Name == "timerNotesHistory");

            // Assert
            Assert.NotNull(method);
            Assert.NotNull(grid);

            // Act
            method.Invoke(form, [new List<TimerStopNote>
            {
                new("fixture", new DateTime(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc), "project", null,
                    Reason: "Synthetic lunch")
            }]);

            // Assert
            Assert.Equal("Synthetic lunch", grid.Rows[0].Cells[1].Value);
        });
    }

    private sealed class QuietHost : Form
    {
        protected override bool ShowWithoutActivation => true;

        internal QuietHost()
        {
            Opacity = 0;
            ShowInTaskbar = false;
            ClientSize = new Size(900, 650);
            Font = new Font("Segoe UI", 9.5f);
        }
    }
}
