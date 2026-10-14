# Releasing

## GitHub setup

Create the target GitHub repository and copy the public source ZIP, not the old `.git` folder or ignored
local files. Workflows do not depend on an owner name or the existing GitLab remote. Enable Actions;
CI uses contents-read, and only the draft-upload job uses contents-write. Enable private vulnerability
reporting and your chosen branch protections after inspecting the first CI run. Action pins are full
commit SHAs; Dependabot proposes Actions/NuGet updates.

## Before a release

- Inspect a real Windows CI build/test result; local builds do not prove hosted workflow success.
- Review versions, lock files, dependencies/licenses, privacy changes and English public documents.
- Run `scripts/Test-Repository.ps1` and inspect installer and binary/source ZIP contents.
- Smoke-test fresh extraction without depending on a globally installed desktop runtime.
- Verify a fresh per-user installation, update in place and uninstall on a disposable Windows account/VM.
- Confirm uninstall retains data, backups and optional downloaded models.
- Check native OCR/image libraries, both languages, tray shutdown and storage/monitoring flows.
- Use synthetic datasets and copied upgrade fixtures; keep private data out of reports and archives.

## Local installer and archives

Use Windows x64, PowerShell 7, the .NET SDK selected by `global.json`, and
[Inno Setup 6.3 or newer](https://jrsoftware.org/isdl.php) for installer builds. The Windows GitHub runner
already includes Inno Setup. ZIP-only builds do not need the installer compiler.

```powershell
pwsh -File .\scripts\Build-Release.ps1 -Version 1.0.0-preview.1 -BuildInstaller
pwsh -File .\scripts\Export-Source.ps1 -Version 1.0.0-preview.1
```

Output defaults to ignored `artifacts/releases`. The installer and binary ZIP use the same validated,
self-contained Windows x64 payload, including runtime/licenses/localisation/OCR/native files.
Optional downloaded models are excluded. Packaging checks the architecture of the executable and
native runtime/OCR/image/SQLite libraries, along with the file inventory and hashes.
The source exporter uses Git's public file list and rejects tracked internal/private files. It contains
no `.git` or local history. Each EXE/ZIP has a checksum sidecar.

The compiler is found in PATH or the standard Inno Setup installation directories. For a portable
compiler, pass `-InnoSetupCompiler 'path\to\ISCC.exe'` to `Build-Release.ps1`. Omitting `-BuildInstaller`
creates only the portable ZIP. An existing versioned output is never overwritten.

The lower-level `scripts/Build-Installer.ps1 -ApplicationDirectory <validated-payload> -OutputDirectory <output>`
can package a previously published directory. Its optional `-CompilerPath` selects ISCC.exe explicitly;
version information comes from the validated `release-info.json`.

## Installation and update behavior

TrackMirror uses its own installation and data identity. Private preview builds made before the
public product name was chosen should be uninstalled before installing TrackMirror. Their data
folders remain on disk; use the data-folder controls or an explicit `--data-root` to reuse them.

The installer installs for the current user under `%LOCALAPPDATA%\Programs\TrackMirror`,
creates a Start menu shortcut and offers an optional desktop shortcut. It needs no administrator rights
and does not enable Hubstaff startup, timer control or AI automatically. Silent installation does not
launch the application. Exit the application from the tray before updating or uninstalling.

Setup and uninstall refuse to change files while this installation's viewer is running. They stop its
separate, read-only Hubstaff startup observer when needed. Uninstall removes only startup shortcuts
that target this installation; observers and shortcuts belonging to other copies are retained.

`installer/windows/Setup.iss` keeps a stable AppId and default directory. Keep these identifiers stable
when changing the product's display name, so updates continue to discover the previous installation.
Installation paths determine the existing per-installation data preference, so moving an installation
manually can select a different history. Use the application's data-folder transfer/restore instead.

Uninstall removes installer-owned files and shortcuts. It deliberately retains the LocalAppData data
profile, custom data folders, existing portable data, backups and downloaded AI models. Remove those
separately only when desired. Installing alongside an existing portable copy does not automatically
import that copy's data; use its data-folder transfer or backup/restore tools.

This release targets x64. A 32-bit installer is not enough to create x86 support: the pinned ONNX Runtime
1.29.0 package has no Windows x86 native library, and the bundled Ollama download targets x64.
Do not publish an x86 asset until those dependencies and the complete runtime flow support it.

## Tag-based draft Release

After committing verified changes on GitHub:

```powershell
git tag -a v1.0.0 -m "TrackMirror 1.0.0"
git push origin v1.0.0
```

A version tag `v<MAJOR>.<MINOR>.<PATCH>` with an optional prerelease suffix starts Release. The workflow
validates and checks out the exact tag, restores/builds/tests, builds the installer, both ZIPs and SHA256SUMS.txt,
and uploads a draft. Manual dispatch accepts an existing tag and never creates one implicitly.
Both triggers require the tagged commit to be part of `origin/main` history. Tags on unmerged feature
or develop commits are rejected before the build. Older commits already contained in `main` are allowed.
Pull requests and branch pushes never create a release; merge changes into `main`, then push a version tag.
The archive/assembly version comes from the validated tag. Prerelease suffixes mark prerelease drafts.

A rerun replaces assets only in an existing draft; a published Release fails safely. Review notes,
source availability and checksums before publishing the draft in GitHub. Do not move/reuse published tags.

## Assets and licensing

- `TrackMirror-v<VERSION>-win-x64-setup.exe`: per-user Windows x64 installer.
- `TrackMirror-v<VERSION>-win-x64.zip`: complete self-contained portable application.
- `TrackMirror-v<VERSION>-source.zip`: corresponding public source.
- `SHA256SUMS.txt`: SHA-256 values for the installer and both ZIPs.

## Preview signing policy

Initial preview assets are unsigned. SHA-256 checks detect changed downloads when compared with the
trusted release checksum list; they do not provide a verified Windows publisher identity. Windows can
show an unknown-publisher or SmartScreen warning. A self-signed certificate would not fix public trust.
Do not tell users to disable their security protections.

Before broad distribution, evaluate [SignPath Foundation](https://signpath.org/) for eligible open-source
projects or a publicly trusted Authenticode signing service/certificate. Sign the application binaries
before creating the package inventory and ZIP, then sign the installer before generating final checksums.
Never publish private signing keys in the repository. This workflow does not require signing credentials
and does not register or purchase any signing service.

MPL-2.0 requires making covered source available when distributing executables. Keep corresponding
source/notices with releases and preserve third-party terms. Read [LICENSE](LICENSE) and
[Mozilla's MPL FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/).

Keep `global.json` within .NET 10. Refresh lock files for package changes and verify locked restore.
Review action pin/version updates. Ollama/model digest changes require upstream-license/installer review.
Packaging scripts do not publish; the release workflow runs after source and a tag exist on GitHub.
