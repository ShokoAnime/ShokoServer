using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Core.Addon;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;

namespace Shoko.Core
{
    public class APIStartup
    {
        private readonly IHostingEnvironment _env;
        private readonly IConfiguration _config;
        private readonly ILoggerFactory _loggerFactory;

        public APIStartup(IHostingEnvironment env, IConfiguration config,
            ILoggerFactory loggerFactory)
        {
            _env = env;
            _config = config;
            _loggerFactory = loggerFactory;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseSignalR(routes => {
                //Add any Shoko Core hubs here..
                
                foreach (IPlugin plugin in AddonRegistry.Plugins.Values) 
                    plugin.RegisterSignalR(routes);
            }); 
        }
    }
}
