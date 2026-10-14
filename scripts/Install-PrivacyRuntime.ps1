# SPDX-License-Identifier: MPL-2.0
# This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
# If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

#Requires -Version 7.4
param([string]$ApplicationDirectory = (Join-Path $PSScriptRoot '..\publish'))

$ErrorActionPreference = 'Stop'
$taskApplication = [IO.Path]::GetFullPath($ApplicationDirectory)
$taskRuntime = Join-Path $taskApplication 'privacy-runtime'
$taskModels = Join-Path $taskRuntime 'models'
$taskEndpoint = 'http://127.0.0.1:11437'
$taskModel = 'qwen3-vl:4b-instruct'
$taskDigest = 'ee4b975b58c17ce268cd19d40db35d5edc64603035d2ffc1fee1968eb0947f7b'
$taskVersion = 'v0.40.3'
$taskArchiveHash = 'E5ECFF02DFC7CAE8DDF6D6B7A7AC5F727FFEEDF4491F44AE75FF5B0820DBBBD5'
$taskDownload = Join-Path ([IO.Path]::GetTempPath()) ('hubstaff-privacy-' + [Guid]::NewGuid().ToString('N') + '.zip')

if (!(Test-Path -LiteralPath (Join-Path $taskApplication 'TrackMirror.exe'))) {
    throw 'Publish the application before installing its optional privacy runtime.'
}

if (Test-Path -LiteralPath $taskRuntime) {
    throw 'The privacy runtime directory already exists. Keep it, or move it aside before reinstalling.'
}

$taskProbe = [Net.Sockets.Socket]::new(
    [Net.Sockets.AddressFamily]::InterNetwork,
    [Net.Sockets.SocketType]::Stream,
    [Net.Sockets.ProtocolType]::Tcp)

try {
    $taskProbe.ExclusiveAddressUse = $true
    $taskProbe.Bind([Net.IPEndPoint]::new([Net.IPAddress]::Loopback, 11437))
} finally {
    $taskProbe.Dispose()
}

Write-Output 'Downloading the isolated Ollama runtime (about 1.5 GB).'
Invoke-WebRequest -Uri ('https://github.com/ollama/ollama/releases/download/' + $taskVersion +
    '/ollama-windows-amd64.zip') -OutFile $taskDownload

if ((Get-FileHash -LiteralPath $taskDownload -Algorithm SHA256).Hash -ne $taskArchiveHash) {
    throw 'Runtime archive checksum mismatch. Nothing has been installed.'
}

New-Item -ItemType Directory -Path $taskRuntime -Force | Out-Null
Expand-Archive -LiteralPath $taskDownload -DestinationPath $taskRuntime
Remove-Item -LiteralPath $taskDownload
New-Item -ItemType Directory -Path $taskModels -Force | Out-Null

$taskStart = [Diagnostics.ProcessStartInfo]::new((Join-Path $taskRuntime 'ollama.exe'), 'serve')
$taskStart.WorkingDirectory = $taskRuntime
$taskStart.UseShellExecute = $false
$taskStart.CreateNoWindow = $true
$taskStart.RedirectStandardOutput = $true
$taskStart.RedirectStandardError = $true
$taskStart.Environment['OLLAMA_HOST'] = '127.0.0.1:11437'
$taskStart.Environment['OLLAMA_MODELS'] = $taskModels
$taskStart.Environment['OLLAMA_NO_CLOUD'] = '1'
$taskStart.Environment['OLLAMA_NUM_PARALLEL'] = '1'
$taskProcess = [Diagnostics.Process]::Start($taskStart)
$taskProcess.BeginOutputReadLine()
$taskProcess.BeginErrorReadLine()

try {
    $taskReady = $false

    for ($taskAttempt = 0; $taskAttempt -lt 80; $taskAttempt++) {
        if ($taskProcess.HasExited) { throw 'The isolated runtime exited during startup.' }

        try {
            Invoke-RestMethod -Uri ($taskEndpoint + '/api/version') -TimeoutSec 1 | Out-Null
            $taskReady = $true
            break
        } catch [System.Net.Http.HttpRequestException] {
            Start-Sleep -Milliseconds 250
        }
    }

    if (!$taskReady) { throw 'The isolated runtime did not become ready.' }

    Write-Output 'Downloading Qwen3-VL 4B Instruct (about 3.3 GB).'
    $taskBody = @{ model = $taskModel; stream = $false } | ConvertTo-Json -Compress
    $taskPull = Invoke-RestMethod -Uri ($taskEndpoint + '/api/pull') -Method Post -ContentType 'application/json' `
        -Body $taskBody -TimeoutSec 3600

    if ($taskPull.status -ne 'success') { throw 'The model download did not finish successfully.' }

    $taskTags = Invoke-RestMethod -Uri ($taskEndpoint + '/api/tags')
    $taskLocalHash = '3c9f45126ab947c70afd5602e74427b3ef96cc4408589f172840003f294c90b8'
    $taskInstalled = $taskTags.models | Where-Object {
        $_.name -eq $taskModel -and $_.digest -in @($taskDigest, $taskLocalHash)
    }

    if (!$taskInstalled -or $taskProcess.HasExited) {
        throw 'The downloaded model does not match the pinned version. The viewer will refuse it.'
    }

    Write-Output 'Local privacy runtime installed. Restart the viewer to refresh Settings.'
} finally {
    if (!$taskProcess.HasExited) {
        $taskProcess.Kill($true)
        $taskProcess.WaitForExit(5000) | Out-Null
    }

    $taskProcess.Dispose()
}
