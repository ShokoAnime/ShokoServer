using System;
using System.IO;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Serialization;
using JMMContracts.PlexAndKodi;

namespace JMMServer.PlexAndKodi.Plex
{
    public class PlexProvider : IProvider
    {
        public const string MediaTagVersion = "1461344894";

        public MediaContainer NewMediaContainer(MediaContainerTypes type, string title, bool allowsync = true,
            bool nocache = true, BreadCrumbs info = null)
        {
            MediaContainer m = new MediaContainer();
            m.AllowSync = allowsync ? "1" : "0";
            m.NoCache = nocache ? "1" : "0";
            m.MediaTagVersion = MediaTagVersion;
            m.Identifier = "com.plexapp.plugins.myanime";
            m.MediaTagPrefix = "/system/bundle/media/flags/";
            m.LibrarySectionTitle = "Anime";
            if (type != MediaContainerTypes.None)
                info?.FillInfo(this, m, false, false);
            m.Thumb = null;
            switch (type)
            {
                case MediaContainerTypes.Show:
                    m.ViewGroup = "show";
                    m.ViewMode = "65592";
                    break;
                case MediaContainerTypes.Episode:
                    m.ViewGroup = "episode";
                    m.ViewMode = "65592";
                    break;
                case MediaContainerTypes.Video:
                    m.ViewMode = "65586";
                    m.ViewGroup = "video";
                    break;
                case MediaContainerTypes.Season:
                    m.ViewMode = "131132";
                    m.ViewGroup = "season";
                    break;
                case MediaContainerTypes.Movie:
                    m.ViewGroup = "movie";
                    m.ViewMode = "65592";
                    break;
                case MediaContainerTypes.File:
                    break;
            }
            return m;
        }


        public string ServiceAddress => MainWindow.PathAddressPlex;
        public int ServicePort => int.Parse(ServerSettings.JMMServerPort);
        public bool UseBreadCrumbs => true;
        public bool AddExtraItemForSearchButtonInGroupFilters => true;
        public bool ConstructFakeIosParent => true;

        public string Proxyfy(string url)
        {
            return "/video/jmm/proxy/" + ToHex(url);
        }

        private static string ToHex(string ka)
        {
            byte[] ba = Encoding.UTF8.GetBytes(ka);
            StringBuilder hex = new StringBuilder(ba.Length*2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private static string FromHex(string hex)
        {
            byte[] raw = new byte[hex.Length/2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i*2, 2), 16);
            }
            return Encoding.UTF8.GetString(raw);
        }

        public System.IO.Stream GetStreamFromXmlObject<T>(T obj)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            Utf8StringWriter textWriter = new Utf8StringWriter();
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                WebOperationContext.Current.OutgoingResponse.ContentType = "application/xml";
            }
            xmlSerializer.Serialize(textWriter, obj, ns);
            return new MemoryStream(Encoding.UTF8.GetBytes(textWriter.ToString()));
        }
    }
}