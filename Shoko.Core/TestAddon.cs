using System;
using Autofac;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json.Linq;
using Shoko.Core.Addon;

namespace Shoko.Core
{
    [Plugin("Test")]
    public class TestAddon : IPlugin
    {
        public void RegisterSignalR(HubRouteBuilder routes)
        {
            throw new System.NotImplementedException();
        }

        [AutofacRegistrationMethod] public void RegisterAutofac() {}
        [AutofacRegistrationMethod] public static void RegisterAutofac1() {}
        [AutofacRegistrationMethod] public static void RegisterAutofac2(ContainerBuilder builder) {}

        public void LoadConfiguration(JToken config)
        {
            throw new NotImplementedException();
        }
    }
}