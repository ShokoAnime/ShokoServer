using System;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Plugin;

namespace Shoko.Plugin.LegacyPlex;

/// <inheritdoc />
public partial class Plugin : IPluginServiceRegistration
{
    /// <inheritdoc />
    public static void RegisterServices(IServiceCollection serviceCollection, IApplicationPaths applicationPaths)
    {
        serviceCollection.AddHttpClient(PlexHelper.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        serviceCollection.AddSingleton<PlexHelperProvider>();
    }
}
