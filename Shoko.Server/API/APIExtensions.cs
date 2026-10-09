using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using Newtonsoft.Json;
using Sentry;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Utilities;
using Shoko.Abstractions.Web.SignalR;
using Shoko.Server.API.ActionConstraints;
using Shoko.Server.API.ActionFilters;
using Shoko.Server.API.Annotations;
using Shoko.Server.API.Authentication;
using Shoko.Server.API.FileProviders;
using Shoko.Server.API.ModelBinders;
using Shoko.Server.API.Resolvers;
using Shoko.Server.API.SignalR;
using Shoko.Server.API.SignalR.Aggregate;
using Shoko.Server.API.Swagger;
using Shoko.Server.API.v3.Helpers;
using Shoko.Server.Server;
using Shoko.Server.Services;
using Shoko.Server.Settings;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;

using File = System.IO.File;

#pragma warning disable CS0618
namespace Shoko.Server.API;

public static partial class APIExtensions
{
    public static IServiceCollection AddAPI(this IServiceCollection services, IPluginManager pluginManager)
    {
        services.AddSingleton<LoggingEmitter>();
        services.AddSingleton<IEventEmitter, AiringEventEmitter>();
        services.AddSingleton<IEventEmitter, AnidbEventEmitter>();
        services.AddSingleton<IEventEmitter, AvdumpEventEmitter>();
        services.AddSingleton<IEventEmitter, ConfigurationEventEmitter>();
        services.AddSingleton<IEventEmitter, FileEventEmitter>();
        services.AddSingleton<IEventEmitter, ManagedFolderEventEmitter>();
        services.AddSingleton<IEventEmitter, MetadataEventEmitter>();
        services.AddSingleton<IEventEmitter, NetworkEventEmitter>();
        services.AddSingleton<IEventEmitter, QueueEventEmitter>();
        services.AddSingleton<IEventEmitter, ReleaseEventEmitter>();
        services.AddSingleton<IEventEmitter, RestartEventEmitter>();
        services.AddSingleton<IEventEmitter, SuspensionEventEmitter>();
        services.AddSingleton<IEventEmitter, UserDataEventEmitter>();
        services.AddSingleton<IEventEmitter, UserEventEmitter>();
        services.AddSingleton<IEventEmitter, GroupEventEmitter>();
        services.AddSingleton<IEventEmitter, PluginEventEmitter>();
        services.AddSingleton<EventEmitterRegistry>();
        services.AddScoped<GeneratedPlaylistService>();
        services.AddScoped<FilterFactory>();
        services.AddSingleton<MetadataModelBuilder>();
        services.AddSingleton<MetadataSourceActions>();
        services.AddScoped<WebUIFactory>();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = CustomAuthOptions.DefaultScheme;
            options.DefaultChallengeScheme = CustomAuthOptions.DefaultScheme;
        }).AddScheme<CustomAuthOptions, CustomAuthHandler>(CustomAuthOptions.DefaultScheme, _ => { });

        services.AddAuthorization(auth =>
        {
            auth.AddPolicy("admin",
                policy => policy.Requirements.Add(new UserHandler(user => user.IsAdmin == 1)));
            auth.AddPolicy("init",
                policy => policy.Requirements.Add(new UserHandler(user =>
                    user.JMMUserID == 0 && user.Username == "init")));
        });

        // Before MVC reads its route templates.
        services.Configure<RouteOptions>(options =>
        {
            options.ConstraintMap[MetadataSourceRouteConstraint.Name] = typeof(MetadataSourceRouteConstraint);
            options.ConstraintMap[MetadataEntityTypeRouteConstraint.Name] = typeof(MetadataEntityTypeRouteConstraint);
        });

        services.AddSwaggerGen(
            options =>
            {
                // Resolve the services we'll need from the static service provider
                // since the app is running at this point.
                var provider = ISystemService.StaticServices.GetRequiredService<IApiVersionDescriptionProvider>();
                var webSettings = ISettingsProvider.Instance.GetSettings().Web;

                // Add a swagger document for each discovered API version (server-only).
                foreach (var description in provider.ApiVersionDescriptions.OrderByDescending(a => a.ApiVersion))
                {
                    if (description.GroupName is "v2" or "v2.1" && !webSettings.EnableAPIv2)
                        continue;
                    if (description.GroupName is "v3" && !webSettings.EnableAPIv3)
                        continue;
                    if (description.GroupName is not ("v2" or "v2.1" or "v3"))
                        continue;
                    options.SwaggerDoc(description.GroupName, CreateInfoForApiVersion(description, "Shoko Server"));
                }

                // Add a swagger document for each plugin's API versions that it has controllers
                // for, and one for the endpoints it may map itself.
                foreach (var document in GetPluginDocuments(pluginManager, provider))
                    options.SwaggerDoc(document.Name, document.Info);

                // Use document inclusion predicate to separate server and plugin controllers.
                options.DocInclusionPredicate(new PluginDocumentInclusionPredicate(pluginManager).Include);

                options.AddSecurityDefinition(AuthorizeOperationFilter.ApiKeyHeaderScheme,
                    new OpenApiSecurityScheme()
                    {
                        Description = "Shoko API Key Header",
                        Name = "apikey",
                        In = ParameterLocation.Header,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "apikey"
                    });
                options.AddSecurityDefinition(AuthorizeOperationFilter.ApiKeyQueryScheme,
                    new OpenApiSecurityScheme()
                    {
                        Description = "Shoko API Key Query Parameter. Useful for clients that can't set headers, such as media players.",
                        Name = "apikey",
                        In = ParameterLocation.Query,
                        Type = SecuritySchemeType.ApiKey,
                        Scheme = "apikey"
                    });
                options.OperationFilter<AuthorizeOperationFilter>();
                options.OperationFilter<MetadataRouteParameterFilter>();

                // integrate xml comments
                //Locate the XML file being generated by ASP.NET...
                var xmlFile = "Shoko.Server.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }

                options.AddPlugins(pluginManager);

                var v3Enums = typeof(APIExtensions).Assembly.GetTypes()
                    .Concat(typeof(TitleLanguage).Assembly.GetTypes())
                    .Where(a => a.IsEnum)
                    .Where(a =>
                        (a.FullName?.StartsWith("Shoko.Server.API.v3.", StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                        (a.FullName?.StartsWith("Shoko.Abstractions.", StringComparison.InvariantCultureIgnoreCase) ?? false) ||
                        (a.FullName?.StartsWith("Shoko.Server.Providers.", StringComparison.InvariantCultureIgnoreCase) ?? false)
                    )
                    .Concat([
                        typeof(DayOfWeek),
                        typeof(DriveType),
                        typeof(CreatorRoleType),
                    ])
                    .ToList();
                foreach (var type in v3Enums)
                {
                    var descriptorType = typeof(EnumSchemaFilter<>).MakeGenericType(type);
                    options.SchemaFilterDescriptors.Add(new FilterDescriptor
                    {
                        Type = descriptorType,
                        Arguments = []
                    });
                }

                options.SchemaFilter<MetadataBodyFieldSchemaFilter>();
                options.MapType<MetadataSource>(MetadataSourceSchema.Create);
                options.MapType<MetadataEntityType>(MetadataEntityTypeSchema.Create);
                options.MapType<PartialDateOnly>(PartialDateSchemas.CreatePartialDateOnly);
                options.MapType<FuzzyDateOnly>(PartialDateSchemas.CreateFuzzyDateOnly);
                options.CustomSchemaIds(GetTypeName);
            });
        services.AddSwaggerGenNewtonsoftSupport();

        // Describe the endpoints plugins map themselves, next to the controllers.
        services.AddEndpointsApiExplorer();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IApiDescriptionProvider, RequestDelegateApiDescriptionProvider>());

        services.AddSingleton<ActorHubFilter>();
        services.AddSignalR(options =>
            {
                options.EnableDetailedErrors = true;
                options.ClientTimeoutInterval = TimeSpan.FromSeconds(60); // default timeout is 30 seconds
                // Every hub call runs for the connection's token.
                options.AddFilter<ActorHubFilter>();
            })
            .AddNewtonsoftJsonProtocol(o => o.PayloadSerializerSettings.ContractResolver = new ApiContractResolver());

        // allow CORS calls from other both local and non-local hosts
        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder => { builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader(); });
        });

        // this caused issues with auth. https://stackoverflow.com/questions/43574552
        services.AddMvc(options =>
            {
                options.EnableEndpointRouting = false;
                options.AllowEmptyInputInBodyModelBinding = true;
                foreach (var formatter in options.InputFormatters)
                {
                    if (formatter.GetType() == typeof(NewtonsoftJsonInputFormatter))
                    {
                        ((NewtonsoftJsonInputFormatter)formatter).SupportedMediaTypes.Add(
                            MediaTypeHeaderValue.Parse("text/plain"));
                    }
                }

                options.Filters.Add(typeof(DatabaseBlockedFilter));
                options.Filters.Add(typeof(ManualLinkChangeFilter));

                // Any route may reach a provider, so a provider out of reach or not configured is answered the same way everywhere.
                options.Filters.Add(new MetadataProviderUnavailableAttribute());

                // Before the simple type binder, which would read any valid value through the type converter.
                options.ModelBinderProviders.Insert(0, new MetadataSourceModelBinderProvider());
                options.ModelBinderProviders.Insert(0, new MetadataEntityTypeModelBinderProvider());
                options.ModelBinderProviders.Insert(0, new EnumMemberModelBinderProvider());

                EmitEmptyEnumerableInsteadOfNullAttribute.MvcOptions = options;
            })
            .AddNewtonsoftJson(json =>
            {
                json.SerializerSettings.MaxDepth = 10;
                json.SerializerSettings.ContractResolver = new ApiContractResolver();
                json.SerializerSettings.NullValueHandling = NullValueHandling.Include;
                json.SerializerSettings.DefaultValueHandling = DefaultValueHandling.Populate;
                // json.SerializerSettings.DateFormatString = "yyyy-MM-dd";
            })
            .ConfigureApplicationPartManager(manager =>
            {
                // Remove the default ControllerFeatureProvider and replace with our custom one
                // that respects API version kill-switch settings.
                var defaultProvider = manager.FeatureProviders
                    .OfType<ControllerFeatureProvider>()
                    .FirstOrDefault();
                if (defaultProvider is not null)
                    manager.FeatureProviders.Remove(defaultProvider);

                var webSettings = ISettingsProvider.Instance.GetSettings().Web;
                manager.FeatureProviders.Add(new ApiVersionControllerFeatureProvider(webSettings));
            })
            .AddPluginControllers(pluginManager)
            .AddControllersAsServices();

        services
            .AddApiVersioning(options =>
            {
                var webSettings = ISettingsProvider.Instance.GetSettings().Web;

                options.ReportApiVersions = true;
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.ApiVersionReader = new ShokoApiReader(webSettings.EnableAPIv2);
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });
        services.AddResponseCaching();

        services.Configure<KestrelServerOptions>(options =>
        {
            options.AllowSynchronousIO = true;
        });
        return services;
    }

    public static IMvcBuilder AddPluginControllers(this IMvcBuilder mvc, IPluginManager pluginManager)
    {
        foreach (var pluginInfo in pluginManager.GetPluginInfos().Where(p => p.IsEnabled && p.CanLoad))
        {
            var assembly = pluginInfo.PluginType!.Assembly;
            if (assembly == typeof(APIExtensions).Assembly)
                continue; //Skip the current assembly, this is implicitly added by ASP.

            mvc.AddApplicationPart(assembly);
        }

        return mvc;
    }

    public static SwaggerGenOptions AddPlugins(this SwaggerGenOptions options, IPluginManager pluginManager)
    {
        foreach (var pluginInfo in pluginManager.GetPluginInfos().Where(p => p.IsEnabled && p.CanLoad))
        {
            var assembly = pluginInfo.PluginType!.Assembly;
            if (assembly == typeof(APIExtensions).Assembly)
                continue; //Skip the current assembly, as these are added earlier.

            var location = assembly.Location;
            var xml = Path.ChangeExtension(location, "xml");
            if (File.Exists(xml))
            {
                options.IncludeXmlComments(xml, true); //Include the XML comments if it exists.
            }
        }

        return options;
    }

    private static string GetTypeName(Type type) =>
        GetTypeName(type.ToString()!.Replace("+", "."));

    private static string GetTypeName(string fullName)
    {
        if (!fullName.Contains('`'))
            return ConvertTypeName(fullName);

        var firstPart = fullName[..fullName.IndexOf('`')];
        var secondPart = fullName[(fullName.IndexOf('`') + 3)..^1];
        return ConvertTypeName(firstPart) + "_" + GetTypeName(secondPart) + "_";
    }

    private static string ConvertTypeName(string fullName)
    {
        string title;
        if (fullName.StartsWith("System.Collections.Generic."))
            title = fullName.Split('.').Skip(3).Join('.');
        else if (fullName.StartsWith("System.") || fullName.StartsWith("Microsoft."))
            title = fullName.Split('.').Skip(1).Join('.');

        // Plugin schemas
        else if (!fullName.StartsWith("Shoko.Server.") && !fullName.StartsWith("Shoko.Abstractions."))
            title = fullName
                .Replace("Shoko.Plugin.", "API.")
                .Replace("API.API.", "API.")
                .Replace(PluginApiVersionRegex(), e => $"APIv{(e.Groups["version"].Success ? e.Groups["version"].Value : "1")}.");

        // APIv0 (API independent plugin abstraction) schemas
        else if (fullName.StartsWith("Shoko.Abstractions."))
            title = fullName
                .Replace("Shoko.Abstractions.", "APIv0.Abstraction.")
                .Replace("TitleLanguage", "LanguageName")
                .Replace("DataModels.", "")
                .Replace("Enums.", "");

        // APIv0 (API independent plex webhook) schemas
        else if (fullName.StartsWith("Shoko.Server.Plex."))
            title = fullName.Replace("Shoko.Server.Plex.", "APIv0.Plex.");

        // APIv0 (API independent settings) schemas
        else if (fullName.StartsWith("Shoko.Server.Settings."))
            title = fullName
                .Replace("Shoko.Server.Settings.", "APIv0.Settings.");

        // APIv0 (API independent) schemas
        else if (fullName is "Shoko.Server.TagFilter.Filter")
            title = "APIv0.Shared.TagFilter";
        else if (fullName.StartsWith("Shoko.Server.Server."))
            title = fullName
                .Replace("Shoko.Server.Server.", "APIv0.Shared.")
                .Replace("Enums.", "");
        else if (fullName.StartsWith("Shoko.Common."))
            title = fullName
                .Replace("Shoko.Common.", "APIv0.Shared.")
                .Replace("Enums.", "");
        else if (fullName.StartsWith("Shoko.Server.Providers."))
            title = fullName
                .Replace("Shoko.Server.Providers.", "APIv0.")
                .Replace("AniDB", "Anidb")
                .Replace("TMDB", "Tmdb")
                .Replace("Anidb.Anidb", "Anidb.")
                .Replace("Tmdb.Tmdb", "Tmdb.");
        else if (fullName.StartsWith("Shoko.Server.API.v0.Controllers."))
            title = fullName
                .Replace("Shoko.Server.API.v0.Controllers.", "APIv0.")
                .Replace("Controller", "");
        else if (fullName.StartsWith("Shoko.Server.API.v0.Models."))
            title = fullName
                .Replace("Shoko.Server.API.v0.Models.", "APIv0.");

        // APIv3 schemas
        else if (fullName.StartsWith("Shoko.Server.API.v3.Controllers."))
            title = fullName
                .Replace("Shoko.Server.API.v3.Controllers.", "APIv3.")
                .Replace("Controller", "");
        else if (fullName.StartsWith("Shoko.Server.API.v3.Models."))
            title = fullName
                .Replace("Shoko.Server.API.v3.Models.", "APIv3.")
                .Replace("Common.", "")
                .Replace("Input.", "")
                .Replace("AniDB", "Anidb")
                .Replace("TMDB", "Tmdb")
                .Replace("Anidb.Anidb", "Anidb.")
                .Replace("Tmdb.Tmdb", "Tmdb.")
                .Replace("Image.Image", "Image");

        // All else exposed in APIv3 (mostly from the settings object), in addition to anything in APIv2.
        else
            title = string.Join(".", fullName.Replace("+", ".").Replace("`1", "").Split(".").TakeLast(2));

        return title;
    }

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersionDescription description, string title)
        => CreateInfoForApiVersion(description.ApiVersion, description.IsDeprecated, title);

    private static OpenApiInfo CreateInfoForApiVersion(ApiVersion apiVersion, bool isDeprecated, string title)
    {
        var info = new OpenApiInfo
        {
            Title = $"{title} API {apiVersion}",
            Version = apiVersion.ToString(),
            Description = $"{title} API."
        };

        if (isDeprecated)
        {
            info.Description += " This API version has been deprecated.";
        }

        return info;
    }

    /// <summary>
    /// Lists the Swagger documents of the enabled plugins: one per API version the plugin has
    /// controllers for, and one for the unversioned endpoints it may map itself when it
    /// registers into the application pipeline and has no controllers of that version.
    /// </summary>
    /// <param name="pluginManager">The plugin manager.</param>
    /// <param name="provider">The API versions of the application.</param>
    /// <returns>The plugin documents, grouped by plugin, newest version first.</returns>
    private static IEnumerable<PluginSwaggerDocument> GetPluginDocuments(IPluginManager pluginManager, IApiVersionDescriptionProvider provider)
    {
        foreach (var pluginInfo in pluginManager.GetPluginInfos().Where(p => p.IsEnabled && p.CanLoad))
        {
            var assembly = pluginInfo.PluginType!.Assembly;
            if (assembly == typeof(APIExtensions).Assembly)
                continue; //Skip the current assembly, as these are added separately.

            var pluginVersions = GetPluginApiVersions(assembly);
            var dllName = Path.GetFileNameWithoutExtension(pluginInfo.DLLs[0]);

            foreach (var description in provider.ApiVersionDescriptions
                         .Where(d => pluginVersions.Contains(d.GroupName))
                         .OrderByDescending(a => a.ApiVersion))
            {
                yield return new(
                    $"{dllName}-{description.GroupName}",
                    $"{pluginInfo.Name} {description.GroupName.ToUpperInvariant()}",
                    CreateInfoForApiVersion(description, pluginInfo.Name),
                    HasControllers: true
                );
            }

            // Mapped endpoints are known only once the pipeline is built, so the document is
            // declared for every plugin that could map them.
            if (pluginInfo.ApplicationRegistrationType is not null && !pluginVersions.Contains(PluginDocumentInclusionPredicate.DefaultVersionGroup))
            {
                var groupName = PluginDocumentInclusionPredicate.DefaultVersionGroup;
                yield return new(
                    $"{dllName}-{groupName}",
                    $"{pluginInfo.Name} {groupName.ToUpperInvariant()}",
                    CreateInfoForApiVersion(new ApiVersion(1, 0), isDeprecated: false, pluginInfo.Name),
                    HasControllers: false
                );
            }
        }
    }

    /// <summary>
    /// Lists the documents shown in the Swagger UI, each time the UI is loaded.
    /// </summary>
    /// <param name="serverUrls">The server's own documents, listed first.</param>
    /// <param name="pluginDocuments">The plugin documents.</param>
    /// <param name="predicate">Assigns the endpoints to the documents.</param>
    /// <param name="apiExplorer">The described endpoints of the application.</param>
    /// <param name="swaggerPrefix">The path the Swagger documents are served under.</param>
    /// <returns>The server documents, then every plugin document with controllers or any mapped endpoint.</returns>
    private static IEnumerable<UrlDescriptor> ListSwaggerDocuments(
        IReadOnlyList<UrlDescriptor> serverUrls,
        IReadOnlyList<PluginSwaggerDocument> pluginDocuments,
        PluginDocumentInclusionPredicate predicate,
        IApiDescriptionGroupCollectionProvider apiExplorer,
        string swaggerPrefix
    )
    {
        foreach (var url in serverUrls)
            yield return url;

        foreach (var document in pluginDocuments)
        {
            if (!document.HasControllers && !apiExplorer.ApiDescriptionGroups.Items.SelectMany(group => group.Items).Any(api => predicate.Include(document.Name, api)))
                continue;

            yield return new() { Url = $"/{swaggerPrefix}/{document.Name}/swagger.json", Name = document.DisplayName };
        }
    }

    /// <summary>
    /// A plugin's Swagger document.
    /// </summary>
    /// <param name="Name">The document name, <c>{DllName}-{version group}</c>.</param>
    /// <param name="DisplayName">The name shown in the Swagger UI.</param>
    /// <param name="Info">The document's info block.</param>
    /// <param name="HasControllers">Whether the plugin has controllers for the document's version.</param>
    private sealed record PluginSwaggerDocument(string Name, string DisplayName, OpenApiInfo Info, bool HasControllers);

    public static IApplicationBuilder UseAPI(this IApplicationBuilder app, IPluginManager pluginManager)
    {
        var settings = ISettingsProvider.Instance.GetSettings();
        var webSettings = settings.Web;
        if (!settings.SentryOptOut)
        {
            app.Use(async (context, next) =>
            {
                try
                {
                    await next.Invoke(context);
                }
                catch (Exception e)
                {
                    try
                    {
                        SentrySdk.CaptureException(e);
                    }
                    catch
                    {
                        // ignore
                    }
                    throw;
                }
            });
        }

#if DEBUG
        app.UseDeveloperExceptionPage();
#else
        if (webSettings.AlwaysUseDeveloperExceptions)
            app.UseDeveloperExceptionPage();
#endif

        // Create web ui directory and add the boot-strapper.
        var webUIDir = new DirectoryInfo(ApplicationPaths.Instance.WebPath);
        var backupDir = new DirectoryInfo(Path.Combine(ApplicationPaths.Instance.ApplicationPath, "webui"));
        var webUIUpdateService = app.ApplicationServices.GetRequiredService<ISystemUpdateService>();
        if (!webUIDir.Exists)
        {
            if (backupDir.Exists)
                CopyFilesRecursively(backupDir, webUIDir);
            else
                webUIDir.Create();
        }
        else if (
            backupDir.Exists &&
            webSettings.AutoReplaceWebUIWithIncluded &&
            webUIUpdateService.LoadIncludedWebComponentVersionInformation() is { } includedVersion &&
            (
                webUIUpdateService.LoadWebComponentVersionInformation() is not { } currentVersion ||
                (
                    new SemverVersionComparer().Compare(includedVersion.Version, currentVersion.Version) > 0 &&
                    (
                        (includedVersion.Channel is not ReleaseChannel.Debug && currentVersion.Channel is not ReleaseChannel.Debug) ||
                        (includedVersion.Channel is ReleaseChannel.Debug && currentVersion.Channel is ReleaseChannel.Debug)
                    )
                )
            )
        )
        {
            CopyFilesRecursively(backupDir, webUIDir);
        }

        if (webSettings.EnableSwaggerUI)
        {
            app.UseSwagger(c =>
            {
                if (webSettings.SwaggerUIPrefix is not "swagger")
                    c.RouteTemplate = c.RouteTemplate.Replace("/swagger/", $"/{webSettings.SwaggerUIPrefix}/");
                c.PreSerializeFilters.Add((swaggerDoc, _) =>
                {
                    var commonPrefix = LongestCommonPathPrefix(swaggerDoc.Paths.Keys);
                    swaggerDoc.Servers ??= [];
                    if (commonPrefix.Length > 0 && commonPrefix != "/")
                        swaggerDoc.Servers.Add(new() { Url = commonPrefix });
                    else
                        swaggerDoc.Servers.Add(new() { Url = "/" });
                    var paths = new OpenApiPaths();
                    foreach (var path in swaggerDoc.Paths)
                    {
                        if (commonPrefix.Length > 0 && path.Key.StartsWith(commonPrefix))
                        {
                            var stripped = "/" + path.Key[commonPrefix.Length..].TrimStart('/');
                            paths.Add(stripped, path.Value);
                        }
                        else
                        {
                            if (path.Value is OpenApiPathItem pathValue)
                            {
                                pathValue.Servers ??= [];
                                pathValue.Servers.Clear();
                                pathValue.Servers.Add(new() { Url = "/" });
                            }

                            paths.Add(path.Key, path.Value);
                        }
                    }
                    swaggerDoc.Paths = paths;
                });
            });
            app.UseSwaggerUI(
                options =>
                {
                    options.RoutePrefix = webSettings.SwaggerUIPrefix;
                    var provider = app.ApplicationServices.GetRequiredService<IApiVersionDescriptionProvider>();

                    // Server API bundles (listed first)
                    foreach (var description in provider.ApiVersionDescriptions.OrderByDescending(a => a.ApiVersion))
                    {
                        if (description.GroupName is not ("v2" or "v2.1" or "v3"))
                            continue;
                        if (description.GroupName is "v2" or "v2.1" && !webSettings.EnableAPIv2)
                            continue;
                        if (description.GroupName is "v3" && !webSettings.EnableAPIv3)
                            continue;

                        options.SwaggerEndpoint(
                            $"/{webSettings.SwaggerUIPrefix}/{description.GroupName}/swagger.json",
                            $"Server {description.GroupName.ToUpperInvariant()}"
                        );
                    }

                    // Plugins map their endpoints after this runs, so the list is re-read on every UI load;
                    // a document only for mapped endpoints is left out while it has none.
                    var serverUrls = options.ConfigObject.Urls?.ToList() ?? [];
                    var pluginDocuments = GetPluginDocuments(pluginManager, provider).ToList();
                    var predicate = new PluginDocumentInclusionPredicate(pluginManager);
                    var apiExplorer = app.ApplicationServices.GetRequiredService<IApiDescriptionGroupCollectionProvider>();
                    options.ConfigObject.Urls = ListSwaggerDocuments(serverUrls, pluginDocuments, predicate, apiExplorer, webSettings.SwaggerUIPrefix);
                    options.EnablePersistAuthorization();
                });
        }

        if (webSettings.EnableWebUI)
        {
            var httpContextAccessor = app.ApplicationServices.GetRequiredService<IHttpContextAccessor>();
            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new WebUiFileProvider(webUIUpdateService, httpContextAccessor, webSettings.WebUIPublicPath, webUIDir.FullName),
                RequestPath = webSettings.WebUIPublicPath,
                ServeUnknownFileTypes = true,
                DefaultContentType = "text/html",
                OnPrepareResponse = ctx =>
                {
                    // We set the cache headers only for index.html file because it doesn't have a different hash when changed.
                    // The served index is an in-memory copy without a physical path, so match it by name.
                    if (string.Equals(ctx.File.Name, "index.html", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
                        ctx.Context.Response.Headers.Append("Expires", "0");
                    }
                }
            });
        }

        app.UseRouting();

        app.UseMiddleware<ServerNotRunningMiddleware>();

        // Important for first run at least
        app.UseAuthentication();
        // Everything the request does runs for its token, until the request ends.
        app.UseMiddleware<ActorContextMiddleware>();
        app.UseAuthorization();

        app.UseEndpoints(conf =>
        {
            conf.MapControllers();
            if (webSettings.EnableSignalR)
            {
                conf.MapHub<LoggingHub>("/signalr/logging").RequireAuthorization("admin");
                conf.MapHub<AggregateHub>("/signalr/aggregate").RequireAuthorization();
            }
        });

        var registeredPlugins = false;
        foreach (var pluginInfo in pluginManager.GetPluginInfos().Where(p => p.IsActive && p.ApplicationRegistrationType is not null))
        {
            pluginInfo.ApplicationRegistrationType!
                .GetMethod(nameof(IPluginApplicationRegistration.RegisterServices), BindingFlags.Public | BindingFlags.Static, [typeof(IApplicationBuilder), typeof(IApplicationPaths)])!
                .Invoke(null, [app, ApplicationPaths.Instance]);
            registeredPlugins = true;
        }

        // Plugin endpoints join an existing data source that never signals a change, and the API
        // explorer cached its endpoints earlier; a new empty source makes it re-read them all.
        if (registeredPlugins)
            app.UseEndpoints(endpoints => endpoints.DataSources.Add(new DefaultEndpointDataSource()));

        app.UseCors(options => options.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

        app.UseMvc();

        return app;
    }

    private static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
    {
        if (target.Exists)
            target.Delete(recursive: true);

        target.Create();
        foreach (var dir in source.GetDirectories())
        {
            CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
        }

        foreach (var file in source.GetFiles())
        {
            file.CopyTo(Path.Combine(target.FullName, file.Name));
        }
    }

    private static string LongestCommonPathPrefix(IEnumerable<string> paths)
    {
        var arr = paths.ToArray();
        if (arr.Length == 0)
            return string.Empty;

        // Build a map of every segment-bounded prefix → how many paths share it.
        var prefixCounts = new Dictionary<string, int>();
        foreach (var path in arr)
        {
            var pos = 1; // skip leading '/'
            while (pos < path.Length)
            {
                var slash = path.IndexOf('/', pos);
                if (slash < 0)
                    break;
                var prefix = path[..(slash + 1)];
                prefixCounts.TryGetValue(prefix, out var count);
                prefixCounts[prefix] = count + 1;
                pos = slash + 1;
            }
        }

        // Pick the longest prefix that covers ≥90% of paths.
        var threshold = (int)(arr.Length * 0.9);
        var best = string.Empty;
        foreach (var (prefix, count) in prefixCounts)
        {
            if (count >= threshold && prefix.Length > best.Length)
                best = prefix;
        }

        // If no majority prefix, fall back to strict LCP of all paths.
        if (best.Length == 0)
        {
            best = arr[0];
            for (var i = 1; i < arr.Length; i++)
            {
                while (!arr[i].StartsWith(best, StringComparison.Ordinal))
                    best = best[..^1];
                if (best.Length == 0)
                    return string.Empty;
            }

            var lastSlash = best.LastIndexOf('/');
            return lastSlash > 0 ? best[..(lastSlash + 1)] : string.Empty;
        }

        return best;
    }

    private static HashSet<string> GetPluginApiVersions(Assembly assembly)
    {
        var versions = new HashSet<string>();
        var controllerTypes = assembly.GetExportedTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in controllerTypes)
        {
            var attr = type.GetCustomAttribute<ApiVersionAttribute>();
            if (attr is not null)
            {
                foreach (var v in attr.Versions)
                {
                    versions.Add($"v{v}");
                }
            }
            else
            {
                // No attribute → defaults to v1
                versions.Add("v1");
            }
        }

        return versions;
    }

    [GeneratedRegex(@"(?:[^ ]+\.)?v(?<version>\d+(?:\.\d+)?)?\.(?:Models?\.|DTOs?\.)|(?:[^ ]+\.)?API\.(?:Controllers?\.)?(?:v(?<version>\d+(?:\.\d+)?)\.(?:Models?\.|DTOs?\.)?)?", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ECMAScript)]
    private static partial Regex PluginApiVersionRegex();
}
