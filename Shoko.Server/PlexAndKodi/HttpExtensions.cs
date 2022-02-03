using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Shoko.Commons.Extensions;
using Shoko.Server.PlexAndKodi.Plex;
using UPnP;

namespace Shoko.Server.PlexAndKodi
{
    public static class HttpExtensions
    {
        public static string ServerUrl(this IProvider prov, int port, string path, bool externalip = false,
            bool forcescheme = false)
        {
            Tuple<string, string> scheme_host = prov?.GetSchemeHost(externalip);
            if (scheme_host == null || forcescheme)
            {
                return "{SCHEME}://{HOST}:" + port + "/" + path;
            }
            return scheme_host.Item1 + "://" + scheme_host.Item2 + ":" + port + "/" + path;
        }

        private static Tuple<string, string> GetSchemeHost(this IProvider prov, bool externalip = false)
        {
            var req = prov?.HttpContext?.Request;
            string host = req?.Host.Host;
            string scheme = req?.Scheme;

            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(scheme)) return null;
            if (externalip)
            {
                IPAddress ip = NAT.GetExternalAddress();
                if (ip != null)
                    host = ip.ToString();
            }
            return new Tuple<string, string>(scheme, host);
        }

        public static string ReplaceSchemeHost(this IProvider prov, string str, bool externalip = false)
        {
            Tuple<string, string> scheme_host = prov.GetSchemeHost(externalip);
            if (scheme_host == null)
                scheme_host = new Tuple<string, string>("http", "127.0.0.1");
            return str?.Replace("{SCHEME}", scheme_host.Item1).Replace("{HOST}", scheme_host.Item2);
        }

        public static string GetQueryParameter(this IProvider prov, string name)
        {
            if (prov?.HttpContext?.Request != null)
            {
                return prov.HttpContext.Request.Query[name];
            }

            return null;
        }

        public static bool IsExternalRequest(this IProvider prov)
        {
            if (!NAT.UPnPPortAvailable)
                return false;
            string extarnalhost = prov.GetQueryParameter("externalhost");
            if (extarnalhost == null || extarnalhost == "0")
                return false;
            return true;
        }

        public static string RequestHeader(this IProvider prov, string name)
        {
            if (prov?.HttpContext?.Request?.Headers != null)
            {
                if (prov.HttpContext.Request.Headers.Keys.Contains(name))
                    return prov.HttpContext.Request.Headers[name].ElementAt(0);
            }

            return null;
        }


        public static PlexDeviceInfo GetPlexClient(this IProvider prov)
        {
            string product = prov.RequestHeader("X-Plex-Product");
            string device = prov.RequestHeader("X-Plex-Device");
            string version = prov.RequestHeader("X-Plex-Version");
            string platform = prov.RequestHeader("X-Plex-Platform");
            return new PlexDeviceInfo(device, product, version, platform);
        }

        public static void AddResponseHeaders(this IProvider prov, Dictionary<string, string> headers,
            string contentype = null)
        {
            if (prov?.HttpContext?.Response != null)
            {
                headers.Select(a => (a.Key, a.Value)).ForEach(a => prov.HttpContext.Response.Headers[a.Key] = a.Value);
                prov.HttpContext.Response.ContentType = contentype;
            }
        }

        public static Dictionary<string, string> GetOptions()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("X-Plex-Protocol", "1.0");
            headers.Add("Access-Control-Allow-Origin", "*");
            headers.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS, DELETE, PUT, HEAD");
            headers.Add("Access-Control-Max-Age", "1209600");
            headers.Add("Access-Control-Allow-Headers",
                "accept, x-plex-token, x-plex-client-identifier, x-plex-username, x-plex-product, x-plex-device, x-plex-platform, x-plex-platform-version, x-plex-version, x-plex-device-name");
            return headers;
        }
    }
}