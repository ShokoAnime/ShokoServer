using System;
using Autofac;
using Shoko.Core.Addon;

namespace Shoko.Core
{
    public class ShokoServer
    {
        public static IContainer AutofacContainer { get; set; }

        public static void SetupAutofac() 
        {
            var builder = new ContainerBuilder();

            AddonRegistry.RegisterAutofac(builder);

            AutofacContainer = builder.Build();
        }
    }
}