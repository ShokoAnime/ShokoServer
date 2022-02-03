using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NLog.Web;

namespace Shoko.CLI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            // here we would do things that need to happen after init, but before shutdown, while blocking the main thread.
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            string instance = null;
            for (int x = 0; x < args.Length; x++)
            {
                if (!args[x].Equals("instance", StringComparison.InvariantCultureIgnoreCase)) continue;
                if (x + 1 >= args.Length) continue;
                instance = args[x + 1];
                break;
            }

            var arguments = new ProgramArguments {Instance = instance};
            return CreateBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddSingleton(arguments);
                    services.AddHostedService<Worker>();
                });
        }

        private static IHostBuilder CreateBuilder(string[] args)
        {
            var builder = new HostBuilder();

            builder.UseContentRoot(Directory.GetCurrentDirectory());
            builder.ConfigureHostConfiguration(config =>
            {
                config.AddEnvironmentVariables(prefix: "DOTNET_");
                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            });

            builder.ConfigureAppConfiguration((hostingContext, config) =>
            {
                var env = hostingContext.HostingEnvironment;
                
                var reloadOnChange = hostingContext.Configuration.GetValue("hostBuilder:reloadConfigOnChange", defaultValue: true);

                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: reloadOnChange)
                      .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: reloadOnChange);

                if (env.IsDevelopment() && !string.IsNullOrEmpty(env.ApplicationName))
                {
                    var appAssembly = Assembly.Load(new AssemblyName(env.ApplicationName));
                    if (appAssembly != null)
                    {
                        config.AddUserSecrets(appAssembly, optional: true);
                    }
                }

                config.AddEnvironmentVariables();

                if (args != null)
                {
                    config.AddCommandLine(args);
                }
            })
            .UseDefaultServiceProvider((context, options) =>
            {
                var isDevelopment = context.HostingEnvironment.IsDevelopment();
                options.ValidateScopes = isDevelopment;
                options.ValidateOnBuild = isDevelopment;
            }).UseNLog();

            return builder;
        }
    }
}