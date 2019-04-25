using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json.Serialization;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.SignalR;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;
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


            services.AddSwaggerGen(
                options =>
                {
                    // resolve the IApiVersionDescriptionProvider service
                    // note: that we have to build a temporary service provider here because one has not been created yet
                    var provider = services.BuildServiceProvider().GetRequiredService<IApiVersionDescriptionProvider>();

                    // add a swagger document for each discovered API version
                    // note: you might choose to skip or document deprecated API versions differently
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description));
                    }

                    // add a custom operation filter which sets default values
                    options.OperationFilter<SwaggerDefaultValues>();

                    // integrate xml comments
                    //options.IncludeXmlComments(XmlCommentsFilePath);
                });

            services.ConfigureSwaggerGen(options => { options.CustomSchemaIds(x => x.FullName); });

            services.AddApiVersioning(o =>
            {
                o.ReportApiVersions = true;
                o.AssumeDefaultVersionWhenUnspecified = true;
                o.DefaultApiVersion = new ApiVersion(1, 0);
                o.ApiVersionReader = ApiVersionReader.Combine(
                    new QueryStringApiVersionReader(),
                    new HeaderApiVersionReader("api-version"),
                    new ShokoApiReader()
                );
            });
            services.AddVersionedApiExplorer();

            services.AddSignalR();

            // this caused issues with auth. https://stackoverflow.com/questions/43574552
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddJsonOptions(json =>
                {
                    json.SerializerSettings.MaxDepth = 10;
                    json.SerializerSettings.ContractResolver = new DefaultContractResolver
                    {
                        NamingStrategy = new DefaultNamingStrategy()
                    };
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
                RequestPath = "/webui"
            });

            app.UseSwagger();
            app.UseSwaggerUI(
                options =>
                {
                    // build a swagger endpoint for each discovered API version
                    var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();
                    foreach (var description in provider.ApiVersionDescriptions)
                    {
                        options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
                    }
                });
            // Important for first run at least
            app.UseAuthentication();

            app.UseSignalR(conf =>
            {
                conf.MapHub<EventsHub>("/signalr/events");
            });

            app.UseMvc();
        }

        //static string XmlCommentsFilePath
        //{
        //    get
        //    {
        //        var fileName = typeof(Startup).GetTypeInfo().Assembly. + ".xml";
        //        return Path.Combine(basePath, fileName);
        //    }
        //}

        static Info CreateInfoForApiVersion(ApiVersionDescription description)
        {
            var info = new Info()
            {
                Title = $"Shoko API {description.ApiVersion}",
                Version = description.ApiVersion.ToString(),
                Description = "Shoko Server API.",
            };

            if (description.IsDeprecated)
            {
                info.Description += " This API version has been deprecated.";
            }

            return info;

/* Unmerged change from project 'Shoko.Server(net47)'
Before:
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
After:
        }
*/
        }
    }

    internal class ShokoApiReader : IApiVersionReader
    {
        public void AddParameters(IApiVersionParameterDescriptionContext context)
        {
            context.AddParameter(null, ApiVersionParameterLocation.Path);
        }

        public string Read(HttpRequest request)
        {
            PathString[] apiv1 =
            {
                "/v1", "/api/Image", "/api/Kodi",
                "/api/Metro", "/api/Plex"
            };

            PathString[] apiv2 =
            {
                "/api/webui", "/api/version", "/plex", "/api/init",
                /* "/api/image" */ "/api/dev", "/api/modules",
                "/api/core", "/api/links",  "/api/cast", "/api/group",
                "/api/filter", "/api/cloud", "/api/serie", "/api/ep",
                "/api/file", "/api/queue", "/api/myid",  "/api/news",
                "/api/search", "/api/remove_missing_files",
                "/api/stats_update", "/api/medainfo_update", "/api/hash",
                "/api/rescan", "/api/rescanunlinked", "/api/folder",
                "/api/rescanmanuallinks", "/api/rehash", "/api/config",
                "/api/rehashunlinked", "/api/rehashmanuallinks",
            };

            if (apiv1.Any(request.Path.StartsWithSegments))
                return "1.0";

            if (apiv2.Any(request.Path.StartsWithSegments))
                return "2.0";

            return null;
        }
    }

    public class SwaggerDefaultValues : IOperationFilter
    {
        /// <summary>
        /// Applies the filter to the specified operation using the given context.
        /// </summary>
        /// <param name="operation">The operation to apply the filter to.</param>
        /// <param name="context">The current operation filter context.</param>
        public void Apply(Operation operation, OperationFilterContext context)
        {
            if (operation.Parameters == null)
            {
                return;
            }

            // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/412
            // REF: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/pull/413
            foreach (var parameter in operation.Parameters.OfType<NonBodyParameter>())
            {
                var description = context.ApiDescription.ParameterDescriptions.First(p => p.Name == parameter.Name);
                var routeInfo = description.RouteInfo;

                if (parameter.Description == null)
                {
                    parameter.Description = description.ModelMetadata?.Description;
                }

                if (routeInfo == null)
                {
                    continue;
                }

                if (parameter.Default == null)
                {
                    parameter.Default = routeInfo.DefaultValue;
                }

                parameter.Required |= !routeInfo.IsOptional;
            }
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
