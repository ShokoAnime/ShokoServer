using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shoko.Server.Settings.DI
{
    static class DiExtensions
    {
        public static IServiceCollection AddSettings<T>(this IServiceCollection services, T setting)
        {
            return services.AddScoped<IConfiguration<T>>(_ => new Configuration<T>(setting));
        }
    }
}
