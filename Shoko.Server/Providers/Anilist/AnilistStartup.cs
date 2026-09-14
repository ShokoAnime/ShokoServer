using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Core.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata.Anilist.Services;

namespace Shoko.Server.Providers.Anilist;

/// <summary>
/// Extension methods for registering Anilist services.
/// </summary>
public static class AnilistStartup
{
    /// <summary>
    /// The base URL for the Anilist GraphQL API.
    /// </summary>
    public const string AnilistGraphQLUrl = "https://graphql.anilist.co";

    /// <summary>
    /// Registers all Anilist services and HTTP client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAnilist(this IServiceCollection services)
    {
        // Register HTTP client for Anilist GraphQL API
        services.AddHttpClient("Anilist", (serviceProvider, client) =>
        {
            // Same identity as the "Default" client, so AniList sees one ShokoServer version string everywhere.
            var systemService = serviceProvider.GetRequiredService<ISystemService>();
            client.BaseAddress = new Uri(AnilistGraphQLUrl);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", $"ShokoServer/{systemService.Version.Version.ToSemanticVersioningString()}");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("deflate");
            client.DefaultRequestHeaders.AcceptEncoding.ParseAdd("br");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register rate limiter, API client and image registration
        services.AddSingleton<AnilistRateLimiter>();
        services.AddSingleton<AnilistApiClient>();
        services.AddSingleton<AnilistImageService>();

        // Register services with their interfaces
        services.AddSingleton<AnilistLinkingService>();
        services.AddSingleton<IAnilistLinkingService>(sp => sp.GetRequiredService<AnilistLinkingService>());

        services.AddSingleton<AnilistMetadataService>();
        services.AddSingleton<IAnilistMetadataService>(sp => sp.GetRequiredService<AnilistMetadataService>());

        services.AddSingleton<AnilistSearchService>();
        services.AddSingleton<IAnilistSearchService>(sp => sp.GetRequiredService<AnilistSearchService>());

        return services;
    }
}
