using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Connectivity.Services;
using Shoko.Abstractions.Connectivity.Suspensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Plugin;
using Shoko.Server.Utilities;

namespace Shoko.Server.Services;

/// <summary>
///   Registers the suspension providers and keeps what they report, clearing
///   each suspension once its end passes. Thread-safe.
/// </summary>
public sealed class SuspensionService : ISuspensionService, IDisposable
{
    #region Fields

    private readonly IPluginManager _pluginManager;

    private readonly IMetadataProviderManager _providerManager;

    private readonly ILogger<SuspensionService> _logger;

    private readonly TimeProvider _timeProvider;

    private readonly Lock _lock = new();

    private List<Entry>? _entries;

    private Dictionary<Type, Entry> _entriesByType = [];

    private ITimer? _expiryTimer;

    private bool _disposed;

    /// <summary>
    ///   A registered provider and its suspensions.
    /// </summary>
    /// <param name="info">The provider's registration.</param>
    private sealed class Entry(SuspensionProviderInfo info)
    {
        /// <summary>
        ///   The provider's registration.
        /// </summary>
        public SuspensionProviderInfo Info { get; } = info;

        /// <summary>
        ///   The suspensions on now, by kind.
        /// </summary>
        public Dictionary<SuspensionKind, Suspension> Active { get; } = [];

        /// <summary>
        ///   The kinds being lifted now and who lifted them, so the provider
        ///   resuming one from its <see cref="ISuspensionProvider.Lift"/> counts
        ///   as lifting it.
        /// </summary>
        public Dictionary<SuspensionKind, ApiToken?> Lifting { get; } = [];

        /// <summary>
        ///   The status as of the last change.
        /// </summary>
        public SuspensionStatus Status { get; set; } = null!;
    }

    #endregion

    #region Constructors

    /// <summary>
    ///   Creates the service.
    /// </summary>
    /// <param name="pluginManager">Tells which plugin a provider belongs to.</param>
    /// <param name="providerManager">The metadata providers, to tell which hold a source.</param>
    /// <param name="logger">Where refused providers and changes are logged.</param>
    public SuspensionService(IPluginManager pluginManager, IMetadataProviderManager providerManager, ILogger<SuspensionService> logger)
        : this(pluginManager, providerManager, logger, TimeProvider.System) { }

    /// <summary>
    ///   Creates the service on a clock of its own, for the tests.
    /// </summary>
    /// <param name="pluginManager">Tells which plugin a provider belongs to.</param>
    /// <param name="providerManager">The metadata providers, to tell which hold a source.</param>
    /// <param name="logger">Where refused providers and changes are logged.</param>
    /// <param name="timeProvider">The clock the suspensions run out by.</param>
    internal SuspensionService(IPluginManager pluginManager, IMetadataProviderManager providerManager, ILogger<SuspensionService> logger, TimeProvider timeProvider)
    {
        _pluginManager = pluginManager;
        _providerManager = providerManager;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    #endregion

    #region Registration

    /// <summary>
    ///   Takes the suspension providers the plugins export, in plugin load
    ///   order. Called once during start-up; later calls have no effect.
    /// </summary>
    /// <param name="providers">The providers.</param>
    public void AddParts(IEnumerable<ISuspensionProvider> providers)
    {
        if (_entries is not null)
            return;

        var registered = new List<Entry>();
        foreach (var provider in providers)
        {
            if (Register(provider, registered) is not { } info)
                continue;

            var entry = new Entry(info);
            entry.Status = BuildStatus(entry);
            registered.Add(entry);
        }

        lock (_lock)
        {
            _entriesByType = registered.ToDictionary(entry => entry.Info.Provider.GetType());
            _entries = registered;
        }

        if (registered.Count > 0)
            _logger.LogInformation(
                "Registered {Count} suspension providers: {Providers}.",
                registered.Count,
                string.Join(", ", registered.Select(entry => entry.Info.Name))
            );
    }

    /// <summary>
    ///   Builds a provider's registration, or logs why it is refused.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="registered">The providers taken before it.</param>
    /// <returns>The registration, or <c>null</c> when it is refused.</returns>
    private SuspensionProviderInfo? Register(ISuspensionProvider provider, List<Entry> registered)
    {
        var providerType = provider.GetType();
        if (_pluginManager.GetPluginInfo(providerType.Assembly) is not { } pluginInfo)
        {
            _logger.LogWarning("Refusing suspension provider {Provider}: it does not belong to a loaded plugin.", providerType.FullName);
            return null;
        }

        if (registered.Any(entry => entry.Info.Provider.GetType() == providerType))
        {
            _logger.LogWarning("Refusing suspension provider {Provider}: one of its type is registered already.", providerType.FullName);
            return null;
        }

        string name;
        string? description;
        IReadOnlyList<Type> heldTypes;
        try
        {
            name = provider.Name;
            description = provider.Description;
            heldTypes = [.. (provider.HeldProviderTypes ?? []).Where(type => type is not null).Distinct()];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Refusing suspension provider {Provider}: reading its name or held types failed.", providerType.FullName);
            return null;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogError("Refusing suspension provider {Provider}: it has no name.", providerType.FullName);
            return null;
        }

        return new()
        {
            ID = GetID(providerType, pluginInfo.ID),
            Provider = provider,
            PluginInfo = pluginInfo,
            Name = name,
            Description = description?.CleanDescription() is { Length: > 0 } cleaned ? cleaned : null,
            HeldProviderTypes = heldTypes,
        };
    }

    /// <summary>
    ///   Derives a provider's ID from its type and its plugin.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <param name="pluginID">The plugin's ID.</param>
    /// <returns>The ID.</returns>
    internal static Guid GetID(Type providerType, Guid pluginID)
        => UuidUtility.GetV5($"SuspensionProvider={providerType.FullName!}", pluginID);

    #endregion

    #region Providers

    /// <inheritdoc />
    public IEnumerable<SuspensionProviderInfo> GetAvailableProviders()
        => Entries.Select(entry => entry.Info);

    /// <inheritdoc />
    public IReadOnlyList<SuspensionProviderInfo> GetProviderInfo(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);
        return [.. Entries.Where(entry => entry.Info.PluginInfo.ID == plugin.ID).Select(entry => entry.Info)];
    }

    /// <inheritdoc />
    public SuspensionProviderInfo? GetProviderInfo(Guid providerID)
        => Find(providerID)?.Info;

    /// <inheritdoc />
    public SuspensionProviderInfo GetProviderInfo(ISuspensionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return Entries.FirstOrDefault(entry => ReferenceEquals(entry.Info.Provider, provider))?.Info
            ?? throw new ArgumentException($"Unregistered suspension provider: '{provider.GetType().Name}'", nameof(provider));
    }

    /// <inheritdoc />
    public SuspensionProviderInfo GetProviderInfo<TProvider>() where TProvider : class, ISuspensionProvider
        => GetProviderInfo(typeof(TProvider));

    /// <inheritdoc />
    public SuspensionProviderInfo GetProviderInfo(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);
        return Find(providerType)?.Info
            ?? throw new ArgumentException($"Unregistered suspension provider: '{providerType.Name}'", nameof(providerType));
    }

    private IReadOnlyList<Entry> Entries => _entries ?? [];

    private Entry? Find(Guid providerID)
        => providerID == Guid.Empty ? null : Entries.FirstOrDefault(entry => entry.Info.ID == providerID);

    private Entry? Find(Type providerType)
    {
        lock (_lock)
            return _entriesByType.GetValueOrDefault(providerType);
    }

    #endregion

    #region Statuses

    /// <inheritdoc />
    public event EventHandler<SuspensionChangedEventArgs>? SuspensionChanged;

    /// <inheritdoc />
    public IReadOnlyList<SuspensionStatus> GetAll()
    {
        lock (_lock)
            return [.. Entries.Select(entry => entry.Status)];
    }

    /// <inheritdoc />
    public SuspensionStatus? Get(Guid providerID)
    {
        if (Find(providerID) is not { } entry)
            return null;

        lock (_lock)
            return entry.Status;
    }

    /// <inheritdoc />
    public IReadOnlyList<SuspensionStatus> GetForSource(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var providerTypes = _providerManager.MetadataProviders
            .Where(info => info.Source == source)
            .Select(info => info.Provider.GetType())
            .ToHashSet();
        if (providerTypes.Count is 0)
            return [];

        lock (_lock)
            return [.. Entries.Where(entry => entry.Info.HeldProviderTypes.Any(providerTypes.Contains)).Select(entry => entry.Status)];
    }

    /// <summary>
    ///   The statuses of the suspended providers holding back a provider type.
    /// </summary>
    /// <param name="heldType">The type of the metadata or release provider.</param>
    /// <returns>The statuses, which is empty while nothing holds it back.</returns>
    internal IReadOnlyList<SuspensionStatus> GetHolding(Type heldType)
    {
        ArgumentNullException.ThrowIfNull(heldType);

        lock (_lock)
            return [.. Entries.Where(entry => entry.Status.IsSuspended && entry.Info.HeldProviderTypes.Contains(heldType)).Select(entry => entry.Status)];
    }

    /// <inheritdoc />
    public async Task<bool> Lift(Guid providerID, SuspensionKind kind, CancellationToken token = default)
    {
        if (Find(providerID) is not { } entry)
            return false;

        lock (_lock)
        {
            if (!entry.Active.TryGetValue(kind, out var suspension) || !suspension.IsLiftable || !entry.Lifting.TryAdd(kind, ActorContext.CurrentActor))
                return false;
        }

        try
        {
            await entry.Info.Provider.Lift(kind, token).ConfigureAwait(false);
            _logger.LogInformation("Lifted the {Kind} suspension of {Provider}.", kind, entry.Info.Name);
            Remove(entry, [kind], SuspensionRemovalCause.Lifted);
        }
        finally
        {
            lock (_lock)
                entry.Lifting.Remove(kind);
        }

        return true;
    }

    #endregion

    #region Reporting

    /// <summary>
    ///   The current status of a provider, for its reporter.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <returns>The status.</returns>
    /// <exception cref="InvalidOperationException">The type is not a loaded provider.</exception>
    internal SuspensionStatus GetStatus(Type providerType)
    {
        var entry = Require(providerType);
        lock (_lock)
            return entry.Status;
    }

    /// <summary>
    ///   Raises or updates a provider's suspension of a kind, for its reporter.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="reason">The detail, or <c>null</c>.</param>
    /// <param name="resumesAt">The end, or <c>null</c>.</param>
    /// <param name="isLiftable">Whether an admin may lift it.</param>
    /// <exception cref="InvalidOperationException">The type is not a loaded provider.</exception>
    internal void Suspend(Type providerType, SuspensionKind kind, string? reason, DateTime? resumesAt, bool isLiftable)
    {
        var entry = Require(providerType);
        var end = resumesAt is { } value ? ToUtc(value) : (DateTime?)null;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (end <= now)
            return;

        SuspensionChangedEventArgs args;
        lock (_lock)
        {
            if (_disposed)
                return;

            var previous = entry.Status;
            Suspension? raised = null;
            Suspension? updated = null;
            if (entry.Active.TryGetValue(kind, out var existing))
            {
                var next = existing with { Reason = reason, ResumesAt = end, IsLiftable = isLiftable };
                if (next == existing)
                    return;

                entry.Active[kind] = updated = next;
            }
            else
            {
                entry.Active[kind] = raised = new()
                {
                    Kind = kind,
                    Reason = reason,
                    RaisedAt = now,
                    ResumesAt = end,
                    IsLiftable = isLiftable,
                };
            }

            entry.Status = BuildStatus(entry);
            ArmExpiryTimer(now);
            args = new()
            {
                Previous = previous,
                Current = entry.Status,
                Raised = raised is null ? [] : [raised],
                Updated = updated is null ? [] : [updated],
                Removed = [],
            };
        }

        _logger.Log(
            args.Raised.Count > 0 ? LogLevel.Information : LogLevel.Debug,
            "{Provider} is suspended ({Kind}) until {ResumesAt}.",
            entry.Info.Name,
            kind,
            end is { } until ? until.ToString("u") : "resumed"
        );
        Raise(args);
    }

    /// <summary>
    ///   Ends a provider's suspensions of the given kinds, or all of them, for
    ///   its reporter.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <param name="kind">The kind, or <c>null</c> for all.</param>
    /// <exception cref="InvalidOperationException">The type is not a loaded provider.</exception>
    internal void Resume(Type providerType, SuspensionKind? kind)
    {
        var entry = Require(providerType);
        IReadOnlyList<SuspensionKind> kinds;
        if (kind is { } single)
        {
            kinds = [single];
        }
        else
        {
            lock (_lock)
                kinds = [.. entry.Active.Keys];
        }

        Remove(entry, kinds, SuspensionRemovalCause.Resumed);
    }

    private Entry Require(Type providerType)
        => Find(providerType)
            ?? throw new InvalidOperationException(
                _entries is null
                    ? $"Suspension provider '{providerType.Name}' reported before the providers were registered."
                    : $"'{providerType.Name}' is not a loaded suspension provider."
            );

    private void Remove(Entry entry, IReadOnlyList<SuspensionKind> kinds, SuspensionRemovalCause cause)
    {
        SuspensionChangedEventArgs? args;
        lock (_lock)
            args = RemoveUnderLock(entry, kinds, cause);

        if (args is null)
            return;

        _logger.LogInformation(
            "{Provider} is no longer suspended for {Kinds} ({Cause}).",
            entry.Info.Name,
            string.Join(", ", args.Removed.Select(removal => removal.Suspension.Kind)),
            args.Removed[0].Cause
        );
        Raise(args);
    }

    // Called under the lock.
    private SuspensionChangedEventArgs? RemoveUnderLock(Entry entry, IEnumerable<SuspensionKind> kinds, SuspensionRemovalCause cause)
    {
        var removed = new List<SuspensionRemoval>();
        ApiToken? actor = null;
        foreach (var kind in kinds)
        {
            if (entry.Active.Remove(kind, out var suspension))
            {
                var lifted = cause is SuspensionRemovalCause.Lifted || (cause is SuspensionRemovalCause.Resumed && entry.Lifting.ContainsKey(kind));
                removed.Add(new(suspension, lifted ? SuspensionRemovalCause.Lifted : cause));
                if (lifted && entry.Lifting.TryGetValue(kind, out var lifter))
                    actor ??= lifter;
            }
        }

        if (removed.Count is 0)
            return null;

        var previous = entry.Status;
        entry.Status = BuildStatus(entry);
        return new()
        {
            Previous = previous,
            Current = entry.Status,
            Raised = [],
            Updated = [],
            Removed = removed,
            Actor = actor,
        };
    }

    private static SuspensionStatus BuildStatus(Entry entry)
    {
        var suspensions = entry.Active.Values.OrderBy(suspension => suspension.Kind).ToList();
        return new()
        {
            Provider = entry.Info,
            Suspensions = suspensions,
            IsSuspended = suspensions.Count > 0,
            ResumesAt = suspensions.Count is 0 || suspensions.Any(suspension => suspension.ResumesAt is null)
                ? null
                : suspensions.Max(suspension => suspension.ResumesAt),
        };
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Local => value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value,
    };

    private void Raise(SuspensionChangedEventArgs args)
    {
        if (SuspensionChanged is not { } handlers)
            return;

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler<SuspensionChangedEventArgs>>())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A handler of a suspension change of {Provider} failed.", args.Current.Provider.Name);
            }
        }
    }

    #endregion

    #region Expiry

    // Called under the lock.
    private void ArmExpiryTimer(DateTime now)
    {
        if (_disposed)
            return;

        var next = Entries
            .SelectMany(entry => entry.Active.Values)
            .Select(suspension => suspension.ResumesAt)
            .Where(end => end is not null)
            .Min();
        if (next is not { } nextEnd)
        {
            _expiryTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            return;
        }

        var dueTime = nextEnd - now;
        dueTime = dueTime < TimeSpan.Zero ? TimeSpan.Zero : dueTime > TimeSpan.FromDays(1) ? TimeSpan.FromDays(1) : dueTime;
        if (_expiryTimer is { } timer)
        {
            timer.Change(dueTime, Timeout.InfiniteTimeSpan);
            return;
        }

        // The timer lives as long as the service, so it takes nothing of the reporter's context along.
        using (DetachedFlow.Suppress())
            _expiryTimer = _timeProvider.CreateTimer(_ => Expire(), null, dueTime, Timeout.InfiniteTimeSpan);
    }

    /// <summary>
    ///   Clears every suspension whose end passed and arms the timer for the
    ///   next one.
    /// </summary>
    internal void Expire()
    {
        var changes = new List<(Entry Entry, SuspensionChangedEventArgs Args)>();
        lock (_lock)
        {
            if (_disposed)
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var entry in Entries)
            {
                var expired = entry.Active.Values.Where(suspension => suspension.ResumesAt <= now).Select(suspension => suspension.Kind).ToList();
                if (expired.Count > 0 && RemoveUnderLock(entry, expired, SuspensionRemovalCause.Expired) is { } args)
                    changes.Add((entry, args));
            }

            ArmExpiryTimer(now);
        }

        foreach (var (entry, args) in changes)
        {
            _logger.LogInformation(
                "{Provider} is no longer suspended for {Kinds}: the suspension ran out.",
                entry.Info.Name,
                string.Join(", ", args.Removed.Select(removal => removal.Suspension.Kind))
            );
            Raise(args);
        }
    }

    #endregion

    #region Disposal

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _expiryTimer?.Dispose();
            _expiryTimer = null;
        }
    }

    #endregion
}
