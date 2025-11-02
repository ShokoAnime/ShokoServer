using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Providers.AniDB.HTTP;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.AniDB.UDP;
using Shoko.Server.Utilities;

namespace Shoko.Server.Providers.AniDB;

public static class AniDBStartup
{
    public static IServiceCollection AddAniDB(this IServiceCollection services)
    {
        services.AddSingleton<HttpAnimeParser>();
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

        services.AddHttpClient("AniDB", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("gzip"));
                client.DefaultRequestHeaders.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("deflate"));
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 6.1; WOW64; rv:40.0) Gecko/20100101 Firefox/40.1");
                client.BaseAddress = new Uri(Utils.SettingsProvider.GetSettings().AniDb.HTTPServerUrl);
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                SslOptions = new SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = delegate { return true; }
                }
            });

        return services;
    }
}
