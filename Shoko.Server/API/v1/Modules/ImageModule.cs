using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
