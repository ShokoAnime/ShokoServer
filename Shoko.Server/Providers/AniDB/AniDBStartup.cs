using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP;

namespace Shoko.Server.Providers.AniDB;

public static class AniDBStartup
{
    public static IServiceCollection AddAniDB(this IServiceCollection services)
    {
        services.AddSingleton<HttpAnimeParser>();
        services.AddSingleton<ImageHttpClientFactory>();
        services.AddSingleton<AniDBTitleHelper>();
        services.AddSingleton<AnimeCreator>();
        services.AddSingleton<HttpXmlUtils>();
        services.AddSingleton<UDPRateLimiter>();
        services.AddSingleton<HttpRateLimiter>();
        services.AddSingleton<IHttpConnectionHandler, AniDBHttpConnectionHandler>();
        services.AddSingleton<IUDPConnectionHandler, AniDBUDPConnectionHandler>();
        services.AddSingleton<IRequestFactory, RequestFactory>();

        // Register Requests
        var requestType = typeof(IRequest);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => requestType.IsAssignableFrom(p) && !p.IsAbstract && p.IsClass);

        /* Possibly negate the need for IRequest (non-generic)
        var requestType = typeof(IRequest<>);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => p.IsGenericType && requestType.IsAssignableFrom(p.GetGenericTypeDefinition()) && !p.IsAbstract && p.IsClass)
         */

        foreach (var type in types)
        {
            services.AddTransient(type);
        }

        return services;
    }
}
