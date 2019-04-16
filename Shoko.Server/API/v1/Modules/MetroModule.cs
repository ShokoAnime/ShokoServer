#if false
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
#endif