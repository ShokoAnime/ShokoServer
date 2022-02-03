
namespace Shoko.Plugin.Abstractions
{
    /// <summary>
    /// This can specify the static method
    /// <code>static void ConfigureServices(IServiceCollection serviceCollection)</code>
    /// This will allow you to inject other services to the Shoko DI container which can be accessed via <see cref="ShokoServer.ServiceContainer">ShokoServer.ServiceContainer</see>
    /// if you want a Logger, you can use <see cref="Microsoft.Extensions.Logging.ILogger&lt;T&gt;"/>
    /// </summary>
    public interface IPlugin
    {
        string Name { get; }
        void Load();

        // static void ConfigureServices(IServiceCollection serviceCollection);
    }
} 