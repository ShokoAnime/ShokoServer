using System.IO;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Serialization;
using JMMContracts.PlexAndKodi;
using System.Text.RegularExpressions;

namespace JMMServer.PlexAndKodi.Kodi
{
    public class KodiProvider : IProvider
    {
        //public const string MediaTagVersion = "1420942002";

        public string ServiceAddress => MainWindow.PathAddressKodi;
        public int ServicePort => int.Parse(ServerSettings.JMMServerPort);
        public bool UseBreadCrumbs => false; // turn off breadcrumbs navigation (plex)
        public int AddExtraItemForSearchButtonInGroupFilters => 0; // dont add item count for search (plex)
        public bool ConstructFakeIosParent => false; //turn off plex workaround for ios (plex)
        public bool AutoWatch => false; //turn off marking watched on stream side (plex)

        public bool EnableRolesInLists { get; } =true;
        public bool EnableAnimeTitlesInLists { get; } = true;
        public bool EnableGenresInLists { get; } = true;


        private static Regex _removeIp=new Regex(@"(\d+\.\d+\.\d+\.\d+):(\d+)",RegexOptions.Compiled);

        public string Proxyfy(string url)
        {
            return url;
        }

        public string ShortUrl(string url)
        {
            Match remove_this = _removeIp.Match(url);
            if (remove_this.Success)
            {
                //remove http, host, port because we already know whats that
                //return url.Substring(url.IndexOf(":" + ServicePort + "/") + ServicePort.ToString().Length + 2);
                return url.Replace("http://","").Replace(remove_this.Groups[1].Value, "");
            }
            else
            {
                return url;
            }
        }

        public MediaContainer NewMediaContainer(MediaContainerTypes type, string title = null, bool allowsync = false, bool nocache = false, BreadCrumbs info = null)
        {
            MediaContainer m = new MediaContainer();
            m.Title1 = m.Title2 = title;
            // not needed
            //m.AllowSync = allowsync ? "1" : "0";
            //m.NoCache = nocache ? "1" : "0";
            //m.ViewMode = "65592";
            //m.ViewGroup = "show";
            //m.MediaTagVersion = MediaTagVersion;
            m.Identifier = "plugin.video.nakamori";
            return m;
        }

        public void AddResponseHeaders()
        {
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Nakamori-Protocol", "1.0");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
            }
        }


    }
}