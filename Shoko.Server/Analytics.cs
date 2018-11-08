using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Shoko.Server.Extensions;

namespace Shoko.Server
{
    internal static class Analytics
    {
        private const string AnalyticsId = "UA-128934547-1";

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
        internal static bool PostEvent(string eventCategory, string eventAction, string eventLabel = null, IDictionary<string, string> extraData = default)
        {
            if (ServerSettings.GA_OptOutPlzDont) return false;

            using (var client = new HttpClient())
            {
                //
                var data = new Dictionary<string, string>
                {
                    {"an", "Shoko Server"},
                    {"t", "event"},
                    {"tid",  AnalyticsId},
                    {"cid",  ServerSettings.GA_ClientId.ToString()},
                    {"v", "1"},
                    {"av", Utils.GetApplicationVersion()},
                    {"ea", eventAction},
                    {"ec", eventCategory},
                    {"aip", "1"}
                };
                data.AddRange(extraData);

                var resp = client.PostAsync($"{Endpoint}/collect", new FormUrlEncodedContent(data)).ConfigureAwait(false).GetAwaiter().GetResult();

                return true;
            }
        }

        public static void Startup()
        {
            PostEvent("Server", "Startup");
        }
    }
}
