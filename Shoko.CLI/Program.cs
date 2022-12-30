#region
using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Web;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

#endregion
namespace Shoko.CLI;

public static class Program
{
    public static void Main()
    {
        try
        {
            UnhandledExceptionManager.AddHandler();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
        }
        Utils.SetInstance();
        Utils.InitLogger();
        // startup DI builds ShokoServer and StartServer, then those build the runtime DI. The startup DI allows logging and other DI handling during startup
        new HostBuilder().UseContentRoot(Directory.GetCurrentDirectory())
            .ConfigureHost()
            .ConfigureApp()
            .ConfigureServiceProvider()
            .UseNLog()
            .ConfigureServices(ConfigureServices)
            .Build()
            .Run();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddHostedService<Worker>();
        services.AddSingleton<ISettingsProvider, SettingsProvider>();
        services.AddSingleton<ShokoServer>();
        services.AddSingleton<StartServer>();
    }
}
