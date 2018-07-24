using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using System.Linq;

namespace Shoko.Server.API
{
    public class Startup
    {
        public IHostingEnvironment HostingEnvironment { get; }
        public IConfiguration Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                //.AddJsonFile("appsettings.json")
                .SetBasePath(env.ContentRootPath);

            HostingEnvironment = env;
            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc()
                .AddJsonOptions(json => json.SerializerSettings.MaxDepth = 10);
            services.AddAuthenticationCore();
            services.AddAuthorization(auth =>
            {
                auth.AddPolicy("admin", policy => policy.Requirements.Add(new UserHandler(user => user.IsAdmin == 1)));
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //var appConfig = new AppConfiguration();
            //ConfigurationBinder.Bind(config, appConfig);

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Use((ctx, next) =>
            {

                SVR_JMMUser identity = GetRequestUser(ctx);
                if (identity != null)
                    ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);

                return next();
            });

            app.UseMvc(routes =>
            {
                //nothing as of yet.
            });

            /*app.UseOwin(x => {
                x.UseNancy(opt => {
                    opt.Bootstrapper = new Bootstrapper();
                    opt.PassThroughWhenStatusCodesAre(Nancy.HttpStatusCode.NotFound);
                });
            });*/
        }


        private static SVR_JMMUser GetRequestUser(HttpContext ctx)
        {
            if (!(ServerState.Instance?.ServerOnline ?? false)) return null;
            string apikey = ctx.Request.Headers["apikey"].FirstOrDefault()?.Trim();
            if (string.IsNullOrEmpty(apikey))
            {
                // try from query string instead
                try
                {
                    apikey = ctx.Request.Query["apikey"].First();
                }
                catch
                {
                    // ignore
                }
            }
            AuthTokens auth = Repo.AuthTokens.GetByToken(apikey);
            return auth != null ? Repo.JMMUser.GetByID(auth.UserID) : null;
        }
    }
}
