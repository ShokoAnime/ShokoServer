using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Utilities;

namespace Shoko.Server.Settings.DI;

public class Configuration<T> : IConfiguration<T>
{
    public T Instance { get; private set; }

    public Configuration(T instance)
    {
        Instance = instance;
    }

    public void Dispose()
    {
        Utils.ServiceContainer.GetRequiredService<ISettingsProvider>()
            .SaveSettings();
    }
}
