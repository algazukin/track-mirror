// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using TrackMirror.Constants;
using TrackMirror.Privacy.Installation;
using TrackMirror.Privacy.Models;

namespace TrackMirror.Privacy.Providers;

internal sealed class LocalModelRuntime : IDisposable
{
    private readonly string _root;
    private readonly int _port;
    private Process? process;

    internal static string Root => Path.Combine(AppContext.BaseDirectory, PrivacyNames.RuntimeDirectory);

    internal static bool Installed => IsInstalled(PrivacyNames.Model);

    internal static bool IsInstalled(string model) => PrivacyModelCatalog.Installed(Root, model);

    internal LocalModelRuntime(string? root = null, int port = PrivacyNames.LocalPort)
    {
        _root = root ?? Root;

        _port = port;
    }

    internal async Task EnsureReadyAsync(HttpClient client, string tag, CancellationToken token)
    {
        if (!PrivacyModelCatalog.Installed(_root, tag))
            throw new PrivacyScanException(PrivacyNames.RuntimeUnavailable);

        await EnsureServerAsync(client, token).ConfigureAwait(false);

        using var response = await client.GetAsync(PrivacyProtocol.TagsPath, token).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token).ConfigureAwait(false));

        var model = PrivacyModelCatalog.Get(tag);

        var installed = body.RootElement.GetProperty(PrivacyProtocol.Models).EnumerateArray()
            .Any(value => value.GetProperty(PrivacyProtocol.Name).GetString() == tag
                && value.GetProperty(PrivacyProtocol.Digest).GetString() is string digest
                && (digest == model.ManifestDigest || digest == model.LocalManifestDigest));

        if (process!.HasExited)
            throw new PrivacyScanException(PrivacyNames.RuntimeStopped);

        if (!installed)
            throw new PrivacyScanException(PrivacyNames.ModelUnavailable);
    }

    internal async Task EnsureServerAsync(HttpClient client, CancellationToken token)
    {
        if (process is { HasExited: false })
            return;

        process?.Dispose();

        process = null;

        if (!File.Exists(Path.Combine(_root, PrivacyNames.RuntimeExecutable))
            || File.Exists(Path.Combine(_root, PrivacyInstallNames.RuntimePendingFile)))
            throw new PrivacyScanException(PrivacyNames.RuntimeUnavailable);

        // Bind first and never attach to an unknown service on either private endpoint.
        try
        {
            using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            listener.ExclusiveAddressUse = true;

            listener.Bind(new IPEndPoint(IPAddress.Loopback, _port));
        }
        catch (SocketException)
        {
            throw new PrivacyScanException(PrivacyNames.RuntimePortUnavailable);
        }

        var start = new ProcessStartInfo(Path.Combine(_root, PrivacyNames.RuntimeExecutable), PrivacyProtocol.Serve)
        {
            WorkingDirectory = _root,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        start.Environment[PrivacyProtocol.HostEnvironment] =
            IPAddress.Loopback + PrivacyInstallNames.HostSeparator + _port;

        start.Environment[PrivacyProtocol.ModelsEnvironment] = Path.Combine(_root, PrivacyNames.ModelsDirectory);

        start.Environment[PrivacyProtocol.CloudEnvironment] = PrivacyProtocol.One;

        start.Environment[PrivacyProtocol.ParallelEnvironment] = PrivacyProtocol.One;

        start.Environment[PrivacyProtocol.LoadedEnvironment] = PrivacyProtocol.One;

        start.Environment[PrivacyProtocol.KeepAliveEnvironment] = PrivacyProtocol.KeepAlive;

        start.Environment[PrivacyProtocol.ContextEnvironment] = PrivacyProtocol.ContextLength;

        try
        {
            process = Process.Start(start) ?? throw new PrivacyScanException(PrivacyNames.RuntimeUnavailable);
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new PrivacyScanException(PrivacyNames.RuntimeUnavailable);
        }

        process.BeginOutputReadLine();

        process.BeginErrorReadLine();

        for (var attempt = 0; attempt < 80; attempt++)
        {
            token.ThrowIfCancellationRequested();

            if (process.HasExited)
                throw new PrivacyScanException(PrivacyNames.RuntimeStopped);

            try
            {
                using var response = await client.GetAsync(PrivacyProtocol.VersionPath, token).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                if (process.HasExited)
                    throw new PrivacyScanException(PrivacyNames.RuntimeStopped);

                return;
            }
            catch (HttpRequestException)
            {
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }

        throw new PrivacyScanException(PrivacyNames.ModelConnection);
    }

    public void Dispose()
    {
        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);

                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
        }

        process.Dispose();

        process = null;
    }
}
