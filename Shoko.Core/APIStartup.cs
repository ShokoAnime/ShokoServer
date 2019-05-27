using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Core.Addon;
using Shoko.Core.Extensions;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Autofac;

namespace Shoko.Core
{
    public class APIStartup
    {
        private readonly IConfiguration _config;
        private readonly ILoggerFactory _loggerFactory;

        public APIStartup(IHostingEnvironment env, IConfiguration config,
            ILoggerFactory loggerFactory)
        {
            _config = config;
            _loggerFactory = loggerFactory;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();

            var mvc = services.AddMvc();
            foreach ((string _, IPlugin plugin) in AddonRegistry.Plugins)
            {
                mvc.AddApplicationPart(plugin.GetType().Assembly).AddControllersAsServices();
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSignalR(routes => {
                //Add any Shoko Core hubs here..
                
                foreach (ISignalRPlugin plugin in AddonRegistry.Plugins.Values.AsEnumerable().Where(pl => pl is ISignalRPlugin).Select(pl => (ISignalRPlugin) pl)) 
                    plugin.RegisterSignalR(routes);
            }); 
        }
    }
}
