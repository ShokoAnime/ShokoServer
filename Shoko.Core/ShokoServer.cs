using System;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;
using Shoko.Core.Addon;

namespace Shoko.Core
{
    public static class ShokoServer
    {
        public static IContainer AutofacContainer { get; internal set; }
        internal static ContainerBuilder AutofacContainerBuilder { get; set; }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger(); //I would make this AutoFac, though it's generally a static class variable for speed.

        private static IWebHost _webhost;

        private static void SetupAutofac() 
        {
            AutofacContainerBuilder = new ContainerBuilder();

            //TODO: Add any Shoko.Core autofac things here.
            AddonRegistry.RegisterAutofac(AutofacContainerBuilder);
        }

        private static void StartWebHost()
        {
            if (_webhost != null) return;
            _webhost = new WebHostBuilder().UseKestrel(conf =>
            {
                //TODO: When implementing the config API, make this part of the config
                conf.ListenAnyIP(8111);
            }).UseStartup<APIStartup>()
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
#if DEBUG
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
#else
                logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Error);
#endif
            })
            .UseNLog()
            .Build();

            try
            {
                _webhost.Start();
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
            }
        }

        public static void Init()
        {
            SetupAutofac();

            //Autofac container build is called in here, so we need this to be after the setup.
            StartWebHost();
            AddonRegistry.Init();
        }
    }
}