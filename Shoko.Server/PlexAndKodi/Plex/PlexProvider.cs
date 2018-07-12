using System;
using System.Text;
using Shoko.Models.PlexAndKodi;
using Nancy;

namespace Shoko.Server.PlexAndKodi.Plex
{
    public class PlexProvider : IProvider
    {
        public const string MediaTagVersion = "1461344894";

        public MediaContainer NewMediaContainer(MediaContainerTypes type, string title, bool allowsync = true,
            bool nocache = true, BreadCrumbs info = null)
        {
            MediaContainer m = new MediaContainer
            {
                AllowSync = allowsync ? "1" : "0",
                NoCache = nocache ? "1" : "0",
                MediaTagVersion = MediaTagVersion,
                Identifier = "com.plexapp.plugins.myanime",
                MediaTagPrefix = "/system/bundle/media/flags/",
                LibrarySectionTitle = "Anime"
            };
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

        public string ExcludeTags => "Kodi";

        public string ServiceAddress => ShokoServer.PathAddressPlex;
        public int ServicePort => ServerSettings.JMMServerPort;
        public bool UseBreadCrumbs => true;
        public bool ConstructFakeIosParent => true;
        public bool AutoWatch => true;
        public bool EnableRolesInLists { get; } = false;
        public bool EnableAnimeTitlesInLists { get; } = false;
        public bool EnableGenresInLists { get; } = false;

        public string Proxyfy(string url)
        {
            return "/video/jmm/proxy/" + ToHex(url);
        }

        public string ShortUrl(string url)
        {
            return url;
        }


        private static string ToHex(string ka)
        {
            byte[] ba = Encoding.UTF8.GetBytes(ka);
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        private static string FromHex(string hex)
        {
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return Encoding.UTF8.GetString(raw);
        }

        public bool AddPlexSearchItem { get; } = true;
        public bool AddPlexPrefsItem { get; } = true;
        public bool RemoveFileAttribute { get; } = true;
        public bool AddEpisodeNumberToTitlesOnUnsupportedClients { get; } = true;
        public NancyModule Nancy { get; set; }

        //public void AddResponseHeaders()
        //{
        //    if (WebOperationContext.Current != null)
        //    {
        //        WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");
        //        WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
        //    }
        //}
    }
}