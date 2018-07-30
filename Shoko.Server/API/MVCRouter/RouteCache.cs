using System.Collections.Generic;

namespace Shoko.Server.API.MVCRouter
{
    internal class RouteCache
    {
        public string ModulePath { get; set; }
        public List<RouteCacheItem> Items { get; set; }
    }
}
