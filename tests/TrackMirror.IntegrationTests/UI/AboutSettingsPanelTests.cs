// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Diagnostics;
using System.Net;
using System.Text;
using TrackMirror.Constants;
using TrackMirror.IntegrationTests.Infrastructure.Windows;
using TrackMirror.IntegrationTests.Localization;
using TrackMirror.Localization;
using TrackMirror.Models;
using TrackMirror.UI.Controls;
using TrackMirror.Updates;
using Xunit;

namespace TrackMirror.IntegrationTests.UI;

[Collection(nameof(LocalizationCollection))]
public sealed class AboutSettingsPanelTests
{
    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void The_check_button_fits_its_translated_caption_in_a_narrow_panel(string language)
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize(language);
            using var handler = new Handler((_, _) => throw new InvalidOperationException("Unexpected update check."));
            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0-preview.2",
                new UpdateChecker(client), _ => { });

            // Act
            panel.Width = 460;
            panel.CreateControl();
            panel.PerformLayout();
            var button = UiControls.Descendants<Button>(panel).Single();
            var caption = TextRenderer.MeasureText(button.Text, button.Font, Size.Empty,
                TextFormatFlags.SingleLine);

            // Assert
            Assert.True(button.ClientSize.Width >= caption.Width + 16,
                $"The {language} caption needs {caption.Width + 16}px, but the button is {button.ClientSize.Width}px wide.");
            Assert.True(button.Right <= panel.ClientSize.Width);
        });
    }

    [Fact]
    public void Opening_about_shows_version_and_project_links_without_contacting_GitHub()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            var requests = 0;
            var opened = new List<Uri>();

            using var handler = new Handler((_, _) =>
            {
                requests++;

                return Task.FromResult(Json("[]"));
            });

            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0-preview.2",
                new UpdateChecker(client), opened.Add);

            // Act
            panel.CreateControl();
            var labels = UiControls.Descendants<Label>(panel).ToArray();
            var links = UiControls.Descendants<LinkLabel>(panel).ToArray();

            // Assert
            Assert.Contains(labels, label => label.Text.Contains("1.0.0-preview.2", StringComparison.Ordinal));
            Assert.Contains(links, link => link.Text == "GitHub");
            Assert.Contains(links, link => link.Text == "MPL-2.0");
            Assert.Equal(0, requests);
            Assert.Empty(opened);
        });
    }

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    public void A_new_release_opens_its_GitHub_page_after_a_manual_check(string language)
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize(language);
            var opened = new List<Uri>();

            using var handler = new Handler((_, _) => Task.FromResult(Json(
                "[{\"tag_name\":\"v1.0.0-preview.2\",\"draft\":false,\"prerelease\":true}]")));

            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0-preview.1",
                new UpdateChecker(client), opened.Add);

            // Act
            Complete(panel.CheckForUpdatesAsync());

            // Assert
            Assert.Equal(new Uri(ProjectInfo.RepositoryUrl + "/releases/tag/v1.0.0-preview.2"), Assert.Single(opened));
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text.Contains("1.0.0-preview.2", StringComparison.Ordinal));
        });
    }

    [Theory]
    [InlineData("[{\"tag_name\":\"v1.0.0\",\"draft\":false,\"prerelease\":false}]", "latest version")]
    [InlineData("[]", "No published releases")]
    public void A_completed_check_without_an_update_reports_the_result_without_opening_a_browser(string feed, string expected)
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            var opened = new List<Uri>();

            using var handler = new Handler((_, _) => Task.FromResult(Json(feed)));
            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.Stable, "1.0.0",
                new UpdateChecker(client), opened.Add);

            // Act
            Complete(panel.CheckForUpdatesAsync());

            // Assert
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text.Contains(expected, StringComparison.Ordinal));
            Assert.Empty(opened);
        });
    }

    [Fact]
    public void A_network_failure_restores_the_check_button_and_reports_failure()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0",
                new UpdateChecker(client), _ => throw new InvalidOperationException("Unexpected browser launch."));

            // Act
            Complete(panel.CheckForUpdatesAsync());
            var button = UiControls.Descendants<Button>(panel).SingleOrDefault(value => value.Text == "Check for updates");

            // Assert
            Assert.NotNull(button);
            Assert.True(button.Enabled);
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text.Contains("Unable to check", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void Repeated_clicks_do_not_start_parallel_checks_and_disposal_cancels_the_request()
    {
        // Arrange
        StaThread.Run(() =>
        {
            var requests = 0;
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var opened = new List<Uri>();

            using var handler = new Handler((_, token) =>
            {
                requests++;

                return response.Task.WaitAsync(token);
            });

            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0",
                new UpdateChecker(client), opened.Add);

            // Act
            var first = panel.CheckForUpdatesAsync();
            var repeated = panel.CheckForUpdatesAsync();
            panel.Dispose();
            Complete(Task.WhenAll(first, repeated));

            // Assert
            Assert.Equal(1, requests);
            Assert.Empty(opened);
        });
    }

    [Fact]
    public void The_channel_selector_in_settings_persists_the_choice_without_changing_other_preferences()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            using var panel = new SettingsPanel(new AppSettings { NotificationsEnabled = false }, false);
            AppSettings? saved = null;
            panel.Changed += value => saved = value;

            // Act
            var selector = UiControls.Descendants<OptionSelector>(panel)
                .SingleOrDefault(control => control.AccessibleName == "Update channel");

            // Assert
            Assert.NotNull(selector);

            // Act
            selector.SelectValue("Stable");

            // Assert
            Assert.NotNull(saved);
            Assert.Equal(UpdateChannel.Stable, saved.UpdateChannel);
            Assert.False(saved.NotificationsEnabled);
        });
    }

    [Theory]
    [InlineData("GitHub", ProjectInfo.RepositoryUrl)]
    [InlineData("MPL-2.0", ProjectInfo.LicenseUrl)]
    public void Project_links_open_the_expected_official_address(string caption, string address)
    {
        // Arrange
        StaThread.Run(() =>
        {
            var opened = new List<Uri>();
            using var handler = new Handler((_, _) => throw new InvalidOperationException("Unexpected update check."));
            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0-preview.2",
                new UpdateChecker(client), opened.Add);

            // Act
            var link = UiControls.Descendants<LinkLabel>(panel).Single(value => value.Text == caption);
            UiControls.Invoke(link, "OnLinkClicked", new LinkLabelLinkClickedEventArgs(link.Links[0]));

            // Assert
            Assert.Equal(new Uri(address), Assert.Single(opened));
        });
    }

    [Theory]
    [InlineData("rate", "temporarily limited")]
    [InlineData("timeout", "timed out")]
    [InlineData("json", "Unable to check")]
    public void Check_errors_have_clear_messages_and_do_not_open_a_browser(string kind, string message)
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            var opened = new List<Uri>();

            using var handler = new Handler((_, _) => kind switch
            {
                "rate" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)),
                "timeout" => Task.FromException<HttpResponseMessage>(new TaskCanceledException("Synthetic timeout.")),
                _ => Task.FromResult(Json("not-json"))
            });

            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0-preview.2",
                new UpdateChecker(client), opened.Add);

            // Act
            Complete(panel.CheckForUpdatesAsync());

            // Assert
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text.Contains(message, StringComparison.Ordinal));
            Assert.Empty(opened);
        });
    }

    [Fact]
    public void A_missing_browser_association_is_reported_without_crashing_the_check()
    {
        // Arrange
        StaThread.Run(() =>
        {
            UiLocalization.Initialize("en");
            using var handler = new Handler((_, _) => Task.FromResult(Json(
                "[{\"tag_name\":\"v2.0.0\",\"draft\":false,\"prerelease\":false}]")));

            using var client = new HttpClient(handler);
            using var panel = new AboutSettingsPanel(UpdateChannel.PreRelease, "1.0.0",
                new UpdateChecker(client), _ => throw new System.ComponentModel.Win32Exception("Synthetic association error."));

            // Act
            Complete(panel.CheckForUpdatesAsync());

            // Assert
            Assert.Contains(UiControls.Descendants<Label>(panel), label => label.Text.Contains("Unable to open your browser", StringComparison.Ordinal));
            Assert.True(UiControls.Descendants<Button>(panel).Single(button => button.Text == "Check for updates").Enabled);
        });
    }

    private static void Complete(Task operation)
    {
        var elapsed = Stopwatch.StartNew();

        while (!operation.IsCompleted && elapsed.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(1);
        }

        Assert.True(operation.IsCompleted, "The manual update check did not finish.");
        operation.GetAwaiter().GetResult();
    }

    private static HttpResponseMessage Json(string value) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(value, Encoding.UTF8, "application/json")
    };

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            respond(request, token);
    }
}
