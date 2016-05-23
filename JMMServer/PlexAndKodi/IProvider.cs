using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JMMContracts;
using JMMContracts.PlexAndKodi;

namespace JMMServer.PlexAndKodi
{
    public interface IProvider
    {
        MediaContainer NewMediaContainer(MediaContainerTypes type, string title = null, bool allowsync = true, bool nocache = true, Breadcrumbs info = null);
        System.IO.Stream GetStreamFromXmlObject<T>(T obj);
        string Serviceddress { get; }
        int ServicePort { get; }
        bool UserBreadCrumbs { get; }
        bool AddExtraItemForSearchButtonInGroupFilters { get; }
        bool ConstructFakeIosParent { get;  }
        string Proxyfy(string url);
    }
}
