# Contributing

Use English for documents, issues, pull requests, identifiers and comments. Localisation resources
and synthetic multilingual fixtures may contain other languages.

## Setup and verification

Use Windows x64, PowerShell 7 and .NET 10 SDK. WinForms/native OCR require Windows verification.
`global.json` selects the SDK policy; `NuGet.config` uses the public feed only.

```powershell
dotnet restore .\TrackMirror.sln --locked-mode
dotnet build .\TrackMirror.sln -c Release --no-restore -m:1 -nr:false
pwsh -File .\scripts\Test-Repository.ps1
```

Use a separate fixture data folder and empty source for manual runs. Never test migration, cleanup,
transfers or model deletion against someone else's real history.

The integration project uses temporary SQLite databases, synthetic images/XML and hidden STA windows.
It needs no Hubstaff credentials or downloaded AI model. One import-interruption scenario starts and
terminates its own synthetic child. Run tests when your environment/project instructions permit it:

```powershell
dotnet test .\TrackMirror.sln -c Release --no-restore -m:1 -nr:false
```

Every test method includes `// Arrange`, `// Act` and `// Assert` at its actual phase boundaries.
Tests are grouped by feature; shared fixtures live in `Infrastructure`.

Storage regressions use real file watchers, file locks, temporary SQLite databases and ZIP archives.
They cover partial/corrupt images, duplicate events, schema upgrades, cancelled backup/restore,
failed folder switching, settings write failures and cleanup protections. Failure injection uses
synthetic SQLite triggers and synchronous progress callbacks, so failures occur at known boundaries.
The corrupt-image retry test takes about ten seconds to exercise the monitor's actual retry limit.
Filter the data-safety block with:

```powershell
dotnet test .\TrackMirror.sln -c Release --no-restore -m:1 -nr:false `
    --filter 'FullyQualifiedName~Monitoring|FullyQualifiedName~Storage'
```

UI lifecycle tests run the same test assembly as a bounded child process with a real WinForms message
loop, isolated data/startup folders, a fake analyzer and an in-memory HTTP handler. Tray exit is checked
while scanning or downloading; cancellation must finish while the UI still dispatches callbacks.
Layout checks use separate processes for 96 DPI and the desktop's actual DPI, in English and Russian.
To cover other native DPI values, run the suite in Windows sessions configured for those values.

Activation, focus and owned-window regressions are explicitly skipped during ordinary runs. On a
dedicated interactive Windows desktop, opt in with:

```powershell
$env:HUBSTAFF_VIEWER_DESKTOP_TESTS = '1'
try {
    dotnet test .\TrackMirror.sln -c Release --no-restore -m:1 -nr:false
} finally {
    Remove-Item Env:HUBSTAFF_VIEWER_DESKTOP_TESTS
}
```

These scenarios open only synthetic windows, but can temporarily move keyboard focus. They need an
unlocked interactive session; headless/service runners cannot establish focus behavior reliably.

CI runs the default synthetic suite on Windows; desktop activation cases require explicit opt-in.
Report what ran, was skipped or could not run; a build is not proof
of test/hosted workflow success. Add meaningful storage/queue/cancellation/privacy coverage. Check UI
layout, focus, keyboard access and both languages. Global culture tests use a non-parallel collection.

## Code and layout

- One component per source file; place it in the appropriate existing directory.
- Prefer readable local logic. Extract helpers for real duplication/domain concepts, not predicate chains.
- Separate operations with blank lines and keep production lines within 120 characters.
- Use control-flow braces except short return/continue/break/throw guards.
- Avoid Trim calls unless a demonstrated requirement needs them.
- Put UI text in resources; retain invariant protocol/filesystem constants.
- Keep English/Russian resource keys and format arguments in sync; English is the fallback.
- Preserve MPL-2.0 notices; do not apply project licensing to third-party assets.

Follow `.editorconfig`. Public documents live in the root. Internal plans/specs stay in ignored `docs`,
not pull requests or public archives. Build output, datasets, diagnostics and downloaded models are local.

## Dependencies and releases

Versions and lock files are committed. After an intentional package change, restore without locked
mode, review lock updates, then check a locked restore. Preserve native/OCR files, locale assemblies
and upstream notices in releases. Use `scripts/Build-Release.ps1`, not an existing application/data folder.

## Pull requests

Explain the concrete problem/result, validation and material limits. Keep changes focused and preserve
unrelated work. Do not include real screenshots, databases, credentials, personal paths, binaries or
internal notes. Use synthetic/redacted bug reports. Collection/network/storage changes need privacy review.
Contributions are offered under MPL-2.0; see [LICENSE](LICENSE). For vulnerabilities, read [SECURITY.md](SECURITY.md).
