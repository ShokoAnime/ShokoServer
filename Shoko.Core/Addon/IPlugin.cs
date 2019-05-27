using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Microsoft.AspNetCore.SignalR;

namespace Shoko.Core.Addon
{
    interface IPlugin
    {
        // void RegisterAutofac(ContainerBuilder bulder);
        void RegisterSignalR(HubRouteBuilder routes);
    }
}
