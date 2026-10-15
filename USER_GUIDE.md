# User guide

## Install, update and uninstall

Choose the Windows x64 setup EXE from Releases for a normal per-user installation, or the ZIP for
portable use. Both include the desktop runtime. The installer needs no administrator rights and adds
a Start menu shortcut; the desktop shortcut is optional.

Exit application from the tray before running an update or uninstalling. Setup asks you to exit if the
viewer is still running. Its separate startup observer stops automatically; uninstall also removes this
installation's startup shortcut. A newer installer updates the
same installation. Remove the program through Windows Installed apps. History, settings, backups and
downloaded models are retained. Installing separately from an existing portable copy does not select
its data automatically; use the data-folder or backup/restore settings to transfer it.

## Review and navigation

The viewer reads `<account>/screens` below the configured Hubstaff source root, saves separate JPGs,
and stores account/filename/timestamps/flags/reviewed state. Timer control is optional and off by default; the viewer does not
remove Hubstaff screenshots. Open a tile for details and use Back to gallery to return. Reviewed marks
an image read; Flag is independent. Opening from a notification marks that image read; dismissing one does not.

Choose one mode: All, Flagged, Unread or AI risks. Calendar start/end dates are inclusive; the same date
twice selects one day. A partially selected range retains the old filter when closed. All dates clears it.
Use a thumbnail checkbox or Space to select screenshots; Enter opens the focused image. Select all results
includes every screenshot matching the current filter and dates, including items below the viewport.
Changing the filter or dates clears selection; newly arriving screenshots are not selected automatically.
The selection bar offers Mark as read, AI scan and Cancel. Mark as read saves the selected unread items
in one transaction and offers Undo for 10 seconds. Existing read states and flags are preserved.
AI scan lets you choose categories for that run without changing saved settings. Completed categories
are reused unless Recheck completed categories is selected. Manual findings remain visible even when
that category is not selected in Settings. Additional actions move into More actions in narrow windows.

Open full size fits the entire image. Use Fit, 100%, plus/minus, Ctrl+wheel, dragging and scrollbars.
Ctrl+0 fits, Ctrl+1 selects native pixels and Escape closes the viewer.

## Settings and tray

Preview notifications can be disabled while copying continues. Auto-hide defaults to 10 seconds
(range 1–3600); each new capture restarts it. Notifications appear without taking keyboard focus.
Close history to continue in the tray; Exit application stops the viewer.

Start with Hubstaff is off by default. It creates a Windows Startup shortcut and hidden observer for
`HubstaffClient.exe` in the same session. A manual viewer exit does not immediately relaunch it for the
same Hubstaff process run. Language offers English (default) and Russian; exit from the tray and
restart to apply a selection. Dates remain in the local time zone.

## Hubstaff timer control

This optional feature is disabled by default. Settings offers two modes:

- **Pause until activity returns:** after the configured idle threshold, stop an actively tracking timer.
  Resume when new input is observed, only for a pause created by this viewer session with the same selection.
- **Periodic stop/resume while idle:** once the idle threshold is reached, start a separate countdown.
  At each configured interval, stop and immediately resume an actively tracking timer. New input resets
  the countdown; timers already stopped before the operation are not started.

Both intervals are in seconds (1–86400) and saved in `settings.json`. The CLI path is editable; leave it
empty for automatic discovery, use Detect installed CLI, or Browse to the executable in a custom location.
Discovery checks the running Hubstaff client's directory and Windows-known application folders, without
assuming a fixed drive or user name. No shell is used and no custom command arguments are accepted.

Enable **Scripted control** in Hubstaff. The first query may show a permission dialog; choose **Always allow**.
See the [official scripted-control documentation](https://support.hubstaff.com/what-is-scripted-control/).
The viewer queries status and checks project/task identity before resuming. Unsupported status formats,
changed selection, denied commands or timeouts suspend control; check Hubstaff before using Retry control.
Retry after an error does not reclaim an earlier pause. A human action and a CLI command are not atomic;
check the timer after an interrupted command or an external change during control.

Windows supplies the current session's last-input timestamp, without capturing key contents or generating
input. This idle measurement is separate from Hubstaff's activity percentages. The feature does not run
when the viewer is closed. Disabling it, changing mode/path or restarting the viewer does not resume an
existing pause. A confirmed periodic stop finishes its immediate resume with a bounded 15-second budget,
including during shutdown; pending countdowns are cancelled. Failed resume may leave the timer stopped.

## Activity

Activity shows locally received Hubstaff intervals by account/day. The viewer must run to collect
records and cannot recover older files removed by Hubstaff or download server history. Percentages
are weighted by tracked seconds. Blank areas mean no records, not zero activity. An interval belongs
to the day it starts locally; growing records replace prior versions.

The source is `TrackedActivity.xml`. The viewer does not install input hooks, record keystrokes or
persist application/URL lists or Hubstaff project/task identifiers from those records.

## Local AI

Choose a model in Settings and select Download and install. A private Ollama runtime and pinned model
are downloaded separately. Cancel stops the operation; tray exit cancels owned installation work.
Interrupted downloads can resume. Remove model removes the selection while retaining shared blobs
needed by other installed models. Substantial disk space and available RAM/VRAM are required.

| Model | Approximate download excluding Ollama |
| --- | --- |
| Qwen3-VL 2B Instruct | 1.9 GB |
| Qwen3-VL 4B Instruct (default) | 3.3 GB |
| Qwen3-VL 8B Instruct | 6.1 GB |
| Gemma 3 4B | 3.4 GB |

Confidential-data checks default on; games, personal chats and automatic scanning default off.
Check remaining runs missing selected categories; Rescan repeats all categories selected in settings, including newly enabled ones.
If no categories are selected, Rescan repeats the categories in the saved result.
For a batch, select thumbnails or all filtered results and choose AI scan categories.
Completed categories survive selection changes; model/analysis-version changes can
make them outdated. Queues and confirmed checkpoints survive interruption. Partial scans show saved
findings and safe errors. AI results lets you confirm a risk, mark a false positive or reset a decision.

Dense OCR is sent in compact UTF-8-budgeted batches; long blocks overlap and keep their original IDs.
Context errors retry smaller batches, and confirmed findings are merged. Results distinguish incomplete
OCR, model uncertainty, context failures and safety limits. Numeric request/token diagnostics survive
restart; unavailable provider counters are shown as unavailable. Text and raw answers are not logged.
Successful batch checkpoints survive cancellation. Request bytes include the image and JSON payload;
token totals are Ollama's reported input/output counts across attempts, including retries. OCR counts
include overlapping fragments. The 128-request limit stops repeated splitting with an explicit partial
result; the context remains 8192 tokens. OCR engine limits are reported separately from model failures.
After this update, Check remaining refreshes confidential-data coverage; completed game/chat checks remain.

These actions do not delete images or affect Hubstaff uploads. AI can miss risks or misclassify content.
There is no API-key/cloud provider. See the third-party notices for model terms.

## Data and backups

Settings shows the active folder. SQLite `viewer.db` stores metadata; JPGs live in `captures/<account>`;
`settings.json` stores preferences. Imported legacy JSON is retained and can contain older private metadata.

Move data selects an empty local folder, pauses writes, copies/verifies data and reopens it. Source
files remain with a validated redirect; old launch commands/observers follow it. Incoming screenshot
handles remain queued during paused writes. Network/linked/nested/occupied targets are refused.
Create backup writes an unencrypted ZIP outside data/app/Hubstaff folders: database snapshot, settings,
legacy JSON, available JPGs and SHA-256 manifest. Models/binaries are excluded.

Restore verifies a viewer ZIP and switches to another empty folder, preserving the current dataset.
Restored settings, including language and retention, become active. Cancel or tray exit stops
preparation before commitment. If final switching fails, a verified copy may remain for recovery;
choose another empty folder to retry.

Moving the executable creates a new installation identity. Use `--data-root` to retain an existing
dataset. Missing selected folders produce an error instead of silently replacing history. Reconnect
the disk or specify a valid existing folder. Keep redirects and LocalAppData preferences when updating.

## Cleanup and updates

Automatic cleanup defaults off; retention defaults to 30 days (1–3650). Keep flagged and Keep unread
both default on; queued/scanning captures are protected. Enabled checks run at startup and hourly.
Clean now confirms eligible captures and works while auto-cleanup is off. JPGs and related AI records
are removed, while activity, backups, models and Hubstaff originals stay. Locked files are retried
using deletion intents; cancellation does not undo removals. Restore an earlier backup to recover history.

Exit from the tray before replacing binaries and create a backup first. Preserve the selected data
folder, preferences, `privacy-runtime` and the `ru` satellite directory. Keep an older build until
verification. If extracting to a new application directory, explicitly point it to your existing data.

For missing previews, check source paths and available files; Hubstaff originals can disappear before
being opened. For AI failures, check installation, selected model and memory. For storage failures,
check access/free space/removable disks. Startup diagnostics may be written to `startup-error.log` in
an existing data folder. Treat diagnostics as private. Report synthetic/redacted examples only;
read [PRIVACY.md](PRIVACY.md) and [SECURITY.md](SECURITY.md) before sharing files.

