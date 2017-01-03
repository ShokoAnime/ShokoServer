using JMMContracts.PlexAndKodi;
using Nancy;

namespace JMMServer.PlexAndKodi
{
    public interface IProvider
    {
        MediaContainer NewMediaContainer(MediaContainerTypes type, string title = null, bool allowsync = true,
            bool nocache = true, BreadCrumbs info = null);

        //void AddResponseHeaders();
        string ServiceAddress { get; }
        int ServicePort { get; }
        bool UseBreadCrumbs { get; }
        bool ConstructFakeIosParent { get; }
        bool AutoWatch { get; }
        string Proxyfy(string url);
        string ShortUrl(string url);
        bool EnableRolesInLists { get; }
        bool EnableAnimeTitlesInLists { get; } 
        bool EnableGenresInLists { get; }
        bool AddPlexSearchItem { get;  }
        bool AddPlexPrefsItem { get;  }
        bool RemoveFileAttribute { get; } // This will force the transcoder in plex to use the stream instead the file.
        bool AddEpisodeNumberToTitlesOnUnsupportedClients { get;  }
        NancyModule Nancy { get; set; }


    }
}