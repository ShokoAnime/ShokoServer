using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.ServiceModel.Web;
using JMMContracts.PlexAndKodi;

namespace JMMServer.PlexAndKodi
{
    public class PlexObject
    {
        public static NameValueCollection QueryParameters
        {
            get
            {
                if (WebOperationContext.Current == null)
                    return new NameValueCollection();
                return WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters;
            }
        }

        public int Start { get; set; }
        public int Size { get; set; }

        public MediaContainer MediaContainer { get; private set; }

        public static bool IsExternalRequest
        {
            get
            {
                if (!FileServer.FileServer.UPnPPortAvailable)
                    return false;
                if (QueryParameters.AllKeys.Contains("externalhost"))
                {
                    if (QueryParameters["externalhost"] != "0")
                        return true;
                }
                return false;
            }
        }

        private List<Video> LimitVideos(List<Video> list)
        {
            MediaContainer.TotalSize = list.Count.ToString();
            MediaContainer.Offset = Start.ToString();
            int size = Size > list.Count - Start ? list.Count - Start : Size;
            MediaContainer.TotalSize = list.Count.ToString();
            MediaContainer.Size = size.ToString();
            return list.Skip(Start).Take(size).ToList();
        }

        public List<Video> Childrens
        {
            get { return MediaContainer.Childrens; }
            set { MediaContainer.Childrens = LimitVideos(value); }
        }


        public MediaContainer GetStream(IProvider prov)
        {
            if (MediaContainer.Childrens.Count > 0 && MediaContainer.Childrens[0].Type == "movie")
            {
                MediaContainer.ViewGroup = null;
                MediaContainer.ViewMode = null;
            }
            bool removeandroid = false;

            if (WebOperationContext.Current != null &&
                WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
            {
                //Fix for android hang, if the type is populated
                string kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                if (kh.Contains("ANDROID"))
                    removeandroid = true;
            }
            MediaContainer.Childrens.ForEach(a =>
            {
                a.Group = null;
                if (removeandroid)
                    a.Type = null;
            });
            return MediaContainer;
        }

        public PlexObject(MediaContainer m)
        {
            MediaContainer = m;
        }

        public bool Init()
        {
            Start = 0;
            Size = int.MaxValue;
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                if (WebOperationContext.Current.IncomingRequest.Method == "OPTIONS")
                {
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods",
                        "POST, GET, OPTIONS, DELETE, PUT, HEAD");
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Max-Age", "1209600");
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers",
                        "accept, x-plex-token, x-plex-client-identifier, x-plex-username, x-plex-product, x-plex-device, x-plex-platform, x-plex-platform-version, x-plex-version, x-plex-device-name");
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Connection", "close");
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");
                    WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                    WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain";
                    return false;
                }
                if ((WebOperationContext.Current.IncomingRequest.UriTemplateMatch != null) &&
                    (WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters != null))
                {
                    if (
                        WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters.AllKeys.Contains(
                            "X-Plex-Container-Size"))
                    {
                        int max =
                            int.Parse(
                                WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters[
                                    "X-Plex-Container-Size"]);
                        if (max < Size)
                            Size = max;
                    }
                }
            }
            return true;
        }
    }
}