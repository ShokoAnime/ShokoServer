using System;
using System.Collections.Generic;
using NLog;
using Sentry;

namespace Shoko.Server.Utilities;

internal static class Analytics
{
    private const string AnalyticsId = "UA-128934547-1";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Send the event to Google.
    /// </summary>
    /// <param name="eventCategory">The category to store this under</param>
    /// <param name="eventAction">The action of the event</param>
    /// <param name="eventLabel">The label for the event</param>
    /// <param name="extraData">as per: https://developers.google.com/analytics/devguides/collection/protocol/v1/parameters#ec </param>
    /// <returns></returns>
    internal static bool PostEvent(string eventCategory, string eventAction, string eventLabel = null)
    {
        return PostData("event",
            new Dictionary<string, string>
            {
                { "ea", eventAction }, { "ec", eventCategory }, { "el", eventLabel ?? "" }
            });
    }

    internal static bool PostException(Exception ex, bool fatal = false)
    {
        SentrySdk.CaptureException(ex);
        return false;
    }

    private static bool PostData(string type, IDictionary<string, string> extraData)
    {
        return false;
    }

    public static void Startup()
    {
        PostEvent("Server", "Startup");
    }
}
