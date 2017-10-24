using Nancy.Rest.Module;

namespace Shoko.Server.API.v1
{
    public class MetroModule : RestModule
    {
        public MetroModule()
        {
            SetRestImplementation(new ShokoServiceImplementationMetro());
        }
    }
}