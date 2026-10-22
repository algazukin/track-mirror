# Changelog

Versions here match tags and release archives.

## Unreleased

- Add project, author and MPL-2.0 links in Settings, with manual GitHub update checks and saved
  Stable/Pre-release channels. Pre-release is selected by default.
- Optional floating screenshot countdown from local Hubstaff logs, with saved position, quiet final-second
  highlighting, rounded corners, adjustable scale and background opacity, and explicit waiting/unavailable
  states. No process-memory access or timer commands.

- Include newly selected AI categories when rescanning a saved result.
- Update SQLite and its native dependencies, Microsoft.NET.Test.Sdk and the xUnit VSTest adapter.
- Rename the application, solution, namespaces, installation and release assets to TrackMirror.

- Keep disabled checkbox captions readable on the dark theme across settings and gallery actions.
- Refresh missing screenshot-copy warnings after recovery and preserve unrelated error messages.

- Per-user Windows x64 installer, upgrade/uninstall support and installer assets in CI and draft Releases.
- Preserve personal data and downloaded models when uninstalling; validate packaged native x64 libraries.
- Add thumbnail selection, filtered bulk review with Undo, and per-run AI checks with category selection.
- Optional idle-triggered Hubstaff CLI timer control with two modes, configurable timing and executable path.
- Keep Hubstaff timer settings editable after initial window layout and display scaling.
- Size settings cards to their content and keep AI result actions spaced and reachable in smaller windows.

- Complete OCR batching with overlapping evidence IDs, adaptive context retries and merged findings.
- Durable batch checkpoints and numeric request/token diagnostics in SQLite schema 3.
- Separate incomplete OCR, model uncertainty, context and request-limit messages.
- MPL-2.0 project licensing and corresponding source archives.
- English public user/contributor/privacy/security/release documentation.
- Windows CI, clean self-contained ZIP packaging and checksum validation.

## 1.0.0

Initial release candidate; publication still requires maintainer verification.

- Local screenshot copies, gallery, flags, reviewed state and date-range filters.
- Tray controls, focus-preserving previews, timeout and optional Hubstaff startup.
- Full-image fit/zoom/panning and shared scrollbars.
- Local activity intervals and daily graphs.
- Optional AI categories, durable queue, missing checks and pinned model management.
- SQLite, recoverable JSON import, folder moves, backup/restore and optional retention.
- English/Russian UI with English default.
