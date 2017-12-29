using Nancy.Rest.Module;

namespace Shoko.Server.API.v1
{
    public class ImageModule : RestModule
    {
        public ImageModule()
        {
            SetRestImplementation(new ShokoServiceImplementationImage());
        }
    }
}