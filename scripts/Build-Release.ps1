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
    [string]$OutputDirectory,
    [ValidatePattern('^(?:[0-9a-fA-F]{40})?$')][string]$SourceCommit,
    [switch]$BuildInstaller,
    [string]$InnoSetupCompiler
)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($RepositoryRoot)
if (!$OutputDirectory) {
    $OutputDirectory = Join-Path $taskRoot 'artifacts/releases'
}

$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $taskOutput -Force | Out-Null
$taskArchive = Join-Path $taskOutput "TrackMirror-v$Version-win-x64.zip"
if (Test-Path -LiteralPath $taskArchive) {
    throw 'A binary archive with this version already exists. Choose an empty output directory.'
}

$taskStage = Join-Path $taskOutput ('.release-stage-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $taskStage | Out-Null
$taskApp = Join-Path $taskStage 'app'
$taskProject = Join-Path $taskRoot 'src/TrackMirror.App/TrackMirror.App.csproj'

try {
    dotnet restore $taskProject --locked-mode --runtime win-x64 -p:SelfContained=true -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Locked release restore failed.' }

    dotnet publish $taskProject --no-restore --configuration Release --runtime win-x64 --self-contained true `
        --output $taskApp "-p:Version=$Version" -p:DebugType=None -p:DebugSymbols=false `
        -p:ContinuousIntegrationBuild=true "-p:PathMap=$taskRoot=/_/" -p:UseSharedCompilation=false -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }

    $taskCache = $env:NUGET_PACKAGES
    if (!$taskCache) {
        $taskCacheOutput = @(dotnet nuget locals global-packages --list --force-english-output)
        if ($LASTEXITCODE -ne 0 -or $taskCacheOutput.Count -ne 1) { throw 'Cannot locate the NuGet cache.' }
        $taskCache = $taskCacheOutput[0] -replace '^global-packages: ', ''
    }

    $taskDeps = Get-Content -LiteralPath (Join-Path $taskApp 'TrackMirror.deps.json') -Raw |
        ConvertFrom-Json -AsHashtable
    $taskCore = @($taskDeps.libraries.Keys | Where-Object { $_ -like 'runtimepack.Microsoft.NETCore.App.Runtime.win-x64/*' })
    $taskDesktop = @($taskDeps.libraries.Keys | Where-Object { $_ -like 'runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/*' })
    if ($taskCore.Count -ne 1 -or $taskDesktop.Count -ne 1) { throw 'Published runtime versions are ambiguous.' }

    $taskCoreDirectory = Join-Path $taskCache ('microsoft.netcore.app.runtime.win-x64/' + ($taskCore[0] -split '/')[1])
    $taskDesktopDirectory = Join-Path $taskCache ('microsoft.windowsdesktop.app.runtime.win-x64/' + ($taskDesktop[0] -split '/')[1])
    Copy-Item -LiteralPath (Join-Path $taskCoreDirectory 'LICENSE.TXT') `
        -Destination (Join-Path $taskApp 'licenses/Dotnet-Core-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $taskCoreDirectory 'THIRD-PARTY-NOTICES.TXT') `
        -Destination (Join-Path $taskApp 'licenses/Dotnet-Core-THIRD-PARTY-NOTICES.txt')
    Copy-Item -LiteralPath (Join-Path $taskDesktopDirectory 'LICENSE') `
        -Destination (Join-Path $taskApp 'licenses/Dotnet-Desktop-LICENSE.txt')

    if (!$SourceCommit -and (Test-Path -LiteralPath (Join-Path $taskRoot '.git'))) {
        $taskCommitOutput = @(git -C $taskRoot rev-parse --verify --quiet HEAD)
        if ($LASTEXITCODE -eq 0) {
            $SourceCommit = $taskCommitOutput[0]
        }
    }

    $taskSourceDirty = $null
    if (Test-Path -LiteralPath (Join-Path $taskRoot '.git')) {
        $taskStatus = @(git -C $taskRoot status --porcelain --untracked-files=normal)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot check source changes.' }
        $taskSourceDirty = $taskStatus.Count -gt 0
    }

    [ordered]@{
        Application = 'TrackMirror'
        Version = $Version
        Runtime = 'win-x64'
        SelfContained = $true
        License = 'MPL-2.0'
        SourceCommit = $SourceCommit
        SourceDirty = $taskSourceDirty
        SourceArchive = "TrackMirror-v$Version-source.zip"
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskApp 'release-info.json') -Encoding utf8NoBOM

    $taskInventory = @(Get-ChildItem -LiteralPath $taskApp -File -Recurse | Sort-Object FullName | ForEach-Object {
        [ordered]@{
            Path = [IO.Path]::GetRelativePath($taskApp, $_.FullName).Replace('\', '/')
            Length = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
    ConvertTo-Json -InputObject $taskInventory | Set-Content `
        -LiteralPath (Join-Path $taskApp 'package-manifest.json') -Encoding utf8NoBOM
    & (Join-Path $PSScriptRoot 'Test-Package.ps1') -ApplicationDirectory $taskApp

    if ($BuildInstaller) {
        & (Join-Path $PSScriptRoot 'Build-Installer.ps1') -ApplicationDirectory $taskApp `
            -OutputDirectory $taskOutput -CompilerPath $InnoSetupCompiler
    }

    [IO.Compression.ZipFile]::CreateFromDirectory($taskApp, $taskArchive, [IO.Compression.CompressionLevel]::Optimal, $false)
    $taskHash = (Get-FileHash -LiteralPath $taskArchive -Algorithm SHA256).Hash.ToLowerInvariant()
    "$taskHash  $([IO.Path]::GetFileName($taskArchive))" |
        Set-Content -LiteralPath "$taskArchive.sha256" -Encoding utf8NoBOM
    Write-Output "Created binary archive: $taskArchive"
} finally {
    $taskResolved = [IO.Path]::GetFullPath($taskStage)
    if ($taskResolved.StartsWith($taskOutput + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($taskResolved) -match '^\.release-stage-[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $taskResolved -Recurse -Force
    }
}
