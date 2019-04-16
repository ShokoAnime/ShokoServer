using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.ServiceModel.Channels;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using FluentNHibernate.Mapping;
using Nancy;
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

            string host = req?.Host.Host ?? WebOperationContext.Current?.IncomingRequest?.UriTemplateMatch
                              ?.RequestUri.Host;
            string scheme = req?.Scheme ?? WebOperationContext.Current?.IncomingRequest?.UriTemplateMatch
                                ?.RequestUri.Scheme;
            if (host == null)
            {
                var context = System.ServiceModel.OperationContext.Current;
                if (context != null && context.IncomingMessageHeaders?.To != null)
                {
                    Uri ur = context.IncomingMessageHeaders?.To;
                    host = ur.Host;
                    scheme = ur.Scheme;
                }
            }
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
            if (WebOperationContext.Current == null)
                return null;
            if (WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters.AllKeys.Contains(name))
                return WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters[name];
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
            else if (WebOperationContext.Current != null &&
                     WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains(name))
                return WebOperationContext.Current.IncomingRequest.Headers[name];
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
            else if (WebOperationContext.Current != null)
            {
                foreach (string n in headers.Keys)
                    WebOperationContext.Current.OutgoingResponse.Headers.Add(n, headers[n]);
                if (contentype != null)
                    WebOperationContext.Current.OutgoingResponse.ContentType = contentype;
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