using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using NLog;
using Sentry;
using Shoko.Server.Extensions;
using Shoko.Server.Settings;

namespace Shoko.Server.Utilities
{
    internal static class Analytics
    {
        private const string AnalyticsId = "UA-128934547-1";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

//#if !DEBUG
        //private const string Endpoint = "https://www.google-analytics.com/debug";
//#else
        private const string Endpoint = "https://www.google-analytics.com";
//#endif
        /// <summary>
        /// Send the event to Google.
        /// </summary>
        /// <param name="eventCategory">The category to store this under</param>
        /// <param name="eventAction">The action of the event</param>
        /// <param name="eventLabel">The label for the event</param>
        /// <param name="extraData">as per: https://developers.google.com/analytics/devguides/collection/protocol/v1/parameters#ec </param>
        /// <returns></returns>
        internal static bool PostEvent(string eventCategory, string eventAction, string eventLabel = null) => PostData("event", new Dictionary<string, string>
            {
                {"ea", eventAction},
                {"ec", eventCategory},
                {"el", eventLabel ?? "" }
            });

        internal static bool PostException(Exception ex, bool fatal = false)
        {
                SentrySdk.CaptureException(ex);

                return PostData("exception",
                new Dictionary<string, string>
                {
                    {"exd", ex.GetType().FullName},
                    {"exf", (fatal ? 1 : 0).ToString()}
                });
        }

        private static bool PostData(string type, IDictionary<string, string> extraData)
        {
            if (ServerSettings.Instance.GA_OptOutPlzDont) return false;

            try
            {
                using (var client = new HttpClient())
                {
                    var data = new Dictionary<string, string>();
                    data.Add("t", type);
                    data.Add("an", "Shoko Server");
                    data.Add("tid", AnalyticsId);
                    data.Add("cid", ServerSettings.Instance.GA_Client.ToString());
                    data.Add("v", "1");
                    data.Add("av", Utils.GetApplicationVersion());
                    data.Add("ul", CultureInfo.GetCultureInfo(ServerSettings.Instance.Culture).DisplayName);
                    data.Add("aip", "1");
                    data.AddRange(extraData);

                    var resp = client.PostAsync($"{Endpoint}/collect", new FormUrlEncodedContent(data))
                        .ConfigureAwait(false).GetAwaiter().GetResult();

                    return true;
                }
            }
            catch (HttpRequestException ex)
            {
                Logger.Error("There was an error posting to Google Analytics", ex);
                return false;
            }
        }

        public static void Startup()
        {
            PostEvent("Server", "Startup");
        }
    }
}
