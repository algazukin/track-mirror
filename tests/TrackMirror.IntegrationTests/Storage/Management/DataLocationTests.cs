// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using TrackMirror.Storage.Management;
using Xunit;
using TrackMirror.IntegrationTests.Infrastructure;

namespace TrackMirror.IntegrationTests.Storage.Management;

public sealed class DataLocationTests
{
    [Fact]
    public void Existing_portable_data_is_retained_while_new_installations_use_a_writable_user_folder()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var application = Path.Combine(workspace.Root, "application");

        var profile = Path.Combine(workspace.Root, "profile");

        var location = new DataLocationStore(application, profile);

        // Act
        // Assert
        Assert.Equal(Path.Combine(profile, "data"), location.Resolve());

        // Act
        Directory.CreateDirectory(Path.Combine(application, "data"));

        // Act
        // Assert
        Assert.Equal(Path.Combine(application, "data"), location.Resolve());
    }

    [Fact]
    public void Selected_folder_survives_restart_and_explicit_paths_override_it()
    {
        // Arrange
        using var workspace = new TestWorkspace();

        var application = Path.Combine(workspace.Root, "application");

        var profile = Path.Combine(workspace.Root, "profile");

        var selected = Path.Combine(workspace.Root, "Новая папка");

        Directory.CreateDirectory(selected);

        // Act
        new DataLocationStore(application, profile).RecordSelection(selected);

        var reopened = new DataLocationStore(application, profile);

        // Assert
        Assert.Equal(selected, reopened.Resolve());

        Assert.Equal(Path.Combine(workspace.Root, "override"), reopened.Resolve(Path.Combine(workspace.Root, "override")));

        // Act
        Directory.Delete(selected);

        // Act
        // Assert
        Assert.Throws<IOException>(() => reopened.Resolve());

        Assert.False(Directory.Exists(selected));
    }
}
