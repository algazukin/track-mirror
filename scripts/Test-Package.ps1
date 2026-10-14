# SPDX-License-Identifier: MPL-2.0
# This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
# If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

#Requires -Version 7.4
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ApplicationDirectory)

$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath($ApplicationDirectory)
$taskRequired = @('TrackMirror.exe', 'TrackMirror.dll',
    'TrackMirror.deps.json', 'TrackMirror.runtimeconfig.json',
    'coreclr.dll', 'hostfxr.dll', 'System.Windows.Forms.dll', 'Microsoft.Data.Sqlite.dll',
    'ru/TrackMirror.resources.dll', 'LICENSE', 'NOTICE', 'USER_GUIDE.md', 'PRIVACY.md', 'SECURITY.md', 'CHANGELOG.md',
    'THIRD-PARTY-NOTICES.md', 'privacy-models/eslav_PP-OCRv5_rec_mobile.onnx',
    'privacy-models/ppocrv5_eslav_dict.txt', 'licenses/Dotnet-Core-LICENSE.txt',
    'licenses/Dotnet-Core-THIRD-PARTY-NOTICES.txt', 'licenses/Dotnet-Desktop-LICENSE.txt',
    'models/v5/ch_PP-OCRv5_mobile_det.onnx',
    'models/v5/ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx',
    'models/v5/latin_PP-OCRv5_rec_mobile_infer.onnx', 'models/v5/ppocrv5_latin_dict.txt',
    'release-info.json')
foreach ($taskName in $taskRequired) {
    if (!(Test-Path -LiteralPath (Join-Path $taskRoot $taskName) -PathType Leaf)) {
        throw "Incomplete self-contained package: $taskName"
    }
}
$taskFiles = @(Get-ChildItem -LiteralPath $taskRoot -File -Recurse)
foreach ($taskNative in @('onnxruntime.dll', 'libSkiaSharp.dll', 'e_sqlite3.dll')) {
    if (!($taskFiles | Where-Object Name -eq $taskNative)) { throw "Missing native library: $taskNative" }
}
$taskNativeNames = @('TrackMirror.exe', 'coreclr.dll', 'hostfxr.dll',
    'onnxruntime.dll', 'libSkiaSharp.dll', 'e_sqlite3.dll')
foreach ($taskNative in $taskFiles | Where-Object Name -in $taskNativeNames) {
    $taskReader = [IO.BinaryReader]::new([IO.File]::OpenRead($taskNative.FullName))
    try {
        if ($taskReader.BaseStream.Length -lt 64 -or $taskReader.ReadUInt16() -ne 0x5a4d) {
            throw "Invalid Windows binary: $($taskNative.Name)"
        }
        $taskReader.BaseStream.Position = 0x3c
        $taskPeOffset = $taskReader.ReadUInt32()
        if ($taskPeOffset -lt 64 -or $taskPeOffset -gt $taskReader.BaseStream.Length - 6) {
            throw "Invalid Windows binary header: $($taskNative.Name)"
        }
        $taskReader.BaseStream.Position = $taskPeOffset
        if ($taskReader.ReadUInt32() -ne 0x4550 -or $taskReader.ReadUInt16() -ne 0x8664) {
            throw "Expected a Windows x64 binary: $($taskNative.Name)"
        }
    } finally {
        $taskReader.Dispose()
    }
}
$taskForbidden = '(^|/)(docs|plans|specs|data|captures|privacy-runtime|privacy-downloads|\.git|bin|obj)(/|$)|' +
    '(^|/)(viewer\.db.*|settings\.json|index\.json|privacy-results\.json|startup-error\.txt|\.env(?:\..*)?)$|' +
    '\.(pdb|log|tmp|part|zip)$'
foreach ($taskFile in $taskFiles) {
    $taskRelative = [IO.Path]::GetRelativePath($taskRoot, $taskFile.FullName).Replace('\', '/')
    if ($taskRelative -match $taskForbidden) { throw "Private/build file in release: $taskRelative" }
    if ($taskFile.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) { throw 'Release contains a linked file.' }
}
$taskInfo = Get-Content -LiteralPath (Join-Path $taskRoot 'release-info.json') -Raw | ConvertFrom-Json
if ($taskInfo.Runtime -ne 'win-x64' -or !$taskInfo.SelfContained -or $taskInfo.License -ne 'MPL-2.0') {
    throw 'Release metadata does not identify a self-contained MPL-2.0 Windows package.'
}
$taskInventory = Get-Content -LiteralPath (Join-Path $taskRoot 'package-manifest.json') -Raw | ConvertFrom-Json
if ($taskInventory.Count -ne $taskFiles.Count - 1) { throw 'Release file inventory is incomplete.' }
$taskNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($taskEntry in $taskInventory) {
    $taskPath = [IO.Path]::GetFullPath((Join-Path $taskRoot $taskEntry.Path))
    $taskPrefix = $taskRoot + [IO.Path]::DirectorySeparatorChar
    if (!$taskPath.StartsWith($taskPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        !$taskNames.Add($taskEntry.Path)) { throw 'Release inventory has an unsafe or duplicate path.' }
    if ((Get-Item -LiteralPath $taskPath).Length -ne $taskEntry.Length -or
        (Get-FileHash -LiteralPath $taskPath -Algorithm SHA256).Hash -ne $taskEntry.Sha256) {
        throw "Release inventory mismatch: $($taskEntry.Path)"
    }
}
Write-Output "Self-contained release validated: $($taskFiles.Count) files; complete assets and checksums."

