using System.Threading;
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

    private static readonly Lock _installLock = new();

    /// <summary>
    /// Installs a stub provider unless something has already set one.
    /// </summary>
    /// <remarks>
    /// Locked because the check and the assignment are two steps against one process-global static.
    /// Two classes installing at once would otherwise each see it unset and each install their own,
    /// and the loser's settings — which a caller has already started configuring — would be
    /// discarded out from under it.
    /// </remarks>
    public static void Install()
    {
        lock (_installLock)
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
    }

    public IServerSettings GetSettings(bool copy = false) => Settings;

    public void SaveSettings(IServerSettings settings) { }

    public void SaveSettings() { }

    public void DebugSettingsToLog() { }
}
