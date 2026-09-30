using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config.Events;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Core;
using Shoko.Abstractions.Core.Events;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

/// <summary>
///   Collects every reason the server needs a restart: one while any
///   configuration has changed restart-only members, one while any plugin's
///   state changes on restart, and the reasons plugins raise themselves.
///   Backs the restart members of <see cref="Abstractions.Core.Services.ISystemService"/>.
/// </summary>
public sealed class RestartReasonTracker
{
    #region Constants

    /// <summary>
    ///   The key of the <see cref="RestartReasonSource.Configuration"/> reason.
    /// </summary>
    public const string ConfigurationKey = "configuration";

    /// <summary>
    ///   The description of the <see cref="RestartReasonSource.Configuration"/> reason.
    /// </summary>
    public const string ConfigurationDescription = "One or more configuration changes require a restart to take effect.";

    /// <summary>
    ///   The key of the <see cref="RestartReasonSource.PluginState"/> reason.
    /// </summary>
    public const string PluginStateKey = "plugins";

    /// <summary>
    ///   The description of the <see cref="RestartReasonSource.PluginState"/> reason.
    /// </summary>
    public const string PluginStateDescription = "One or more plugin changes require a restart to take effect.";

    /// <summary>
    ///   The path of the server setting holding the enabled plugins. Changes
    ///   to it are covered by the plugin state reason instead.
    /// </summary>
    public const string EnabledPluginsMember = $"{nameof(ServerSettings.Plugins)}.{nameof(PluginSettings.EnabledPlugins)}";

    #endregion

    #region Fields

    private readonly ILogger _logger;

    private readonly IConfigurationService _configurationService;

    private readonly IPluginManager _pluginManager;

    private readonly TimeProvider _timeProvider;

    private readonly object _lock = new();

    private readonly object _publishLock = new();

    private RestartReason? _configurationReason;

    private RestartReason? _pluginStateReason;

    private readonly Dictionary<RestartRequirement, RestartReason> _pluginReasons = [];

    private IReadOnlyList<RestartReason> _published = [];

    private bool _trackPluginState;

    private bool _publishing;

    private bool _publishDirty;

    #endregion

    #region Constructor

    /// <summary>
    ///   Creates the tracker and starts following the configuration service.
    /// </summary>
    /// <param name="logger">The logger to report added and cleared reasons to.</param>
    /// <param name="configurationService">The configuration service to read pending members from.</param>
    /// <param name="pluginManager">The plugin manager to read plugin states and owners from.</param>
    /// <param name="timeProvider">The clock to stamp reasons with, or <see langword="null"/> for the system clock.</param>
    public RestartReasonTracker(ILogger logger, IConfigurationService configurationService, IPluginManager pluginManager, TimeProvider? timeProvider = null)
    {
        _logger = logger;
        _configurationService = configurationService;
        _pluginManager = pluginManager;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _configurationService.Saved += OnConfigurationSaved;
        _configurationService.RequiresRestart += OnConfigurationRequiresRestart;
    }

    #endregion

    #region Reasons

    /// <summary>
    ///   Dispatched, synchronously, whenever the set of reasons changes, with
    ///   every reason that stands afterwards.
    /// </summary>
    public event EventHandler<RestartReasonsChangedEventArgs>? Changed;

    /// <summary>
    ///   The sender <see cref="Changed"/> is raised with, or
    ///   <see langword="null"/> for the tracker itself.
    /// </summary>
    public object? Sender { get; init; }

    /// <summary>
    ///   Every reason that stands, oldest first. Reading it catches up with
    ///   the configuration service and the plugin manager first.
    /// </summary>
    public IReadOnlyList<RestartReason> Reasons
    {
        get
        {
            RefreshConfigurations();
            RefreshPluginState();
            lock (_lock)
                return BuildSnapshot();
        }
    }

    private IReadOnlyList<RestartReason> BuildSnapshot()
        => new[] { _configurationReason, _pluginStateReason }
            .OfType<RestartReason>()
            .Concat(_pluginReasons.Values)
            .OrderBy(reason => reason.RaisedAt)
            .ThenBy(reason => reason.Source)
            .ThenBy(reason => reason.PluginID)
            .ThenBy(reason => reason.Key, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    ///   Raises <see cref="Changed"/> if the reasons differ from the last ones
    ///   raised. Reasons are immutable and only ever added or removed, so
    ///   comparing references is enough. A handler that changes the reasons
    ///   is answered once every handler got the list it is handling, so no
    ///   handler ends up with an older list than the one before it.
    /// </summary>
    private void Publish()
    {
        lock (_publishLock)
        {
            // Only the publishing thread gets past the lock while it runs, so this
            // is a handler changing the reasons; the loop below picks that up.
            if (_publishing)
            {
                _publishDirty = true;
                return;
            }

            _publishing = true;
            try
            {
                do
                {
                    _publishDirty = false;
                    PublishSnapshot();
                } while (_publishDirty);
            }
            finally
            {
                _publishing = false;
                _publishDirty = false;
            }
        }
    }

    /// <summary>
    ///   Raises <see cref="Changed"/> once with the current reasons if they
    ///   differ from the last ones raised, calling each handler on its own so
    ///   one that throws does not keep the others from being told.
    /// </summary>
    private void PublishSnapshot()
    {
        IReadOnlyList<RestartReason> current;
        lock (_lock)
            current = BuildSnapshot();

        if (current.Count == _published.Count && current.Zip(_published).All(pair => ReferenceEquals(pair.First, pair.Second)))
            return;

        _published = current;
        if (Changed is not { } changed)
            return;

        var sender = Sender ?? this;
        var eventArgs = new RestartReasonsChangedEventArgs { Reasons = current };
        foreach (var handler in changed.GetInvocationList().Cast<EventHandler<RestartReasonsChangedEventArgs>>())
        {
            try
            {
                handler(sender, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A handler threw while being told the restart reasons changed.");
            }
        }
    }

    #endregion

    #region Configurations

    private void OnConfigurationSaved(object? sender, ConfigurationSavedEventArgs eventArgs)
        => RefreshConfigurations();

    private void OnConfigurationRequiresRestart(object? sender, ConfigurationRequiresRestartEventArgs eventArgs)
        => RefreshConfigurations();

    /// <summary>
    ///   Brings the configuration reason in line with the members the
    ///   configuration service has pending: it stands while any configuration
    ///   has some, and clears once they are all back to their original values.
    ///   The server's enabled plugins are left out, since the plugin state
    ///   reason stands for them.
    /// </summary>
    public void RefreshConfigurations()
    {
        bool changed;
        lock (_lock)
        {
            // Read under the lock, so a refresh running late for an older save
            // cannot bring back a reason a newer one already cleared.
            var pending = _configurationService.RestartPendingFor.Any(pair => HasReasonMembers(pair.Key, pair.Value));
            changed = UpdateServiceReason(ref _configurationReason, pending, RestartReasonSource.Configuration, ConfigurationKey, ConfigurationDescription);
        }

        if (changed)
            Publish();
    }

    /// <summary>
    ///   Tells whether a configuration's pending members call for the
    ///   configuration reason.
    /// </summary>
    /// <param name="configurationID">The configuration the members belong to.</param>
    /// <param name="members">The member paths the configuration service has pending.</param>
    /// <returns>
    ///   <see langword="true"/> if the configuration is still known and has a
    ///   pending member other than the server's enabled plugins.
    /// </returns>
    private bool HasReasonMembers(Guid configurationID, IReadOnlySet<string> members)
    {
        if (members.Count is 0 || _configurationService.GetConfigurationInfo(configurationID) is not { } info)
            return false;

        return info.Type != typeof(ServerSettings) || members.Any(member => !string.Equals(member, EnabledPluginsMember, StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Plugin State

    /// <summary>
    ///   Starts following the plugin states. Called once the plugins are
    ///   initialized, since until then no plugin is active and every enabled
    ///   one would look like it waits on a restart.
    /// </summary>
    public void StartTrackingPluginState()
    {
        lock (_lock)
            _trackPluginState = true;

        RefreshPluginState();
    }

    /// <summary>
    ///   Brings the plugin state reason in line with the plugin manager: it
    ///   stands while any plugin's next start would load a different version,
    ///   or none, than the one loaded now. An enabled plugin that is not loaded
    ///   and cannot load never raises it, while a loaded one that can no
    ///   longer load does, which matches <see cref="LocalPluginInfo.RestartPending"/>.
    ///   Does nothing until <see cref="StartTrackingPluginState"/> is called.
    /// </summary>
    public void RefreshPluginState()
    {
        bool changed;
        lock (_lock)
        {
            if (!_trackPluginState)
                return;

            var pending = _pluginManager.GetPluginInfos()
                .ToArray()
                .GroupBy(pluginInfo => pluginInfo.ID)
                .Any(group => HasPendingStateChange(group.ToList()));
            changed = UpdateServiceReason(ref _pluginStateReason, pending, RestartReasonSource.PluginState, PluginStateKey, PluginStateDescription);
        }

        if (changed)
            Publish();
    }

    /// <summary>
    ///   Tells whether a restart would change what loads for one plugin, by
    ///   comparing the version loaded now with the one the next start would
    ///   pick: the enabled one that can load, pinned first and then the
    ///   highest.
    /// </summary>
    /// <param name="versions">Every registered version of one plugin.</param>
    /// <returns>
    ///   <see langword="true"/> if a restart would load another version, or
    ///   none where one is loaded now, or one where none is.
    /// </returns>
    internal static bool HasPendingStateChange(IReadOnlyList<LocalPluginInfo> versions)
    {
        var active = versions.FirstOrDefault(pluginInfo => pluginInfo.IsActive);
        var next = versions
            .Where(pluginInfo => pluginInfo is { IsEnabled: true, CanLoad: true })
            .OrderByDescending(pluginInfo => pluginInfo.IsPinned)
            .ThenByDescending(pluginInfo => pluginInfo.Version.Version)
            .FirstOrDefault();
        return !ReferenceEquals(active, next);
    }

    #endregion

    #region Service Reasons

    /// <summary>
    ///   Raises or clears one of the reasons the core holds for a whole
    ///   service. A reason that already stands is kept as it is, so it keeps
    ///   its <see cref="RestartReason.RaisedAt"/>. Call it under the lock.
    /// </summary>
    /// <param name="reason">The field holding the reason, or <see langword="null"/> while none stands.</param>
    /// <param name="stands">Whether the reason should stand.</param>
    /// <param name="source">The source of the reason.</param>
    /// <param name="key">The key of the reason.</param>
    /// <param name="description">The description of the reason.</param>
    /// <returns><see langword="true"/> if the reason was raised or cleared, otherwise <see langword="false"/>.</returns>
    private bool UpdateServiceReason(ref RestartReason? reason, bool stands, RestartReasonSource source, string key, string description)
    {
        if (stands == reason is not null)
            return false;

        if (!stands)
        {
            _logger.LogInformation("A restart is no longer required: {Description}", reason!.Description);
            reason = null;
            return true;
        }

        reason = new RestartReason
        {
            Source = source,
            PluginID = CorePlugin.StaticID,
            Key = key,
            Description = description,
            RaisedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };
        _logger.LogInformation("A restart is required: {Description}", description);
        return true;
    }

    #endregion

    #region Plugin Reasons

    /// <summary>
    ///   Raises a reason owned by a plugin and hands out the hold on it.
    /// </summary>
    /// <param name="plugin">The active plugin raising the reason.</param>
    /// <param name="description">A short, human-readable description of the change.</param>
    /// <returns>The hold on the reason; disposing it clears the reason.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="plugin"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    ///   <paramref name="plugin"/> is not active, or <paramref name="description"/> is empty.
    /// </exception>
    public IRestartRequirement Require(LocalPluginInfo plugin, string description)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        if (!plugin.IsActive)
            throw new ArgumentException($"\"{plugin.Name}\" is not an active plugin, so it cannot raise restart reasons.", nameof(plugin));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A restart reason needs a description.", nameof(description));

        description = description.Trim();
        var reason = new RestartReason
        {
            Source = RestartReasonSource.Plugin,
            PluginID = plugin.ID,
            // The handle is the identity; the key only tells clients the reasons apart.
            Key = Guid.NewGuid().ToString("N"),
            Description = description,
            RaisedAt = _timeProvider.GetUtcNow().UtcDateTime,
        };
        var holder = new RestartRequirement(this, plugin, reason);
        lock (_lock)
            _pluginReasons[holder] = reason;

        _logger.LogInformation("A restart is required by plugin {PluginName}: {Description}", plugin.Name, description);
        Publish();
        return holder;
    }

    /// <summary>
    ///   Clears the reason a handle holds.
    /// </summary>
    /// <param name="holder">The handle being disposed.</param>
    /// <returns><see langword="true"/> if the reason was cleared, otherwise <see langword="false"/>.</returns>
    internal bool Release(RestartRequirement holder)
    {
        lock (_lock)
        {
            if (!_pluginReasons.Remove(holder))
                return false;
        }

        _logger.LogInformation("A restart is no longer required by plugin {PluginName}: {Description}", holder.Plugin.Name, holder.Reason.Description);
        Publish();
        return true;
    }

    #endregion
}

/// <summary>
///   A plugin's hold on one of its restart reasons, handed out by
///   <see cref="RestartReasonTracker.Require"/>.
/// </summary>
/// <param name="tracker">The tracker the reason stands in.</param>
/// <param name="plugin">The plugin that raised the reason.</param>
/// <param name="reason">The reason the handle holds.</param>
internal sealed class RestartRequirement(RestartReasonTracker tracker, LocalPluginInfo plugin, RestartReason reason) : IRestartRequirement
{
    private int _disposed;

    /// <inheritdoc/>
    public RestartReason Reason { get; } = reason;

    /// <inheritdoc/>
    public LocalPluginInfo Plugin { get; } = plugin;

    /// <inheritdoc/>
    public bool IsHeld => Volatile.Read(ref _disposed) == 0;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            tracker.Release(this);
    }
}
