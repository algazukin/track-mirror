// SPDX-License-Identifier: MPL-2.0
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, obtain one at https://mozilla.org/MPL/2.0.

using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using TrackMirror.Constants;
using TrackMirror.Models;

namespace TrackMirror.Monitoring;

internal static class HubstaffActivityReader
{
    internal static List<HubstaffActivity> Read(string path, string account)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            XmlResolver = null,
            MaxCharactersInDocument = 8 * 1024 * 1024
        });

        var document = XDocument.Load(reader);

        var observedUtc = DateTime.UtcNow;

        var result = new List<HubstaffActivity>();

        var items = document.Root?.Element(HubstaffActivityNames.Items)?.Elements(HubstaffActivityNames.Item) ?? [];

        foreach (var item in items)
        {
            var activity = ReadItem(item, account, observedUtc);

            if (activity != null)
            {
                result.Add(activity);
            }
        }

        return result;
    }

    private static HubstaffActivity? ReadItem(XElement item, string account, DateTime observedUtc)
    {
        if (long.TryParse((string?)item.Element(HubstaffActivityNames.BreakId), out var breakId) && breakId != 0)
            return null;

        var start = item.Element(HubstaffActivityNames.Start);

        var reading = item.Element(HubstaffActivityNames.Reading);

        var dateText = (string?)start?.Element(HubstaffActivityNames.DateContainer)
            ?.Element(HubstaffActivityNames.Date);

        var time = start?.Element(HubstaffActivityNames.TimeContainer);

        if (reading == null || time == null)
            return null;

        if (!DateTime.TryParseExact(dateText, HubstaffActivityNames.DateFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
            || !int.TryParse((string?)time.Element(HubstaffActivityNames.Hours), out var hours)
            || !int.TryParse((string?)time.Element(HubstaffActivityNames.Minutes), out var minutes)
            || !int.TryParse((string?)time.Element(HubstaffActivityNames.Seconds), out var seconds)
            || hours is < 0 or > 23 || minutes is < 0 or > 59 || seconds is < 0 or > 59)
            return null;

        if (!int.TryParse((string?)reading.Element(HubstaffActivityNames.Tracked), out var tracked)
            || !int.TryParse((string?)reading.Element(HubstaffActivityNames.Mouse), out var mouse)
            || !int.TryParse((string?)reading.Element(HubstaffActivityNames.Keyboard), out var keyboard)
            || !int.TryParse((string?)reading.Element(HubstaffActivityNames.Overall), out var overall)
            || tracked <= 0 || tracked > 600 || mouse < 0 || keyboard < 0 || overall < 0
            || mouse > overall || keyboard > overall || overall > tracked || overall > mouse + keyboard)
            return null;

        var startedUtc = date.AddHours(hours).AddMinutes(minutes).AddSeconds(seconds);

        if (startedUtc > DateTime.MaxValue.AddSeconds(-600))
            return null;

        return new HubstaffActivity(account, startedUtc,
            tracked, mouse, keyboard, overall, observedUtc);
    }
}
