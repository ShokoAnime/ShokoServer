using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Nancy.Rest.Annotations.Atributes;
using Shoko.Models.Server;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.MVCRouter;
using Shoko.Server.API.v1.Implementations;
using Shoko.Server.Models;
using Shoko.Server.Repositories;
using Swashbuckle.AspNetCore.Swagger;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

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

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CustomAuthOptions.DefaultScheme;
                options.DefaultChallengeScheme = CustomAuthOptions.DefaultScheme;
            }).AddScheme<CustomAuthOptions, CustomAuthHandler>(CustomAuthOptions.DefaultScheme, _ => { });

            services.AddAuthorization(auth =>
            {
                auth.AddPolicy("admin", policy => policy.Requirements.Add(new UserHandler(user => user.IsAdmin == 1)));
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Shoko Server API", Version = "v1" });
            });

            services.ConfigureSwaggerGen(options =>
            {               
                options.CustomSchemaIds(x => x.FullName);
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            //var appConfig = new AppConfiguration();
            //ConfigurationBinder.Bind(config, appConfig);

#if DEBUG
            app.UseDeveloperExceptionPage();
#endif

            string codeBase = Assembly.GetExecutingAssembly().CodeBase;
            UriBuilder uri = new UriBuilder(codeBase);
            string path = Uri.UnescapeDataString(uri.Path);

            var dir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(path), "webui"));
            if (!dir.Exists) dir.Create();

            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(dir.FullName),
                RequestPath  = "/webui"
            });

            /*app.Use((ctx, next) =>
            {
                SVR_JMMUser identity = GetRequestUser(ctx);
                if (identity != null)
                    ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);

                return next();
            });*/

            /*app.UseRouter(routes =>
            {
                routes
                    .RouteFor(new ShokoServiceImplementation())
                    .RouteFor(new ShokoServiceImplementationImage())
                    .RouteFor(new ShokoServiceImplementationKodi())
                    .RouteFor(new ShokoServiceImplementationMetro())
                    .RouteFor(new ShokoServiceImplementationPlex())
                    .RouteFor(new ShokoServiceImplementationStream());
            });*/

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));

            app.UseMvc();

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
            AuthTokens auth = Repo.Instance.AuthTokens.GetByToken(apikey);
            return auth != null ? Repo.Instance.JMMUser.GetByID(auth.UserID) : null;
        }
    }
}
