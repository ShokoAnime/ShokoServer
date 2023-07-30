using System;
using System.Reflection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Shoko.Server.Databases;
using Shoko.Server.Utilities;

namespace Shoko.Server.Server;

public static class StartupExtensions
{
    public static IConfigurationBuilder AddCommandLineIfNotNull(this IConfigurationBuilder configurationBuilder)
    {
        var tmpArgs = Environment.GetCommandLineArgs();
        var args = tmpArgs.Length > 1 ? new string[tmpArgs.Length - 1] : null;
        if (args is null)
            return configurationBuilder;
        
        Array.Copy(tmpArgs, 1, args, 0, args.Length);
        configurationBuilder.AddCommandLine(args);
        return configurationBuilder;
    }

    public static IConfigurationBuilder AddDevelopmentAssembly(this IConfigurationBuilder configurationBuilder, IHostEnvironment env)
    {
        if (env.IsDevelopment() is false || string.IsNullOrWhiteSpace(env.ApplicationName))
            return configurationBuilder;

        var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
        configurationBuilder.AddUserSecrets(appAssembly, optional: true);
        return configurationBuilder;
    }

    public static IWebHostBuilder ConfigureApp(this IWebHostBuilder builder) => builder.ConfigureAppConfiguration(ConfigureApp);
    public static IWebHostBuilder ConfigureServiceProvider(this IWebHostBuilder builder) => builder.UseDefaultServiceProvider(ConfigureDefaultServiceProvider);

    private static void ConfigureApp(WebHostBuilderContext hostingContext, IConfigurationBuilder configurationBuilder)
    {
        var shouldReloadOnChange = hostingContext.Configuration.GetValue("hostBuilder:reloadConfigOnChange", defaultValue: true);
        configurationBuilder.AddJsonFile("appsettings.json",
                optional: true,
                reloadOnChange: shouldReloadOnChange)
            .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json",
                optional: true,
                reloadOnChange: shouldReloadOnChange)
            .AddDevelopmentAssembly(hostingContext.HostingEnvironment)
            .AddEnvironmentVariables(prefix: "DOTNET_")
            .AddEnvironmentVariables()
            .AddCommandLineIfNotNull();
    }

    private static void ConfigureDefaultServiceProvider(WebHostBuilderContext context, ServiceProviderOptions options)
    {
        var isDevelopment = context.HostingEnvironment.IsDevelopment();
        options.ValidateScopes  = isDevelopment;
        options.ValidateOnBuild = isDevelopment;
    }
    
    internal static void UseDatabase(this IServiceCollectionQuartzConfigurator q)
    {
        q.UsePersistentStore(options =>
        {
            var settings = Utils.SettingsProvider.GetSettings();
            // TODO Make these their own settings. SQLite should support putting Quartz in its own db. Will help with locking
            // TODO, take the scripts from here and execute the SQL Scripts if the Quartz tables don't exist
            // https://github.com/quartznet/quartznet/tree/main/database/tables
            if (settings.Database.Type.Trim().Equals(Constants.DatabaseType.SqlServer, StringComparison.InvariantCultureIgnoreCase))
                options.UseSqlServer(DatabaseFactory.Instance.GetConnectionString());
            else if (settings.Database.Type.Trim().Equals(Constants.DatabaseType.MySQL, StringComparison.InvariantCultureIgnoreCase))
                options.UseMySqlConnector(c => c.ConnectionString = DatabaseFactory.Instance.GetConnectionString());
            else if (settings.Database.Type.Trim().Equals(Constants.DatabaseType.Sqlite, StringComparison.InvariantCultureIgnoreCase))
                options.UseMicrosoftSQLite(DatabaseFactory.Instance.GetConnectionString());
        });
    }
}
