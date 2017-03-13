using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy.Rest.Module;
using Shoko.Server.API.v1.Implementations;

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