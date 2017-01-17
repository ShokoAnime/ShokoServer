using Nancy.Rest.Module;
using Shoko.Server.API.v1.Implementations;

namespace Shoko.Server.API.v1.Modules
{
    public class KodiModule : RestModule
    {
        public KodiModule()
        {
            SetRestImplementation(new ShokoServiceImplementationKodi());
        }
    }
}
