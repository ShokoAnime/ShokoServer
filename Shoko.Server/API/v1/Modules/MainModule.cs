#if false
using Nancy.Rest.Module;

namespace Shoko.Server.API.v1
{
    public class MainModule : RestModule
    {
        public MainModule()
        {
            SetRestImplementation(new ShokoServiceImplementation());
        }
    }
}
#endif