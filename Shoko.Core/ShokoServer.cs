using System;
using System.Linq;
using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Web;
using Shoko.Core.Addon;
using Shoko.Core.Config;

namespace Shoko.Core
{
    public static class ShokoServer
    {
        /// <summary>
        /// The Autofac container for any dependancy resolution, this is populated with any plugin types during the server startup.
        /// </summary>
        public static IContainer AutofacContainer { get; internal set; }
        internal static ContainerBuilder AutofacContainerBuilder { get; set; }

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger(); //I would make this AutoFac, though it's generally a static class variable for speed.

        private static IWebHost _webhost;

        private static void SetupAutofac() 
        {
            AutofacContainerBuilder = new ContainerBuilder();
            AutofacContainerBuilder.RegisterType<Database.CoreDbContext>();
            AutofacContainerBuilder.RegisterType<API.Services.UserService>().As<API.Services.IUserService>();

            //TODO: Add any Shoko.Core autofac things here.
            AddonRegistry.RegisterAutofac(AutofacContainerBuilder);
        }

        private static void StartWebHost()
        {
            if (_webhost != null) return;
            _webhost = new WebHostBuilder().UseKestrel(conf =>
            {
                //TODO: When implementing the config API, make this part of the config
                conf.ListenAnyIP(ConfigurationLoader.CoreConfig.ServerPort);
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
            //We need to call this before Autofac else this will not actually register any plugins.
            AddonRegistry.LoadPluigins();
            //Load core config, this is for things like the webhost.
            ConfigurationLoader.LoadConfig();
            SetupAutofac();

            //Autofac container build is called in here, so we need this to be after the setup.
            StartWebHost();
            AddonRegistry.Init();
            //Load the plugin config
            ConfigurationLoader.LoadPluginConfig();

#if DEBUG
            //TODO: Move to migrations.
            using (var ctx = new Database.CoreDbContext())
            {
                ctx.Database.EnsureCreated();
                if (ctx.Users.Count() == 0)
                {
                    ctx.Users.Add(new Models.ShokoUser()
                    {
                        Username = "admin",
                        IsAdmin = true,
                    });
                    ctx.SaveChanges();
                }
            }
#endif
        }
    }
}