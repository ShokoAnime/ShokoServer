using Nancy.Rest.Module;

namespace Shoko.Server.API.v1.Modules
{
    public class StreamModule : RestModule
    {
        public StreamModule()
        {
            SetRestImplementation(new ShokoServiceImplementationStream());
        }
    }
}