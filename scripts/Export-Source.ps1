# SPDX-License-Identifier: MPL-2.0
# This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
# If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$')]
    [string]$Version,
    [string]$RepositoryRoot = (Join-Path $PSScriptRoot '..'),
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($RepositoryRoot)
& (Join-Path $PSScriptRoot 'Test-Repository.ps1') -RepositoryRoot $taskRoot
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $taskRoot 'artifacts/releases'
}

$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArchive = Join-Path $taskOutput "TrackMirror-v$Version-source.zip"
if (Test-Path -LiteralPath $taskArchive) {
    throw 'A source archive with this version already exists. Choose an empty output directory.'
}

$taskFiles = @(git -C $taskRoot -c core.quotepath=false ls-files --cached --others --exclude-standard |
    Sort-Object -Unique)
if ($LASTEXITCODE -ne 0) { throw 'Source export requires a Git checkout.' }

$taskStage = Join-Path $taskOutput ('.source-stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage | Out-Null
try {
    foreach ($taskRelative in $taskFiles) {
        $taskDestination = [IO.Path]::GetFullPath((Join-Path $taskStage $taskRelative))
        if (!$taskDestination.StartsWith($taskStage + [IO.Path]::DirectorySeparatorChar,
            [StringComparison]::OrdinalIgnoreCase)) { throw 'Source path escaped the archive.' }

        New-Item -ItemType Directory -Path (Split-Path -Parent $taskDestination) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $taskRoot $taskRelative) -Destination $taskDestination
    }

    [IO.Compression.ZipFile]::CreateFromDirectory($taskStage, $taskArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
    $taskHash = (Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$taskHash  $([IO.Path]::GetFileName($taskArchive))" |
        Set-Content -LiteralPath "$taskArchive.sha256" -Encoding utf8NoBOM
    Write-Output "Created public source archive: $taskArchive"
} finally {
    $taskResolved = [IO.Path]::GetFullPath($taskStage)
    if ($taskResolved.StartsWith($taskOutput + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($taskResolved) -match '^\.source-stage-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $taskResolved -Recurse -Force
    }
}
