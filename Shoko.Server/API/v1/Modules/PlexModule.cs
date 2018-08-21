#if false
using Nancy.Rest.Module;
using Shoko.Server.API.v1.Implementations;

namespace Shoko.Server.API.v1.Modules
{
    public class PlexModule : RestModule
    {
        public PlexModule()
        {
            SetRestImplementation(new ShokoServiceImplementationPlex());
        }
    }
}
#endif