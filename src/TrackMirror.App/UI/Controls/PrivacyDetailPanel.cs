// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Constants;
using TrackMirror.Models;
using TrackMirror.Privacy;
using TrackMirror.Privacy.Installation;
using TrackMirror.Privacy.Models;
using TrackMirror.UI.Theme;

namespace TrackMirror.UI.Controls;

internal sealed class PrivacyDetailPanel : UserControl
{
    private readonly Label _summary = UiTheme.Label(PrivacyText.Unscanned, 9.5f, true);
    private readonly ScrollableTextField _findings = new()
    {
        Dock = DockStyle.Fill,
        BackColor = UiTheme.Surface,
        ForeColor = UiTheme.Muted,
        TabStop = false,
        MinimumSize = new Size(0, 80),
        Margin = new Padding(0, 0, 0, 12)
    };
    private readonly ActionButton _scan = new(PrivacyText.ScanSelected) { Width = 128, Height = 32 };
    private readonly ActionButton _rescan = new(PrivacyText.RescanSelected) { Width = 136, Height = 32 };
    private readonly ActionButton _acknowledge = new(PrivacyText.Acknowledge) { Width = 144, Height = 32 };
    private readonly ActionButton _dismiss = new(PrivacyText.FalsePositive) { Width = 136, Height = 32 };
    private readonly ActionButton _reset = new(PrivacyText.ResetDisposition) { Width = 144, Height = 32 };
    private readonly FlowLayoutPanel _actions = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Margin = Padding.Empty
    };
    private string? captureId;

    internal event Action<string, bool>? ScanRequested;

    internal event Action<string, PrivacyDisposition>? DispositionRequested;

    internal PrivacyDetailPanel()
    {
        Dock = DockStyle.Fill;

        BackColor = UiTheme.Surface;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Margin = Padding.Empty
        };

        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));

        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        foreach (var button in new[] { _scan, _rescan, _acknowledge, _dismiss, _reset })
        {
            button.MinimumSize = button.Size;

            button.Margin = new Padding(0, 0, 12, 8);

            _actions.Controls.Add(button);
        }

        root.Controls.Add(_summary, 0, 0);

        root.Controls.Add(_findings, 0, 1);

        root.Controls.Add(_actions, 0, 2);

        Controls.Add(root);

        SizeChanged += (_, _) => UpdateMinimumHeight();

        _actions.SizeChanged += (_, _) => UpdateMinimumHeight();

        DpiChangedAfterParent += (_, _) => UpdateMinimumHeight();

        _scan.Click += (_, _) =>
        {
            if (captureId != null)
            {
                ScanRequested?.Invoke(captureId, false);
            }
        };

        _rescan.Click += (_, _) =>
        {
            if (captureId != null)
            {
                ScanRequested?.Invoke(captureId, true);
            }
        };

        foreach (var (button, disposition) in new[]
        {
            (_acknowledge, PrivacyDisposition.Acknowledged),
            (_dismiss, PrivacyDisposition.FalsePositive),
            (_reset, PrivacyDisposition.Pending)
        })
        {
            button.Click += (_, _) =>
            {
                if (captureId != null)
                {
                    DispositionRequested?.Invoke(captureId, disposition);
                }
            };
        }

        SetResult(null, null, new AppSettings());
    }

    internal void SetResult(string? id, PrivacyScanRecord? result, AppSettings settings)
    {
        captureId = id;

        var selected = PrivacyCoverage.Selected(settings);

        var available = (settings.HasPrivacyCategories || result is { Categories.Count: > 0 }) && id != null
            && result?.State is not (PrivacyScanState.Queued or PrivacyScanState.Scanning);

        _scan.Enabled = available && (result == null || result.NeedsCheck);

        _rescan.Visible = result is { Categories.Count: > 0 };

        _rescan.Enabled = available;

        var hasFindings = result is { Findings.Count: > 0 };

        _acknowledge.Visible = _dismiss.Visible = hasFindings && result!.Disposition == PrivacyDisposition.Pending;

        _reset.Visible = hasFindings && result!.Disposition != PrivacyDisposition.Pending;

        var working = result?.State is PrivacyScanState.Queued or PrivacyScanState.Scanning;

        _summary.Text = !settings.HasPrivacyCategories && result == null ? PrivacyText.NoCategories
            : !working && result is { NeedsCheck: true } ? PrivacyText.Stale : PrivacyText.State(result);

        _summary.ForeColor = result is { Findings.Count: > 0 } ? UiTheme.Amber : UiTheme.AccentText;

        var lines = new List<string>();

        var displayedFailures = new HashSet<string>(StringComparer.Ordinal);

        foreach (var category in Enum.GetValues<PrivacyScanCategory>())
        {
            var outcome = result?.Categories.FirstOrDefault(value => value.Category == category);

            var version = PrivacyCoverage.Version(category, settings.PrivacyModel);

            var status = PrivacyText.Coverage(outcome, version);

            if (!selected.Contains(category))
            {
                status = PrivacyText.Disabled + PrivacyText.DetailSeparator + status;
            }

            lines.Add(PrivacyText.Category(category) + PrivacyText.DetailSeparator + status);

            if (selected.Contains(category) && outcome?.Version == version
                && outcome.FailureCode.Length > 0)
            {
                if (displayedFailures.Add(outcome.FailureCode))
                {
                    lines.Add(PrivacyText.Failure(outcome.FailureCode));
                }
            }
        }

        var diagnostics = result?.Categories.Where(category => selected.Contains(category.Category)
            && category.Version == PrivacyCoverage.Version(category.Category, settings.PrivacyModel))
            .SelectMany(category => category.Diagnostics).DistinctBy(value => value.RequestId).ToList() ?? [];

        if (diagnostics.Count > 0)
        {
            var prompt = diagnostics.All(value => value.PromptTokens.HasValue)
                ? diagnostics.Sum(value => (long)value.PromptTokens!.Value).ToString() : PrivacyText.MetricUnavailable;

            var reply = diagnostics.All(value => value.ReplyTokens.HasValue)
                ? diagnostics.Sum(value => (long)value.ReplyTokens!.Value).ToString() : PrivacyText.MetricUnavailable;

            lines.Add(string.Empty);

            lines.Add(string.Format(PrivacyText.DiagnosticsFormat, diagnostics.Count,
                diagnostics.Sum(value => (long)value.RequestBytes), prompt, reply));
        }

        lines.Add(string.Empty);

        var descriptions = result?.Findings.Select(finding => PrivacyText.Category(finding.Category)
            + PrivacyText.DetailSeparator + PrivacyText.Reason(finding.Reason)).Distinct().ToList() ?? [];

        lines.AddRange(descriptions);

        if (settings.HasPrivacyCategories && result is { NeedsCheck: false, State: PrivacyScanState.Completed }
            && descriptions.Count == 0)
        {
            lines.Add(PrivacyText.NoFindings);
        }

        if (result?.State is PrivacyScanState.Partial or PrivacyScanState.Failed)
        {
            lines.Add(PrivacyText.PartialHint);

            if (result.FailureCode.Length > 0 && displayedFailures.Add(result.FailureCode))
            {
                lines.Add(PrivacyText.Failure(result.FailureCode));
            }
        }

        if (result?.Disposition == PrivacyDisposition.Acknowledged)
        {
            lines.Add(PrivacyText.Acknowledged);
        }
        else if (result?.Disposition == PrivacyDisposition.FalsePositive)
        {
            lines.Add(PrivacyText.Dismissed);
        }

        _findings.Text = string.Join(Environment.NewLine, lines);

        UpdateMinimumHeight();
    }

    private void UpdateMinimumHeight()
    {
        var actionsHeight = _actions.GetPreferredSize(new Size(Math.Max(1, ClientSize.Width), 0)).Height;

        MinimumSize = new Size(0, (int)(28 * DeviceDpi / 96f)
            + _findings.MinimumSize.Height + _findings.Margin.Vertical + actionsHeight);
    }
}
