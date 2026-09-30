using System;
using Shoko.Abstractions.Core.Services;

namespace Shoko.Server.Plugin.Databases;

/// <summary>
/// Keeps the plugin databases closed until the late start has migrated them,
/// right after the core's database, so no plugin reads or writes one early.
/// </summary>
/// <remarks>
/// A context asked for before then fails at once rather than waiting: the
/// start-up itself runs plugin code before the migrations (setup, ready, the
/// hosted services' start), and a wait there would never end.
/// </remarks>
/// <param name="systemService">The system service, to tell a failed start-up apart; optional.</param>
internal sealed class PluginDatabaseGate(ISystemService? systemService = null)
{
    private volatile bool _isOpen;

    /// <summary>
    /// Whether the plugin databases have been migrated and can be used.
    /// </summary>
    public bool IsOpen => _isOpen;

    /// <summary>
    /// Opens every plugin database, once all of them are up to date.
    /// </summary>
    public void Open()
        => _isOpen = true;

    /// <summary>
    /// Throws unless the plugin databases can be used.
    /// </summary>
    /// <param name="pluginName">The plugin asking, for the message.</param>
    /// <param name="databaseName">The database's name within the plugin, for the message.</param>
    /// <exception cref="InvalidOperationException">
    /// The databases have not been migrated yet, or the start-up failed before
    /// they were.
    /// </exception>
    public void ThrowIfClosed(string pluginName, string databaseName)
    {
        if (_isOpen)
            return;

        if (systemService?.StartupFailedException is { } failure)
            throw new InvalidOperationException($"Plugin \"{pluginName}\" asked for its database \"{databaseName}\", which cannot be used: the server failed to start before migrating it.", failure);

        throw new InvalidOperationException(
            $"Plugin \"{pluginName}\" asked for its database \"{databaseName}\" before the server migrated it. The plugin databases are migrated late in the start-up, "
            + "right after the server's own database, so use them once the server has started, never from a plugin's Setup or Ready, a constructor, or a hosted service's StartAsync."
        );
    }
}
