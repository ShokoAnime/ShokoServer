using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Server;

namespace Shoko.Server.Settings.DI
{
    public class Configuration<T> : IConfiguration<T>
    {
        public T Instance { get; private set; }

        public Configuration(T instance) => Instance = instance;

        public void Dispose()
        {
            ShokoServer.ServiceContainer.GetRequiredService<ServerSettings>()
                .SaveSettings();
        }
    }
}
