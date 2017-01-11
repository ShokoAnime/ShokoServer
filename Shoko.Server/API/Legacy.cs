using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nancy;
using Nancy.Rest.Annotations;
using Nancy.Rest.Module;

namespace Shoko.Server.API
{
    public class Legacy : RestModule
    {
        public Legacy()
        {
            SetRestImplementation(new JMMServiceImplementation());
        }
    }
}
