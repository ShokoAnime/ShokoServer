using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Data.Context;
using Shoko.Server.Repositories.EntityFramework.TMDB;

namespace Shoko.Server.Data;

public static class Startup
{
    public static IServiceCollection AddEntityFramework(this IServiceCollection services)
    {
        services.AddDbContext<DataContext>();
        return services;
    }
}
