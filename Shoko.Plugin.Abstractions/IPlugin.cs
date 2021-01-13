
using System.ComponentModel.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Plugin.Abstractions.Configuration;

namespace Shoko.Plugin.Abstractions
{
    /// <summary>
    /// This can specify the static method
    /// <code>static void ConfigureServices(IServiceCollection serviceCollection)</code>
    /// This will allow you to inject other services to the Shoko DI container which can be accessed via <see cref="ServiceContainer">ShokoServer.ServiceContainer</see>
    /// if you want a Logger, you can use <see cref="Microsoft.Extensions.Logging.ILogger&lt;T&gt;"/>
    /// </summary>
    public interface IPlugin
    {
        string Name { get; }
        void Load();

        /// <summary>
        /// This will be called with the loaded configuration section for your settings.<br/>
        /// 
        /// If you want read-write access to the configuration, you need to use the typed <see cref="IWritableOptions{T}"/><br/>
        /// 
        /// If these will need to be registered in a magic, optional method of <code>static void ConfigureServices(IServiceCollection serviceCollection)</code>
        /// </summary>
        /// <param name="settings"></param>
        void LoadSettings(IConfigurationSection settings);

        // static void ConfigureServices(IServiceCollection serviceCollection);
    }
} 