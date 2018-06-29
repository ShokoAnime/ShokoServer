using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Nancy.Owin;

namespace Shoko.Server.API
{
    public class Startup
    {
        private readonly IConfiguration config;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                //.AddJsonFile("appsettings.json")
                .SetBasePath(env.ContentRootPath);

            config = builder.Build();
        }

        public void Configure(IApplicationBuilder app)
        {
            //var appConfig = new AppConfiguration();
            //ConfigurationBinder.Bind(config, appConfig);

            
            app.UseOwin(x => {
                x.UseNancy(opt => {
                    opt.Bootstrapper = new Bootstrapper();
                    opt.PassThroughWhenStatusCodesAre(Nancy.HttpStatusCode.NotFound);
                });
            });
        }
    }
}
