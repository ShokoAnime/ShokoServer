using Shoko.Server.Settings;

namespace Shoko.Tests.Infrastructure;

/// <summary>
/// A minimal <see cref="ISettingsProvider"/> over a plain <see cref="ServerSettings"/>.
/// </summary>
/// <remarks>
/// Some server code reads the settings singleton from a field initializer — the MySQL backend
/// builds part of its DDL that way — so it has to be installed before those types are constructed.
/// The static is freely assignable, unlike <c>ISystemService.StaticServices</c>, but it is still
/// process-global, so <see cref="Install"/> leaves an existing provider alone.
/// </remarks>
public sealed class StubSettingsProvider(ServerSettings settings) : ISettingsProvider
{
    public ServerSettings Settings { get; } = settings;

    /// <summary>
    /// Installs a stub provider unless something has already set one.
    /// </summary>
    public static void Install()
    {
        try
        {
            _ = ISettingsProvider.Instance;
        }
        catch
        {
            ISettingsProvider.Instance = new StubSettingsProvider(new ServerSettings());
        }
    }

    public IServerSettings GetSettings(bool copy = false) => Settings;

    public void SaveSettings(IServerSettings settings) { }

    public void SaveSettings() { }

    public void DebugSettingsToLog() { }
}
