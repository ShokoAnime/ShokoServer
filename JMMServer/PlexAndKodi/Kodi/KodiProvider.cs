using System.IO;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Serialization;
using JMMContracts.PlexAndKodi;

namespace JMMServer.PlexAndKodi.Kodi
{
    public class KodiProvider : IProvider
    {
        public const string MediaTagVersion = "1420942002";

        public string ServiceAddress => MainWindow.PathAddressKodi;
        public int ServicePort => int.Parse(ServerSettings.JMMServerPort);
        public bool UseBreadCrumbs => false;
        public int AddExtraItemForSearchButtonInGroupFilters => 0;
        public bool ConstructFakeIosParent => false;
        public bool AutoWatch => false;

        public string Proxyfy(string url)
        {
            return url;
        }


        public MediaContainer NewMediaContainer(MediaContainerTypes type, string title = null, bool allowsync = true,
            bool nocache = true, BreadCrumbs info = null)
        {
            MediaContainer m = new MediaContainer();
            m.Title1 = m.Title2 = title;
            m.AllowSync = allowsync ? "1" : "0";
            m.NoCache = nocache ? "1" : "0";
            m.ViewMode = "65592";
            m.ViewGroup = "show";
            m.MediaTagVersion = MediaTagVersion;
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