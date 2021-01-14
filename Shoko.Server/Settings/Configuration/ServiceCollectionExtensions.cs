using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Shoko.Server.Settings.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static void ConfigureWritable<T>(
            this IServiceCollection services,
            IConfigurationSection section) where T : class, new()
        {
            services.Configure<T>(section);
            services.AddTransient<IWritableOptions<T>>(provider =>
            {
                var configuration = (IConfigurationRoot)provider.GetRequiredService<IConfiguration>();
                var options = provider.GetRequiredService<IOptionsMonitor<T>>();
                return new WritableOptions<T>(options, configuration, section.Key);
            });
        }

        public static void ConfigureWritable<T>(
            this IServiceCollection services,
            IConfigurationRoot section) where T : class, new()
        {
            services.Configure<T>(section);
            services.AddTransient<IWritableOptions<T>>(provider =>
            {
                var options = provider.GetRequiredService<IOptionsMonitor<T>>();
                return new WritableOptions<T>(options, section, null);
            });
        }
    }
}