# SPDX-License-Identifier: MPL-2.0
# This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
# If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

#Requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$ApplicationDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CompilerPath
)

$ErrorActionPreference = 'Stop'
$taskApp = [IO.Path]::GetFullPath($ApplicationDirectory)
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
if ($taskOutput.Equals($taskApp, [StringComparison]::OrdinalIgnoreCase) -or
    $taskOutput.StartsWith($taskApp + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The installer output must be outside the application payload.'
}

& (Join-Path $PSScriptRoot 'Test-Package.ps1') -ApplicationDirectory $taskApp
$taskInfo = Get-Content -LiteralPath (Join-Path $taskApp 'release-info.json') -Raw | ConvertFrom-Json
$taskVersion = $taskInfo.Version
if ($taskVersion -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?$') {
    throw 'The package does not contain a valid release version.'
}

if (!$CompilerPath) {
    $taskCommand = Get-Command ISCC.exe -CommandType Application -ErrorAction SilentlyContinue
    if ($taskCommand) {
        $CompilerPath = $taskCommand.Source
    } else {
        foreach ($taskPrograms in @(${env:ProgramFiles(x86)}, $env:ProgramFiles) | Where-Object { $_ }) {
            foreach ($taskMajor in 6, 7) {
                $taskCandidate = Join-Path $taskPrograms "Inno Setup $taskMajor/ISCC.exe"
                if (Test-Path -LiteralPath $taskCandidate -PathType Leaf) {
                    $CompilerPath = $taskCandidate
                    break
                }
            }
            if ($CompilerPath) { break }
        }
    }
}

if (!$CompilerPath -or !(Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Install Inno Setup 6.3 or newer, add ISCC.exe to PATH, or pass -CompilerPath.'
}

New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskName = "TrackMirror-v$taskVersion-win-x64-setup"
$taskInstaller = Join-Path $taskOutput ($taskName + '.exe')
if (Test-Path -LiteralPath $taskInstaller) {
    throw 'An installer with this version already exists. Choose an empty output directory.'
}

$taskStage = Join-Path $taskOutput ('.installer-stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage | Out-Null
try {
    $taskArguments = @('/Qp', "/DAppSource=$taskApp", "/DAppVersion=$taskVersion",
        ('/DNumericVersion=' + ($taskVersion -split '-', 2)[0] + '.0'), "/O$taskStage", "/F$taskName",
        (Join-Path $PSScriptRoot '../installer/windows/Setup.iss'))

    & $CompilerPath @taskArguments
    if ($LASTEXITCODE -ne 0) { throw 'Windows installer compilation failed.' }

    $taskBuilt = Join-Path $taskStage ($taskName + '.exe')
    if (!(Test-Path -LiteralPath $taskBuilt -PathType Leaf)) { throw 'The compiler did not create the installer.' }

    $taskHash = (Get-FileHash -LiteralPath $taskBuilt -Algorithm SHA256).Hash.ToLowerInvariant()
    [IO.File]::Move($taskBuilt, $taskInstaller)
    "$taskHash  $([IO.Path]::GetFileName($taskInstaller))" |
        Set-Content -LiteralPath "$taskInstaller.sha256" -Encoding utf8NoBOM
    Write-Output "Created unsigned Windows x64 installer: $taskInstaller"
} finally {
    $taskResolved = [IO.Path]::GetFullPath($taskStage)
    if ($taskResolved.StartsWith($taskOutput + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($taskResolved) -match '^\.installer-stage-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $taskResolved -Recurse -Force
    }
}
