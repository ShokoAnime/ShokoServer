using JMMContracts.PlexAndKodi;

namespace JMMServer.PlexAndKodi
{
    public interface IProvider
    {
        MediaContainer NewMediaContainer(MediaContainerTypes type, string title = null, bool allowsync = true,
            bool nocache = true, BreadCrumbs info = null);

        System.IO.Stream GetStreamFromXmlObject<T>(T obj);
        string ServiceAddress { get; }
        int ServicePort { get; }
        bool UseBreadCrumbs { get; }
        bool AddExtraItemForSearchButtonInGroupFilters { get; }
        bool ConstructFakeIosParent { get; }
        string Proxyfy(string url);
    }
}