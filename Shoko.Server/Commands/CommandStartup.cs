using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Shoko.Server.Databases;
using Shoko.Server.Databases.NHIbernate;
using Shoko.Server.Models;

namespace Shoko.Server.Commands;

public static class CommandStartup
{
    public static IServiceCollection AddCommands(this IServiceCollection services)
    {
        // Register Requests
        var requestType = typeof(CommandRequest);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(s => s.GetTypes())
            .Where(p => requestType.IsAssignableFrom(p) && !p.IsAbstract && p.IsClass);

        foreach (var type in types)
        {
            services.AddTransient(type);
        }

        RegisterInitCallbacks();

        return services;
    }

    private static void RegisterInitCallbacks()
    {
        var loggerFactory = new LoggerFactory().AddNLog();
        var logger = loggerFactory.CreateLogger("CommandRequestPostInitializationCallback");
        NHibernateDependencyInjector.RegisterPostInitializationCallback<CommandRequest>((x, values) => Init(logger, x, values));
    }

    private static bool Init(ILogger logger, CommandRequest request, (string name, object value)[] values)
    {
        try
        {
            request.LoadFromCommandDetails(values.FirstOrDefault(a => a.name == "CommandDetails").value as string);
            request.PostInit();
            return true;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to Initialize Command: {CommandType}-{CommandID}, Details: {CommandDetails}", request.CommandType, request.CommandID,
                request.CommandDetails);
            return false;
        }
    }
}
