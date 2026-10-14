// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;

namespace TrackMirror.UI.Controls;

internal sealed class CaptureFilterSelector : FlowLayoutPanel
{
    private readonly Dictionary<CaptureFilter, FilterButton> _buttons = [];
    private bool updating;

    internal CaptureFilter SelectedFilter { get; private set; }

    internal event Action? FilterChanged;

    internal CaptureFilterSelector()
    {
        AutoSize = true;

        AutoSizeMode = AutoSizeMode.GrowAndShrink;

        WrapContents = false;

        Margin = Padding.Empty;

        AccessibleRole = AccessibleRole.Grouping;

        AccessibleName = UiText.HistoryFilters;

        foreach (var (filter, caption, width) in new[]
        {
            (CaptureFilter.All, UiText.AllFilter, 76),
            (CaptureFilter.Flagged, UiText.FlaggedFilter, 126),
            (CaptureFilter.Unread, UiText.UnreadFilter, 136),
            (CaptureFilter.PrivacyRisks, PrivacyText.RiskFilter, 110)
        })
        {
            var button = new FilterButton(caption)
            {
                Dock = DockStyle.None,
                Size = new Size(width, 38),
                Margin = new Padding(0, 0, 8, 0),
                AccessibleRole = AccessibleRole.RadioButton,
                AccessibleName = caption,
                AutoCheck = false,
                Checked = filter == CaptureFilter.All,
                TabStop = filter == CaptureFilter.All
            };

            _buttons.Add(filter, button);

            Controls.Add(button);

            button.Click += (_, _) => SetFilter(filter);

            button.CheckedChanged += (_, _) =>
            {
                if (updating)
                    return;

                SetFilter(button.Checked ? filter : SelectedFilter);
            };

            button.KeyDown += (_, e) =>
            {
                var next = e.KeyCode switch
                {
                    Keys.Right or Keys.Down => ((int)SelectedFilter + 1) % _buttons.Count,
                    Keys.Left or Keys.Up => ((int)SelectedFilter + _buttons.Count - 1) % _buttons.Count,
                    Keys.Home => 0,
                    Keys.End => _buttons.Count - 1,
                    _ => -1
                };

                if (next < 0)
                    return;

                SetFilter((CaptureFilter)next);

                _buttons[SelectedFilter].Focus();

                e.Handled = e.SuppressKeyPress = true;
            };

            button.PreviewKeyDown += (_, e) =>
            {
                if (e.KeyCode is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End)
                {
                    e.IsInputKey = true;
                }
            };
        }
    }

    internal void SetFilter(CaptureFilter filter)
    {
        if (!_buttons.ContainsKey(filter))
            throw new ArgumentOutOfRangeException(nameof(filter));

        var changed = SelectedFilter != filter;

        SelectedFilter = filter;

        updating = true;

        foreach (var (mode, button) in _buttons)
        {
            button.Checked = mode == filter;

            button.TabStop = button.Checked;
        }

        updating = false;

        if (changed)
        {
            FilterChanged?.Invoke();
        }
    }
}
