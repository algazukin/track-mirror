# SPDX-License-Identifier: MPL-2.0
# This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
# If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

#Requires -Version 7.4
[CmdletBinding()]
param([string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'))

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$taskFiles = @(git -C $taskRoot -c core.quotepath=false ls-files --cached --others --exclude-standard)
if ($LASTEXITCODE -ne 0) { throw 'Repository validation requires a Git checkout.' }

$taskRequired = @('LICENSE', 'NOTICE', 'README.md', 'USER_GUIDE.md', 'CONTRIBUTING.md', 'PRIVACY.md',
    'SECURITY.md', 'RELEASING.md', 'CHANGELOG.md', 'THIRD-PARTY-NOTICES.md', 'global.json', 'NuGet.config',
    '.github/workflows/ci.yml', '.github/workflows/release.yml',
    'scripts/Build-Release.ps1', 'scripts/Build-Installer.ps1', 'scripts/Export-Source.ps1', 'scripts/Test-Package.ps1',
    'installer/windows/Setup.iss', 'installer/windows/Messages.iss', 'installer/windows/Lifecycle.iss',
    'src/TrackMirror.App/packages.lock.json',
    'tests/TrackMirror.IntegrationTests/packages.lock.json')
foreach ($taskName in $taskRequired) {
    if (!(Test-Path -LiteralPath (Join-Path $taskRoot $taskName) -PathType Leaf)) {
        throw "Missing public repository file: $taskName"
    }
}

$taskForbidden = '(^|/)(docs|plans|specs|work|data|captures|publish|artifacts|bin|obj|privacy-runtime|privacy-downloads|\.git|\.codex|\.agents|\.superpowers)(/|$)|' +
    '(^|/)(viewer\.db.*|settings\.json|index\.json|privacy-results\.json|activity-history\.json|hubstaff-activity(?:-history)?\.json|data-location\.json|startup-error\.txt|\.env(?:\..*)?)$|' +
    '\.(zip|7z|log|tmp|part|trx)$'
foreach ($taskRelative in $taskFiles | Sort-Object -Unique) {
    if ($taskRelative -match $taskForbidden) { throw "Private/internal file in Git's public list: $taskRelative" }
    $taskPath = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskRelative))
    if (!(Test-Path -LiteralPath $taskPath -PathType Leaf)) { throw "Missing tracked file: $taskRelative" }
    if ((Get-Item -LiteralPath $taskPath).Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
        throw "Linked file is not allowed in the source snapshot: $taskRelative"
    }
    if ((Get-Item -LiteralPath $taskPath).Length -ge 100MB) { throw "File exceeds GitHub's size limit: $taskRelative" }
    if ($taskRelative -match '\.md$|^LICENSE$|^NOTICE$|^licenses/.+\.txt$|^\.github/.+\.ya?ml$') {
        $taskText = Get-Content -LiteralPath $taskPath -Raw -Encoding utf8
        if ($taskText -match '[\u0400-\u04FF]') { throw "Non-English text in public document: $taskRelative" }
        if ($taskText -match 'C:[/\\]Users[/\\](?!Public\b)|Documents[/\\]Codex|gitlab\.com') {
            throw "Personal path or old remote in public document: $taskRelative"
        }
    }
    if ($taskRelative -match '\.(cs|ps1|iss)$') {
        $taskText = Get-Content -LiteralPath $taskPath -Raw -Encoding utf8
        if ($taskText -notmatch 'SPDX-License-Identifier: MPL-2.0') {
            throw "Missing first-party MPL notice: $taskRelative"
        }
    }
    if ($taskRelative -match '\.md$') {
        $taskText = Get-Content -LiteralPath $taskPath -Raw -Encoding utf8
        foreach ($taskLink in [regex]::Matches($taskText, '\]\(([^)]+)\)')) {
            $taskTarget = $taskLink.Groups[1].Value
            if ($taskTarget -match '^(https?://|#|mailto:)') { continue }
            $taskTarget = ($taskTarget -split '#', 2)[0]
            if (!(Test-Path -LiteralPath (Join-Path (Split-Path -Parent $taskPath) $taskTarget))) {
                throw "Broken document link in ${taskRelative}: $taskTarget"
            }
        }
    }
}
$taskLicense = Get-Content -LiteralPath (Join-Path $taskRoot 'LICENSE') -Raw
if ($taskLicense -notmatch 'Mozilla Public License' -or $taskLicense -notmatch '2\.0') {
    throw 'The project license must be MPL-2.0.'
}
Write-Output "Public repository validated: $($taskFiles.Count) files; English documents; no internal/private files."
