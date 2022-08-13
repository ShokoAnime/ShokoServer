using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.AniDB_API;
using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP;

namespace Shoko.Server.Providers.AniDB
{
    public static class AniDBStartup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<HttpAnimeParser>();
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
                .Where(p => requestType.IsAssignableFrom(p) && !requestType.IsAbstract && requestType.IsClass);

            foreach (var type in types)
            {
                services.AddTransient(type);
            }
        }
    }
}
