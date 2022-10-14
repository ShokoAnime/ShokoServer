using Microsoft.Extensions.DependencyInjection;

namespace Shoko.Server.Settings.DI;

internal static class DiExtensions
{
    public static IServiceCollection AddSettings<T>(this IServiceCollection services, T setting)
    {
        return services.AddScoped<IConfiguration<T>>(_ => new Configuration<T>(setting));
    }
}
