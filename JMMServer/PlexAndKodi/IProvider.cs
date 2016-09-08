using JMMContracts.PlexAndKodi;

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
        int AddExtraItemForSearchButtonInGroupFilters { get; }
        bool ConstructFakeIosParent { get; }
        bool AutoWatch { get; }
        string Proxyfy(string url);
        string ShortUrl(string url);
    }
}