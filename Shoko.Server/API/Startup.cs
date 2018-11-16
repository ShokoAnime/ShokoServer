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
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Versioning;

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


            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = CustomAuthOptions.DefaultScheme;
                options.DefaultChallengeScheme = CustomAuthOptions.DefaultScheme;
            }).AddScheme<CustomAuthOptions, CustomAuthHandler>(CustomAuthOptions.DefaultScheme, _ => { });

            services.AddAuthorization(auth =>
            {
                auth.AddPolicy("admin",
                    policy => policy.Requirements.Add(new UserHandler(user => user.IsAdmin == 1)));
            });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info {Title = "Shoko Desktop API", Version = "v1.0"});
                c.DocInclusionPredicate((docName, apiDesc) =>
                {
                    var actionApiVersionModel = apiDesc.ActionDescriptor?.GetApiVersion();
                    // would mean this action is unversioned and should be included everywhere
                    if (actionApiVersionModel == null)
                    {
                        return true;
                    }

                    if (actionApiVersionModel.DeclaredApiVersions.Any())
                    {
                        return actionApiVersionModel.DeclaredApiVersions.Any(v => $"v{v}" == docName);
                    }

                    return actionApiVersionModel.ImplementedApiVersions.Any(v => $"v{v}" == docName);
                });
            });

            services.ConfigureSwaggerGen(options => { options.CustomSchemaIds(x => x.FullName); });

            services.AddApiVersioning(o =>
            {
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.ApiVersionReader =  new HeaderApiVersionReader("api-version");
                
                //APIv1
                o.Conventions.Controller<ShokoServiceImplementation>()      .HasDeprecatedApiVersion(new ApiVersion(1, 0));
                o.Conventions.Controller<ShokoServiceImplementationImage>() .IsApiVersionNeutral();
                o.Conventions.Controller<ShokoServiceImplementationKodi>()  .HasDeprecatedApiVersion(new ApiVersion(1, 0));
                o.Conventions.Controller<ShokoServiceImplementationMetro>() .HasDeprecatedApiVersion(new ApiVersion(1, 0));
                o.Conventions.Controller<ShokoServiceImplementationPlex>()  .HasDeprecatedApiVersion(new ApiVersion(1, 0));
                o.Conventions.Controller<ShokoServiceImplementationStream>().HasDeprecatedApiVersion(new ApiVersion(1, 0));

            });


            // this caused issues with auth. https://stackoverflow.com/questions/43574552
            services.AddMvc()
                .AddJsonOptions(json => json.SerializerSettings.MaxDepth = 10);
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
                RequestPath = "/webui"
            });

            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));

            // Important for first run at least
            app.UseAuthentication();

            app.UseMvc();
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

    internal static class ApiExtensions
    { 
        public static ApiVersionModel GetApiVersion(this ActionDescriptor actionDescriptor)
        {
            return actionDescriptor?.Properties
                .Where(kvp => (Type)kvp.Key == typeof(ApiVersionModel))
                .Select(kvp => kvp.Value as ApiVersionModel).FirstOrDefault();
        }
    }
}
