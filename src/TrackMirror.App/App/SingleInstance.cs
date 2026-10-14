// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Security.Cryptography;
using System.Text;
using TrackMirror.Constants;

namespace TrackMirror.App;

internal static class SingleInstance
{
    internal static string Identifier(string dataRoot)
    {
        var path = Path.GetFullPath(Path.Combine(dataRoot, StartupNames.DirectoryMarker));

        var key = Encoding.UTF8.GetBytes(path.ToUpperInvariant());

        return Convert.ToHexString(SHA256.HashData(key))[..24];
    }

    internal static bool Exists(string name)
    {
        try
        {
            if (!Mutex.TryOpenExisting(name, out var mutex))
            {
                return false;
            }

            mutex.Dispose();

            return true;
        }
        catch (UnauthorizedAccessException)
        {
            // Another instance owns this name, even if this process cannot open its handle.
            return true;
        }
    }
}
