#region
using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Web;
#endregion
namespace Shoko.CLI;

public static class Program
{
    public static void Main()
        => new HostBuilder().UseContentRoot(Directory.GetCurrentDirectory())
                            .ConfigureHostConfiguration(ConfigureHost)
                            .ConfigureAppConfiguration(ConfigureApp)
                            .UseDefaultServiceProvider(ConfigureDefaultServiceProvider)
                            .UseNLog()
                            .ConfigureServices(ConfigureServices)
                            .Build()
                            .Run();

    private static void ConfigureHost(IConfigurationBuilder configurationBuilder)
        => configurationBuilder.AddEnvironmentVariables(prefix: "DOTNET_")
                               .AddCommandLineIfNotNull();

    private static void ConfigureApp(HostBuilderContext hostingContext, IConfigurationBuilder configurationBuilder)
    {
        var shouldReloadOnChange = hostingContext.Configuration.GetValue("hostBuilder:reloadConfigOnChange", defaultValue: true);
        configurationBuilder.AddJsonFile("appsettings.json",
                                         optional: true,
                                         reloadOnChange: shouldReloadOnChange)
                            .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json",
                                         optional: true,
                                         reloadOnChange: shouldReloadOnChange)
                            .AddDevelopmentAssembly(hostingContext.HostingEnvironment)
                            .AddEnvironmentVariables()
                            .AddCommandLineIfNotNull();
    }

    private static void ConfigureDefaultServiceProvider(HostBuilderContext context, ServiceProviderOptions options)
    {
        var isDevelopment = context.HostingEnvironment.IsDevelopment();
        options.ValidateScopes  = isDevelopment;
        options.ValidateOnBuild = isDevelopment;
    }

    private static void ConfigureServices(IServiceCollection services) => services.AddHostedService<Worker>();

#region IConfigurationBuilderExtensions
    private static IConfigurationBuilder AddCommandLineIfNotNull(this IConfigurationBuilder configurationBuilder)
    {
        var args = Environment.GetCommandLineArgs();
        if (args is not null)
            configurationBuilder.AddCommandLine(args);
        return configurationBuilder;
    }

    private static IConfigurationBuilder AddDevelopmentAssembly(this IConfigurationBuilder configurationBuilder, IHostEnvironment env)
    {
        if (env.IsDevelopment() is false || string.IsNullOrWhiteSpace(env.ApplicationName))
            return configurationBuilder;

        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
        configurationBuilder.AddUserSecrets(appAssembly, optional: true);
        return configurationBuilder;
    }
#endregion
}