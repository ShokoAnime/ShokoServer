using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Filtering.Services;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Filters;
using Shoko.Server.Models.Airing;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.Airing;

#nullable enable
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Services;

/// <summary>
/// The single point of entry for everything airing-schedule related: the
/// shared channel registry, the schedules providers own, the episode airings
/// on them, the links between those airings, the estimates a schedule's own
/// line produces, and the refresh requests core carries to providers.
/// </summary>
/// <remarks>
/// Providers fetch in their own jobs and push what they find through here, so
/// every change takes the provider instance and is checked against the
/// registered object and the owner stored on the schedule. Reads are cached
/// lookups with lazy enrichment: nothing but the stored rows is materialised
/// until a caller actually asks for it.
/// </remarks>
public partial class AiringScheduleService(
    ILogger<AiringScheduleService> logger,
    IConfigurationService configurationService,
    IPluginManager pluginManager,
    IApplicationPaths applicationPaths,
    IQueueScheduler schedulerFactory,
    ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider,
    Lazy<IMetadataService> metadataService,
    Lazy<IMetadataCrossReferenceStore> crossReferenceStore,
    Lazy<IMetadataLinkingService> linkingService,
    Lazy<IMetadataFilteringService> filteringService,
    TimeProvider? timeProvider = null
) : IAiringScheduleService
{
    /// <summary>
    /// Every kind there is, which is what a read that includes disabled data,
    /// or one whose provider is gone, sees.
    /// </summary>
    internal static readonly IReadOnlySet<AiringKind> AllKinds = Enum.GetValues<AiringKind>().ToHashSet();

    /// <summary>
    /// How long a schedule with no airings is left alone before the retention
    /// sweep takes it, so a provider that creates a schedule and fills it in a
    /// follow-up job doesn't lose it mid-run.
    /// </summary>
    internal static readonly TimeSpan EmptyScheduleBuffer = TimeSpan.FromHours(1);

    private readonly Lock _lock = new();

    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    private Dictionary<Guid, AiringScheduleProviderInfo> _providerInfos = [];

    private readonly ConcurrentDictionary<int, AiringScheduleProfile> _profiles = [];

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLimits = [];

    private readonly ConcurrentDictionary<string, List<TaskCompletionSource<AiringScheduleProviderRefresh>>> _refreshWaiters = [];

    private ConcurrentDictionary<Guid, int>? _airingIDs;

    private bool _loaded;

    private readonly AnidbLinkedAirDateCache _linkedAirDates = new(crossReferenceStore, metadataService, linkingService);

    private AnidbAiringDateCache? _anidbAiringDates;

    /// <summary>
    /// Resolves an entry through the metadata service's lookup: the core's
    /// tables for its own sources, and for any other a plugin's own metadata
    /// resolver, then the metadata stores.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <param name="id">The source's ID for it.</param>
    /// <returns>The entry, or <c>null</c> when the core holds none.</returns>
    internal IMetadata? GetStoredEntity(MetadataSource source, MetadataEntityType entityType, string id)
        => MetadataEntries.ToGuid(source, entityType, id) is { } guid ? metadataService.Value.GetEntry(guid) : null;

    /// <summary>
    /// The store the links between AniDB entries and every other source's are
    /// read from, for the estimates of episodes a schedule's source does not
    /// list yet.
    /// </summary>
    internal IMetadataCrossReferenceStore CrossReferences => crossReferenceStore.Value;

    /// <summary>
    /// The current time, in UTC, from the service's time provider. A read
    /// takes it once, through its <see cref="AiringReadContext"/>.
    /// </summary>
    internal DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// The AniDB anime of the Shoko series a filter passes for a user.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="user">The user, needed when the filter depends on one.</param>
    /// <exception cref="ArgumentNullException">
    /// The filter depends on the user and <paramref name="user"/> is <c>null</c>.
    /// </exception>
    /// <returns>The AniDB anime IDs.</returns>
    internal IReadOnlyList<int> GetFilteredAnimeIDs(IFilter filter, IUser? user)
        => filteringService.Value.GetFilteredAnimeIDs(filter, user);

    /// <inheritdoc/>
    public ConfigurationInfo ConfigurationInfo => configurationProvider.ConfigurationInfo;

    /// <inheritdoc/>
    public event EventHandler? ProvidersUpdated;

    /// <inheritdoc/>
    public event EventHandler<AiringChannelEventArgs>? ChannelRegistered;

    /// <inheritdoc/>
    public event EventHandler<AiringScheduleEventArgs>? ScheduleUpdated;

    /// <inheritdoc/>
    public event EventHandler<EpisodeAiringsUpdatedEventArgs>? AiringsUpdated;

    #region Airing Notifications

    /// <summary>
    /// The live airing subscriptions, keyed by subscription ID. A
    /// <see cref="ConcurrentDictionary{TKey, TValue}"/> because a plugin may
    /// subscribe or dispose from any thread while the ticker is walking them.
    /// </summary>
    private readonly ConcurrentDictionary<Guid, AiringSubscription> _airingSubscriptions = [];

    private int _airingSubscriptionVersion;

    /// <summary>
    /// Whether the parts have been added, so the background ticker can hold off
    /// until the providers and resolvers are in place rather than reading a
    /// half-built service.
    /// </summary>
    internal bool HasParts => _loaded;

    /// <summary>
    /// Bumped whenever a subscription is added or removed, so the ticker can
    /// tell that the horizon it built no longer matches what is being asked
    /// for. This is what re-arms it the moment the first subscriber turns up,
    /// rather than at the next periodic refresh.
    /// </summary>
    internal int AiringSubscriptionVersion => Volatile.Read(ref _airingSubscriptionVersion);

    /// <inheritdoc/>
    public IDisposable SubscribeToAirings(Action<EpisodeAiredEventArgs> handler, EpisodeAiringFilteringOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new AiringSubscription(Guid.NewGuid(), handler, options, DescribeSubscriber(handler));
        _airingSubscriptions[subscription.ID] = subscription;
        Interlocked.Increment(ref _airingSubscriptionVersion);
        logger.LogTrace("Added airing subscription {Subscriber} ({SubscriptionID}).", subscription.Subscriber, subscription.ID);

        return new DisposableAction(() =>
        {
            if (!_airingSubscriptions.TryRemove(subscription.ID, out _))
                return;

            Interlocked.Increment(ref _airingSubscriptionVersion);
            logger.LogTrace("Removed airing subscription {Subscriber} ({SubscriptionID}).", subscription.Subscriber, subscription.ID);
        });
    }

    /// <summary>
    /// The filters the ticker builds its horizon with: the union of what every
    /// live subscriber asked for, or <c>null</c> when nobody is
    /// subscribed and there is therefore nothing to build.
    /// </summary>
    /// <remarks>
    /// A union rather than the widest possible read, because the widest read is
    /// the expensive one. <see cref="EpisodeAiringFilteringOptions.IncludeEstimates"/>
    /// in particular runs the estimate pipeline over the whole window, and it
    /// is only set here when a live subscriber actually wants estimates.
    /// </remarks>
    /// <returns>The union of the live subscriptions' filters, or <c>null</c>.</returns>
    internal EpisodeAiringFilteringOptions? GetAiringHorizonOptions()
    {
        var subscriptions = _airingSubscriptions.Values.ToList();
        if (subscriptions.Count is 0)
            return null;

        var union = new EpisodeAiringFilteringOptions()
        {
            // A read with no current slot never airs, and the gap a calendar
            // draws for a delayed airing is not an episode airing either, so
            // neither is ever part of the horizon however anyone subscribed.
            IncludeDelayedOriginalSlots = false,
            IncludeEstimates = false,
            IncludeDisabled = false,
            // Preference only ever orders a read, and a dispatch is in slot
            // order, so the widest value is also the only sensible one.
            PreferredOnly = false,
            // The widest value: a subscriber that only wants an entity's own
            // airings still needs the links walked to have them found at all.
            LinkedEntityAirings = true,
            EntityAnchor = AiringEntityAnchor.Raw,
        };
        var providerIDs = new HashSet<Guid>();
        var kinds = new HashSet<AiringKind>();
        var languages = new HashSet<TitleLanguage>();
        var channelIDs = new HashSet<Guid>();
        var anyProviderID = false;
        var anyKinds = false;
        var anyLanguages = false;
        var anyChannelIDs = false;
        foreach (var subscription in subscriptions)
        {
            var options = subscription.Options;
            if (options is null)
            {
                // A subscriber that filters nothing widens every set at once,
                // which is the cheapest way to say "the union is everything".
                anyProviderID = anyKinds = anyLanguages = anyChannelIDs = true;
                union.IncludeEstimates = true;
                union.IncludeDisabled = true;
                continue;
            }

            union.IncludeEstimates |= options.IncludeEstimates;
            union.IncludeDisabled |= options.IncludeDisabled;
            if (options.ProviderIDs is { } subscriberProviderIDs)
                providerIDs.UnionWith(subscriberProviderIDs);
            else
                anyProviderID = true;
            if (options.Kinds is { } subscriberKinds)
                kinds.UnionWith(subscriberKinds);
            else
                anyKinds = true;
            if (options.Languages is { } subscriberLanguages)
                languages.UnionWith(subscriberLanguages);
            else
                anyLanguages = true;
            if (options.ChannelIDs is { } subscriberChannelIDs)
                channelIDs.UnionWith(subscriberChannelIDs);
            else
                anyChannelIDs = true;
        }

        union.ProviderIDs = anyProviderID ? null : providerIDs;
        union.Kinds = anyKinds ? null : kinds;
        union.Languages = anyLanguages ? null : languages;
        union.ChannelIDs = anyChannelIDs ? null : channelIDs;
        return union;
    }

    /// <summary>
    /// Hands one minute's airings to every subscriber the minute has something
    /// for, each filtered by its own options. Only
    /// <see cref="EpisodeAiringNotificationService"/> calls this: the ticker
    /// owns the horizon and the watermark, and the service owns the
    /// subscriptions the contract exposes.
    /// </summary>
    /// <remarks>
    /// Each handler is wrapped on its own, so a plugin that throws costs itself
    /// its dispatch and nothing else — not the tick, and not the subscribers
    /// behind it.
    /// </remarks>
    /// <param name="minute">The minute that passed, in UTC.</param>
    /// <param name="airings">The airings whose slot passed, in slot order.</param>
    /// <exception cref="ArgumentNullException"><paramref name="airings"/> is <c>null</c>.</exception>
    internal void DispatchEpisodesAired(DateTime minute, IReadOnlyList<IEpisodeAiring> airings)
    {
        ArgumentNullException.ThrowIfNull(airings);

        if (airings.Count is 0 || _airingSubscriptions.IsEmpty)
            return;

        foreach (var subscription in _airingSubscriptions.Values)
        {
            var matched = FilterAiringsForSubscriber(airings, subscription.Options);
            if (matched.Count is 0)
                continue;

            try
            {
                subscription.Handler(new EpisodeAiredEventArgs() { AiredAt = minute, Airings = matched });
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "The airing subscription {Subscriber} ({SubscriptionID}) threw while being handed {Count} airing(s) for {Minute:o}.",
                    subscription.Subscriber,
                    subscription.ID,
                    matched.Count,
                    minute
                );
            }
        }
    }

    /// <summary>
    /// The airings of one minute that one subscriber asked for. The horizon was
    /// built from the union of every subscription, so this narrows it back down
    /// with the same hard filters a read applies.
    /// </summary>
    /// <param name="airings">The minute's airings.</param>
    /// <param name="options">The subscriber's filters, or <c>null</c> for everything.</param>
    /// <returns>The airings this subscriber gets, in the order they came in.</returns>
    private List<IEpisodeAiring> FilterAiringsForSubscriber(IReadOnlyList<IEpisodeAiring> airings, EpisodeAiringFilteringOptions? options)
    {
        if (options is null)
            return [.. airings];

        // Its own context, because a schedule view's visible tracks depend on
        // whether the read includes disabled data, and the horizon's may not
        // have been built the way this subscriber asked.
        var context = new AiringReadContext(this, options.IncludeDisabled);
        var anchor = options.EntityAnchor is AiringEntityAnchor.Auto ? AiringEntityAnchor.Raw : options.EntityAnchor;
        var matched = new List<IEpisodeAiring>();
        foreach (var airing in airings)
        {
            if (airing is not EpisodeAiringView view)
                continue;
            if (!options.IncludeEstimates && view.IsEstimated)
                continue;
            if (anchor is AiringEntityAnchor.Shoko && view.ShokoEpisode is null)
                continue;
            if (!MatchesFilters(context, context.GetSchedule(view.ScheduleView.Row), options))
                continue;
            if (!PassesAiringFilters(context, view, options))
                continue;

            matched.Add(airing);
        }

        return matched;
    }

    /// <summary>
    /// A name for whoever subscribed, for the log line when their handler
    /// throws. A method group gives up the type that declares it, a lambda the
    /// type that closed over it, and a static lambda neither.
    /// </summary>
    /// <param name="handler">The subscriber's handler.</param>
    /// <returns>The subscriber's identity, as far as it can be worked out.</returns>
    private static string DescribeSubscriber(Action<EpisodeAiredEventArgs> handler)
        => handler.Target?.GetType().FullName ?? handler.Method.DeclaringType?.FullName ?? "an anonymous handler";

    /// <summary>
    /// One live subscription.
    /// </summary>
    /// <param name="ID">The subscription's own ID, which is also its key in the registry.</param>
    /// <param name="Handler">The handler to call.</param>
    /// <param name="Options">The subscriber's filters, or <c>null</c> for everything.</param>
    /// <param name="Subscriber">Who subscribed, as far as it could be worked out, for logging.</param>
    private sealed record AiringSubscription(Guid ID, Action<EpisodeAiredEventArgs> Handler, EpisodeAiringFilteringOptions? Options, string Subscriber);

    #endregion

    #region Add Parts

    /// <summary>
    /// Takes the airing schedule providers the plugins provide. Called once
    /// during start-up; later calls have no effect.
    /// </summary>
    /// <param name="providers">The airing schedule providers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="providers"/> is <c>null</c>.</exception>
    public void AddParts(IEnumerable<IAiringScheduleProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;

            logger.LogInformation("Initializing service.");

            var config = configurationProvider.Load();
            var order = config.Priority;
            var storedKinds = config.EnabledKinds;
            var storedIntervals = config.SweepIntervals;
            // Nothing has been configured yet, so the core's own providers start
            // enabled for everything they declare and at the front of the list.
            var seedDefaults = storedKinds.Count is 0;
            _providerInfos = providers
                .Where(provider => provider is not null)
                .Select(provider =>
                {
                    var providerType = provider.GetType();
                    var pluginInfo = pluginManager.GetPluginInfo(providerType.Assembly)!;
                    var id = GetID(providerType, pluginInfo);
                    var available = provider.AvailableKinds ?? AllKinds;
                    var enabledKinds = storedKinds.TryGetValue(id, out var stored)
                        // Trimming on load drops kinds the provider no longer declares.
                        ? stored.Where(available.Contains).ToHashSet()
                        : seedDefaults && typeof(CorePlugin) == pluginInfo.PluginType
                            ? available.ToHashSet()
                            : [];
                    var description = provider.Description?.CleanDescription() ?? string.Empty;
                    var configurationType = providerType.GetInterfaces()
                        .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAiringScheduleProvider<>))
                        ?.GetGenericArguments()[0];
                    var configurationInfo = configurationType is null ? null : configurationService.GetConfigurationInfo(configurationType);
                    // The user's value if there is one, else the provider's own
                    // suggestion, else the server's default, and never below the floor.
                    var sweepInterval = storedIntervals.TryGetValue(id, out var storedInterval)
                        ? storedInterval
                        : (provider as ISweepingAiringScheduleProvider)?.SuggestedSweepInterval ?? AiringScheduleServiceSettings.DefaultSweepInterval;
                    if (sweepInterval < AiringScheduleServiceSettings.MinimumSweepInterval)
                        sweepInterval = AiringScheduleServiceSettings.MinimumSweepInterval;
                    return new AiringScheduleProviderInfo()
                    {
                        ID = id,
                        Version = provider.Version,
                        Name = provider.Name,
                        Description = description,
                        Provider = provider,
                        ConfigurationInfo = configurationInfo,
                        PluginInfo = pluginInfo,
                        Icon = ChooseIcon(
                            PackageImageLoader.LoadIcon(
                                pluginInfo,
                                providerType.Assembly,
                                provider.EmbeddedIconResourceName,
                                IconKind(providerType),
                                applicationPaths,
                                logger
                            ),
                            pluginInfo.Icon
                        ),
                        Priority = -1,
                        EnabledKinds = enabledKinds,
                        SweepInterval = sweepInterval,
                    };
                })
                .OrderBy(info => order.IndexOf(info.ID) is -1)
                .ThenBy(info => order.IndexOf(info.ID))
                .ThenByDescending(info => typeof(CorePlugin) == info.PluginInfo.PluginType)
                .ThenBy(info => info.Name, StringComparer.Ordinal)
                .ThenBy(info => info.ID)
                .Select((info, priority) =>
                {
                    info.Priority = priority;
                    return info;
                })
                .ToDictionary(info => info.ID);

            _loaded = true;
        }

        UpdateProviders(false);

        // Parts are added while the plugins initialize, which is before the database is up, so the
        // repositories cannot be counted here.
        logger.LogInformation("Loaded {ProviderCount} providers.", _providerInfos.Count);
    }

    #endregion

    #region Providers

    /// <inheritdoc/>
    public IEnumerable<AiringScheduleProviderInfo> GetAvailableProviders(bool onlyEnabled = false)
        => _providerInfos.Values
            .Where(info => !onlyEnabled || info.Enabled)
            .OrderBy(info => info.Priority)
            .Select(Copy);

    /// <inheritdoc/>
    public IReadOnlyList<AiringScheduleProviderInfo> GetProviderInfo(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        return _providerInfos.Values
            .Where(info => info.PluginInfo.ID == plugin.ID)
            .OrderBy(info => info.Priority)
            .Select(Copy)
            .ToList();
    }

    /// <inheritdoc/>
    public AiringScheduleProviderInfo? GetProviderInfo(Guid providerID)
        => _providerInfos.TryGetValue(providerID, out var info) ? Copy(info) : null;

    /// <inheritdoc/>
    public AiringScheduleProviderInfo GetProviderInfo(IAiringScheduleProvider provider)
        => Copy(GetRegisteredProvider(provider, nameof(provider)));

    /// <inheritdoc/>
    public AiringScheduleProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IAiringScheduleProvider
    {
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        return _providerInfos.Values.FirstOrDefault(info => info.Provider is TProvider) is { } info
            ? Copy(info)
            : throw new ArgumentException($"Unregistered provider: '{typeof(TProvider).Name}'", nameof(TProvider));
    }

    /// <inheritdoc/>
    public void UpdateProviders(params AiringScheduleProviderInfo[] providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        UpdateProviders(true, providers);
    }

    /// <summary>
    /// Apply the wanted order and enabled kinds, persist them, and tell
    /// everyone what changed.
    /// </summary>
    /// <param name="fireEvent">Whether to raise <see cref="ProvidersUpdated"/> when something changed.</param>
    /// <param name="providers">The provider infos carrying the wanted state.</param>
    private void UpdateProviders(bool fireEvent, params AiringScheduleProviderInfo[] providers)
    {
        if (!_loaded)
            return;

        var existingProviders = _providerInfos.Values.OrderBy(info => info.Priority).Select(Copy).ToList();
        var kindsChanged = false;
        var intervalsChanged = false;
        foreach (var providerInfo in providers)
        {
            var wantedIndex = providerInfo.Priority;
            var existingIndex = existingProviders.FindIndex(p => ReferenceEquals(p.Provider, providerInfo.Provider));
            if (existingIndex is -1)
                continue;

            // Only kinds the provider actually declares can be enabled, as the hashing routes do.
            var available = providerInfo.Provider.AvailableKinds ?? AllKinds;
            var wantedKinds = (providerInfo.EnabledKinds ?? []).Where(available.Contains).ToHashSet();
            if (!wantedKinds.SetEquals(existingProviders[existingIndex].EnabledKinds))
            {
                existingProviders[existingIndex].EnabledKinds = wantedKinds;
                kindsChanged = true;
            }

            // The floor is the service's, so a caller asking for less gets the floor
            // rather than an error, the same way an undeclared kind is dropped.
            var wantedInterval = providerInfo.SweepInterval < AiringScheduleServiceSettings.MinimumSweepInterval
                ? AiringScheduleServiceSettings.MinimumSweepInterval
                : providerInfo.SweepInterval;
            if (wantedInterval != existingProviders[existingIndex].SweepInterval)
            {
                existingProviders[existingIndex].SweepInterval = wantedInterval;
                intervalsChanged = true;
            }

            if (wantedIndex != existingIndex)
            {
                var entry = existingProviders[existingIndex];
                existingProviders.RemoveAt(existingIndex);
                if (wantedIndex < 0)
                    existingProviders.Add(entry);
                else
                    existingProviders.Insert(Math.Min(wantedIndex, existingProviders.Count), entry);
            }
        }

        var changed = kindsChanged || intervalsChanged;
        var config = configurationProvider.Load();
        var priority = existingProviders.Select(info => info.ID).ToList();
        if (config.Priority.Count != priority.Count || config.Priority.Where((id, index) => priority[index] != id).Any())
        {
            config.Priority = priority;
            changed = true;
        }

        var enabledKinds = existingProviders
            .OrderBy(info => info.ID)
            .ToDictionary(info => info.ID, info => info.EnabledKinds.Order().ToList());
        if (config.EnabledKinds.Count != enabledKinds.Count ||
            !config.EnabledKinds.All(entry => enabledKinds.TryGetValue(entry.Key, out var kinds) && kinds.SequenceEqual(entry.Value.Order())))
        {
            config.EnabledKinds = enabledKinds;
            changed = true;
        }

        var sweepIntervals = existingProviders
            .OrderBy(info => info.ID)
            .ToDictionary(info => info.ID, info => info.SweepInterval);
        if (config.SweepIntervals.Count != sweepIntervals.Count ||
            !config.SweepIntervals.All(entry => sweepIntervals.TryGetValue(entry.Key, out var interval) && interval == entry.Value))
        {
            config.SweepIntervals = sweepIntervals;
            changed = true;
        }

        if (!changed)
            return;

        lock (_lock)
        {
            _providerInfos = existingProviders
                .Select(info =>
                {
                    info.Priority = priority.IndexOf(info.ID);
                    return info;
                })
                .ToDictionary(info => info.ID);
        }

        // A kind going on or off changes which tracks a schedule shows, and so
        // which samples its estimates learn from. Priority never does.
        if (kindsChanged)
            _profiles.Clear();

        configurationProvider.Save(config);
        if (fireEvent)
            ProvidersUpdated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// A copy of a provider info, so a caller can reorder or re-enable it
    /// without touching the registered entry.
    /// </summary>
    /// <param name="info">The registered info.</param>
    /// <returns>The copy.</returns>
    private static AiringScheduleProviderInfo Copy(AiringScheduleProviderInfo info)
        => new()
        {
            ID = info.ID,
            Version = info.Version,
            Name = info.Name,
            Description = info.Description,
            Provider = info.Provider,
            ConfigurationInfo = info.ConfigurationInfo,
            PluginInfo = info.PluginInfo,
            Icon = info.Icon,
            Priority = info.Priority,
            EnabledKinds = info.EnabledKinds.ToHashSet(),
            SweepInterval = info.SweepInterval,
        };

    /// <summary>
    /// The registered entry for a provider instance, checked by reference
    /// against what <see cref="AddParts"/> received so a forged info object
    /// can't stand in for one.
    /// </summary>
    /// <param name="provider">The provider instance.</param>
    /// <param name="paramName">The name of the argument the provider arrived as.</param>
    /// <returns>The registered provider info.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <c>null</c>.</exception>
    /// <exception cref="InvalidOperationException">Parts have not been added yet.</exception>
    /// <exception cref="ArgumentException">The provider isn't the registered instance.</exception>
    private AiringScheduleProviderInfo GetRegisteredProvider(IAiringScheduleProvider provider, string paramName)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        return _providerInfos.Values.FirstOrDefault(info => ReferenceEquals(info.Provider, provider))
            ?? throw new ArgumentException($"Unregistered provider: '{provider.GetType().Name}'", paramName);
    }

    /// <summary>
    /// The ID of a provider, derived from its type and the plugin supplying it.
    /// </summary>
    /// <param name="type">The provider's type.</param>
    /// <param name="pluginInfo">The plugin the provider came from.</param>
    /// <returns>The provider's ID.</returns>
    private static Guid GetID(Type type, LocalPluginInfo pluginInfo)
        => UuidUtility.GetV5($"AiringScheduleProvider={type.FullName!}", pluginInfo.ID);

    /// <summary>
    /// The image kind a provider's icon is stored under beside its plugin,
    /// named after the provider's type.
    /// </summary>
    /// <remarks>
    /// The dot keeps it apart from the plugin's icon and every source icon.
    /// </remarks>
    /// <param name="providerType">The provider's type.</param>
    /// <returns>The kind, e.g. <c>ExampleProvider.airing-icon</c>.</returns>
    internal static string IconKind(Type providerType)
        => $"{providerType.Name}.airing-icon";

    /// <summary>
    /// Chooses a provider's icon: the one it declared, else its plugin's when
    /// that is an SVG or a PNG, as every other icon is.
    /// </summary>
    /// <param name="declared">The icon the provider declared, if any.</param>
    /// <param name="pluginIcon">The plugin's icon, if any.</param>
    /// <returns>The icon, or <c>null</c> when there is none to show.</returns>
    internal static PackageImageInfo? ChooseIcon(PackageImageInfo? declared, PackageImageInfo? pluginIcon)
        => declared ?? (pluginIcon is { MimeType: "image/svg+xml" or "image/png" } ? pluginIcon : null);

    #endregion

    #region Channels

    /// <inheritdoc/>
    public IAiringChannel FindOrRegisterChannel(string name, AiringChannelType type, string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        if (AiringScheduleUtility.NormalizeChannelName(name).Length is 0)
            throw new ArgumentException("A channel needs a name.", nameof(name));

        // Find first, by own name then by alias, so a merged name lands on the
        // channel it was merged into.
        var country = AiringScheduleUtility.NormalizeCountryCode(countryCode);
        if (RepoFactory.AiringChannel.GetByName(name, type, country) is { } existing)
            return ToChannelView(existing);

        // A channel without a country is one whose country is unknown, so it is
        // this one, and a TV station takes the country.
        if (country is not null)
        {
            var fallbacks = GetChannelsWithoutCountry(name, type, country, useAliases: true);
            if (fallbacks.Count is 1)
                return ToChannelView(AiringChannelMerger.CanTakeCountry(fallbacks[0]) ? AdoptCountry(fallbacks[0], country) : fallbacks[0]);

            if (fallbacks.Count > 1)
                logger.LogWarning(
                    "Registering channel \"{Name}\" in country {CountryCode}, since {Count} channels without a country answer to the name: {Names}.",
                    name.Trim(),
                    country,
                    fallbacks.Count,
                    string.Join(", ", fallbacks.Select(channel => channel.Name))
                );
        }

        var row = new AiringChannel(name, type, country);
        RepoFactory.AiringChannel.Save(row);
        var channel = ToChannelView(row);
        ChannelRegistered?.Invoke(
            this,
            new AiringChannelEventArgs
            {
                Reason = UpdateReason.Added,
                Kind = AiringChannelChangeKind.Registered,
                Channel = channel,
                Actor = ActorContext.CurrentActor,
            }
        );
        return channel;
    }

    /// <inheritdoc/>
    public IAiringChannel? GetChannelByID(Guid channelID)
        => RepoFactory.AiringChannel.GetByChannelID(channelID) is { } row ? ToChannelView(row) : null;

    /// <inheritdoc/>
    public IAiringChannel? GetChannelByName(string nameOrAlias, AiringChannelType type, string? countryCode = null, bool useAliases = true)
    {
        ArgumentNullException.ThrowIfNull(nameOrAlias);

        var country = AiringScheduleUtility.NormalizeCountryCode(countryCode);
        if (RepoFactory.AiringChannel.GetByName(nameOrAlias, type, country, useAliases) is { } row)
            return ToChannelView(row);

        return country is not null && GetChannelsWithoutCountry(nameOrAlias, type, country, useAliases) is [var fallback]
            ? ToChannelView(fallback)
            : null;
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAiringChannel> GetAllChannels(AiringChannelType? type = null)
    {
        return RepoFactory.AiringChannel.GetAll()
            .Where(channel => type is null || channel.Type == type)
            .OrderBy(channel => channel.Type)
            .ThenBy(channel => channel.Name, StringComparer.Ordinal)
            .ThenBy(channel => channel.CountryCode, StringComparer.Ordinal)
            .Select(IAiringChannel (channel) => new AiringChannelView(channel))
            .ToList();
    }

    /// <inheritdoc/>
    public IReadOnlySet<Guid> HiddenChannelIDs
        => RepoFactory.AiringChannel.GetAll()
            .Where(channel => channel.IsHidden)
            .Select(channel => channel.ChannelID)
            .ToHashSet();

    /// <inheritdoc/>
    public IAiringChannel AddChannelAliases(IAiringChannel channel, IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(aliases);

        var row = GetChannelRow(channel, nameof(channel));
        var added = GetCheckedAliases(row, aliases, row.Aliases, nameof(aliases));
        if (added.Count is 0)
            return ToChannelView(row);

        var previousAliases = row.Aliases.ToList();
        row.Aliases = [.. row.Aliases, .. added];
        return SaveChannel(row, AiringChannelChangeKind.AliasesAdded, previousAliases);
    }

    /// <inheritdoc/>
    public IAiringChannel RemoveChannelAliases(IAiringChannel channel, IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(aliases);

        var row = GetChannelRow(channel, nameof(channel));
        var unwanted = aliases
            .Where(alias => alias is not null)
            .Select(AiringScheduleUtility.NormalizeChannelName)
            .ToHashSet(StringComparer.Ordinal);
        var remaining = row.Aliases
            .Where(alias => !unwanted.Contains(AiringScheduleUtility.NormalizeChannelName(alias)))
            .ToList();
        if (remaining.Count == row.Aliases.Count)
            return ToChannelView(row);

        var previousAliases = row.Aliases.ToList();
        row.Aliases = remaining;
        return SaveChannel(row, AiringChannelChangeKind.AliasesRemoved, previousAliases);
    }

    /// <inheritdoc/>
    public IAiringChannel SetChannelAliases(IAiringChannel channel, IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(aliases);

        var row = GetChannelRow(channel, nameof(channel));
        var wanted = GetCheckedAliases(row, aliases, [], nameof(aliases));
        if (wanted.SequenceEqual(row.Aliases, StringComparer.Ordinal))
            return ToChannelView(row);

        var previousAliases = row.Aliases.ToList();
        row.Aliases = wanted;
        return SaveChannel(row, AiringChannelChangeKind.AliasesSet, previousAliases);
    }

    /// <inheritdoc/>
    public IAiringChannel SetChannelHidden(IAiringChannel channel, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var row = GetChannelRow(channel, nameof(channel));
        if (row.IsHidden == hidden)
            return ToChannelView(row);

        row.IsHidden = hidden;
        RepoFactory.AiringChannel.Save(row);
        var view = ToChannelView(row);
        ChannelRegistered?.Invoke(
            this,
            new AiringChannelEventArgs
            {
                Reason = UpdateReason.Updated,
                Kind = AiringChannelChangeKind.HiddenChanged,
                Channel = view,
                PreviousIsHidden = !hidden,
                Actor = ActorContext.CurrentActor,
            }
        );
        return view;
    }

    /// <inheritdoc/>
    public IAiringChannel MergeChannels(IAiringChannel target, IEnumerable<IAiringChannel> sources)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sources);

        var targetRow = GetChannelRow(target, nameof(target));
        var sourceRows = new List<AiringChannel>();
        foreach (var source in sources)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(sources));

            var row = GetChannelRow(source, nameof(sources));
            if (row.AiringChannelID == targetRow.AiringChannelID)
                throw new ArgumentException($"The channel '{row.ChannelID}' can't be merged into itself.", nameof(sources));
            if (row.Type != targetRow.Type)
                throw new ArgumentException($"The channel '{row.ChannelID}' is of another type than the channel it is merged into.", nameof(sources));
            if (sourceRows.All(other => other.AiringChannelID != row.AiringChannelID))
                sourceRows.Add(row);
        }

        if (sourceRows.Count is 0)
            return ToChannelView(targetRow);

        return MergeChannelRows(targetRow, sourceRows, byScheduleMove: false);
    }

    /// <summary>
    /// Merges stored channels into another one and raises the events: the
    /// moved schedules as updated, the merged channels as removed and the
    /// target as updated.
    /// </summary>
    /// <param name="target">The channel to keep.</param>
    /// <param name="sources">The channels to merge into it, already checked.</param>
    /// <param name="byScheduleMove">
    /// Whether a provider moving a schedule caused it, rather than a call to
    /// <see cref="MergeChannels"/>.
    /// </param>
    /// <returns>The merged channel.</returns>
    private AiringChannelView MergeChannelRows(AiringChannel target, IReadOnlyList<AiringChannel> sources, bool byScheduleMove)
    {
        var kinds = byScheduleMove
            ? (AiringChannelChangeKind.CountryMoved, AiringChannelChangeKind.CountryMoved)
            : (AiringChannelChangeKind.Merged, AiringChannelChangeKind.MergedAway);

        // A TV station without a country takes the one its sources agree on.
        var countries = sources
            .Select(source => source.CountryCode)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var adoptedCountry = AiringChannelMerger.CanTakeCountry(target) && countries.Count is 1 ? countries[0] : null;
        var removed = sources.Select(source => ToChannelView(source)).ToList();
        var previousAliases = target.Aliases.ToList();

        // The channel a provider's schedule left was the same channel, so a hidden one stays hidden.
        var wasHidden = target.IsHidden;
        if (byScheduleMove && sources.Any(source => source.IsHidden))
            target.IsHidden = true;
        bool? previousIsHidden = target.IsHidden != wasHidden ? wasHidden : null;
        var moved = AiringChannelMerger.Merge(target, sources, configurationProvider, logger);
        logger.LogInformation(
            "Merged {Count} channel(s) into channel \"{Name}\" ({ChannelID}), moving {ScheduleCount} schedule(s).",
            sources.Count,
            target.Name,
            target.ChannelID,
            moved.Count
        );
        if (adoptedCountry is null)
            return RaiseChannelChanges(target, UpdateReason.Updated, kinds, removed, moved, previousAliases, null, previousIsHidden);

        var previousID = target.ChannelID;
        removed.Add(ToChannelView(CopyChannel(target)));
        var (channel, rekeyed, holderAliases) = AiringChannelMerger.AdoptCountry(target, adoptedCountry, configurationProvider, logger);
        LogAdoptedCountry(target, channel, adoptedCountry);

        // Re-keyed in place, the target is still the channel kept; otherwise it
        // was merged into the one in that country too.
        return channel == target
            ? RaiseChannelChanges(channel, UpdateReason.Added, kinds, removed, [.. moved, .. rekeyed], previousAliases, previousID, previousIsHidden)
            : RaiseChannelChanges(channel, UpdateReason.Updated, kinds, removed, [.. moved, .. rekeyed], holderAliases, null, null);
    }

    /// <summary>
    /// Gives a TV station without a country the country it was found in and
    /// raises the events: its old ID as removed, and the new one as added, or
    /// the channel it merged into as updated.
    /// </summary>
    /// <param name="row">The channel without a country.</param>
    /// <param name="countryCode">The normalised country to give it.</param>
    /// <returns>The channel now holding it.</returns>
    private AiringChannel AdoptCountry(AiringChannel row, string countryCode)
    {
        var removed = ToChannelView(CopyChannel(row));
        var (channel, moved, previousAliases) = AiringChannelMerger.AdoptCountry(row, countryCode, configurationProvider, logger);
        LogAdoptedCountry(row, channel, countryCode);
        RaiseChannelChanges(
            channel,
            channel == row ? UpdateReason.Added : UpdateReason.Updated,
            (AiringChannelChangeKind.CountryTaken, AiringChannelChangeKind.CountryTaken),
            [removed],
            moved,
            previousAliases,
            channel == row ? removed.ChannelID : null,
            null
        );
        return channel;
    }

    /// <summary>
    /// Logs a channel taking a country.
    /// </summary>
    /// <param name="row">The channel that had no country.</param>
    /// <param name="channel">The channel now holding it: the same one re-keyed, or the one it merged into.</param>
    /// <param name="countryCode">The country it took.</param>
    private void LogAdoptedCountry(AiringChannel row, AiringChannel channel, string countryCode)
    {
        if (channel == row)
        {
            logger.LogInformation(
                "Channel \"{Name}\" takes country {CountryCode}, and is now {ChannelID}.",
                row.Name,
                countryCode,
                channel.ChannelID
            );
            return;
        }

        logger.LogInformation(
            "Channel \"{Name}\" takes country {CountryCode} and merges into channel \"{OtherName}\" ({ChannelID}).",
            row.Name,
            countryCode,
            channel.Name,
            channel.ChannelID
        );
    }

    /// <summary>
    /// Raises the events for channels that changed: the schedules that moved as
    /// updated, the channel IDs that are gone as removed, and the channel kept
    /// with the channels merged into it and the aliases they gave it.
    /// </summary>
    /// <param name="channel">The channel kept.</param>
    /// <param name="reason">The reason to raise for the channel kept.</param>
    /// <param name="kinds">The kind for the channel kept, and the one for the IDs that are gone.</param>
    /// <param name="removed">The channels whose IDs are gone, as they were.</param>
    /// <param name="moved">The schedules that moved. Repeats collapse.</param>
    /// <param name="previousAliases">The aliases the channel kept had before.</param>
    /// <param name="previousChannelID">The ID the channel kept had before, if it changed. Every other removed channel was merged into it.</param>
    /// <param name="previousIsHidden">Whether the channel kept was hidden before, if that changed.</param>
    /// <returns>The channel kept.</returns>
    private AiringChannelView RaiseChannelChanges(
        AiringChannel channel,
        UpdateReason reason,
        (AiringChannelChangeKind Kept, AiringChannelChangeKind Removed) kinds,
        IReadOnlyList<AiringChannelView> removed,
        IReadOnlyList<AiringSchedule> moved,
        IReadOnlyList<string> previousAliases,
        Guid? previousChannelID,
        bool? previousIsHidden
    )
    {
        var schedules = moved.DistinctBy(schedule => schedule.AiringScheduleID).ToList();
        foreach (var schedule in schedules)
            InvalidateProfilesForSeries(schedule.SeriesSource, schedule.SeriesID);
        if (schedules.Count > 0)
        {
            var context = new AiringReadContext(this, includeDisabled: true);
            foreach (var schedule in schedules)
                ScheduleUpdated?.Invoke(this, new AiringScheduleEventArgs { Reason = UpdateReason.Updated, Schedule = context.GetSchedule(schedule) });
        }

        var actor = ActorContext.CurrentActor;
        foreach (var view in removed)
        {
            ChannelRegistered?.Invoke(
                this,
                new AiringChannelEventArgs
                {
                    Reason = UpdateReason.Removed,
                    Kind = kinds.Removed,
                    Channel = view,
                    TargetChannelID = channel.ChannelID,
                    Actor = actor,
                }
            );
        }

        var kept = ToChannelView(channel);
        var merged = removed
            .Where(view => view.ChannelID != previousChannelID)
            .ToList<IAiringChannel>();
        var knownAliases = previousAliases
            .Select(AiringScheduleUtility.NormalizeChannelName)
            .ToHashSet(StringComparer.Ordinal);
        var addedAliases = kept.Aliases
            .Where(alias => merged.Count > 0 && !knownAliases.Contains(AiringScheduleUtility.NormalizeChannelName(alias)))
            .ToList();
        ChannelRegistered?.Invoke(
            this,
            new AiringChannelEventArgs
            {
                Reason = reason,
                Kind = kinds.Kept,
                Channel = kept,
                PreviousChannelID = previousChannelID,
                PreviousAliases = previousAliases,
                PreviousIsHidden = previousIsHidden,
                MergedChannels = merged,
                AddedAliases = addedAliases,
                Actor = actor,
            }
        );
        return kept;
    }

    /// <summary>
    /// Gets the channels without a country that a name in a country falls back
    /// to: those of the type answering to it, own names before aliases. One
    /// whose own name a channel in another country has is left out, as it may
    /// be that country's.
    /// </summary>
    /// <param name="name">The name to look up.</param>
    /// <param name="type">The type of the channel.</param>
    /// <param name="countryCode">The normalised country asked for.</param>
    /// <param name="useAliases">Whether an alias may answer.</param>
    /// <returns>The best matches: one, none, or several when it is ambiguous.</returns>
    private static List<AiringChannel> GetChannelsWithoutCountry(string name, AiringChannelType type, string countryCode, bool useAliases)
    {
        var normalizedName = AiringScheduleUtility.NormalizeChannelName(name);
        var candidates = RepoFactory.AiringChannel.GetAllByName(name, type, null)
            .Where(channel => useAliases || channel.NormalizedName == normalizedName)
            .Where(channel => RepoFactory.AiringChannel.GetAllByNameInAnyCountry(channel.Name, type)
                .All(other => other.CountryCode is null || other.CountryCode == countryCode))
            .ToList();
        var ownNames = candidates.Where(channel => channel.NormalizedName == normalizedName).ToList();
        return ownNames.Count > 0 ? ownNames : candidates;
    }

    /// <summary>
    /// Copies a stored channel, so an event can carry it as it was before its
    /// key changed.
    /// </summary>
    /// <param name="row">The stored channel.</param>
    /// <returns>An unsaved copy.</returns>
    private static AiringChannel CopyChannel(AiringChannel row)
        => new()
        {
            AiringChannelID = row.AiringChannelID,
            ChannelID = row.ChannelID,
            Name = row.Name,
            CountryCode = row.CountryCode,
            NormalizedName = row.NormalizedName,
            Type = row.Type,
            Aliases = [.. row.Aliases],
            IsHidden = row.IsHidden,
            CreatedAt = row.CreatedAt,
        };

    /// <summary>
    /// Gets the stored row behind a channel. Aliases and merges live on the
    /// stored channel, so a channel that was never registered here, or whose
    /// row is gone, is a caller error rather than a no-op.
    /// </summary>
    /// <param name="channel">The channel.</param>
    /// <param name="paramName">The name of the argument the channel arrived in.</param>
    /// <returns>The stored channel.</returns>
    /// <exception cref="ArgumentException">The channel isn't registered.</exception>
    private static AiringChannel GetChannelRow(IAiringChannel channel, string paramName)
        => RepoFactory.AiringChannel.GetByChannelID(channel.ChannelID)
            ?? throw new ArgumentException(
                $"Unregistered channel: '{channel.ChannelID}'. Register it with {nameof(FindOrRegisterChannel)} first.",
                paramName
            );

    /// <summary>
    /// Checks aliases for a channel and returns the ones to keep: trimmed, each
    /// once, and never the channel's own name or one it already has.
    /// </summary>
    /// <param name="row">The channel the aliases are for.</param>
    /// <param name="aliases">The aliases to check.</param>
    /// <param name="existing">The aliases the channel keeps besides these.</param>
    /// <param name="paramName">The name of the argument the aliases arrived in.</param>
    /// <returns>The aliases to keep, in the order given.</returns>
    /// <exception cref="ChannelAliasConflictException">Another channel of the same type and country answers to an alias.</exception>
    private List<string> GetCheckedAliases(AiringChannel row, IEnumerable<string> aliases, IEnumerable<string> existing, string paramName)
    {
        var kept = new List<string>();
        var known = existing.Select(AiringScheduleUtility.NormalizeChannelName).ToHashSet(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (alias is null)
                continue;

            var normalizedAlias = AiringScheduleUtility.NormalizeChannelName(alias);
            // An alias equal to the channel's own name, or one it already has, is nothing to do.
            if (normalizedAlias.Length is 0 || string.Equals(normalizedAlias, row.NormalizedName, StringComparison.Ordinal) || !known.Add(normalizedAlias))
                continue;

            // One name, one answer: a caller asserting otherwise should hear about it.
            foreach (var other in RepoFactory.AiringChannel.GetAllByName(alias, row.Type, row.CountryCode))
            {
                if (other.AiringChannelID == row.AiringChannelID)
                    continue;

                throw new ChannelAliasConflictException(
                    alias.Trim(),
                    ToChannelView(other),
                    string.Equals(other.NormalizedName, normalizedAlias, StringComparison.Ordinal),
                    paramName
                );
            }

            kept.Add(alias.Trim());
        }

        return kept;
    }

    /// <summary>
    /// Saves a channel whose aliases changed and raises the event.
    /// </summary>
    /// <param name="row">The stored channel.</param>
    /// <param name="kind">How the aliases changed.</param>
    /// <param name="previousAliases">The aliases the channel had before.</param>
    /// <returns>The channel.</returns>
    private AiringChannelView SaveChannel(AiringChannel row, AiringChannelChangeKind kind, IReadOnlyList<string> previousAliases)
    {
        RepoFactory.AiringChannel.Save(row);
        var view = ToChannelView(row);
        ChannelRegistered?.Invoke(
            this,
            new AiringChannelEventArgs
            {
                Reason = UpdateReason.Updated,
                Kind = kind,
                Channel = view,
                PreviousAliases = previousAliases,
                Actor = ActorContext.CurrentActor,
            }
        );
        return view;
    }

    /// <summary>
    /// A stored channel as the service hands it out.
    /// </summary>
    /// <param name="row">The stored channel.</param>
    /// <returns>The channel.</returns>
    private static AiringChannelView ToChannelView(AiringChannel row)
        => new(row);

    #endregion

    #region Time Zones

    private static IReadOnlyList<TimeZoneInfo>? _timeZones;

    /// <summary>
    /// The shape a source that only knows an offset is accepted in.
    /// </summary>
    [GeneratedRegex(@"^(?<sign>[+-])(?<hours>[0-9]{1,2}):(?<minutes>[0-9]{2})$")]
    private static partial Regex OffsetRegex();

    /// <inheritdoc/>
    public IReadOnlyList<TimeZoneInfo> GetAvailableTimeZones()
    {
        if (_timeZones is not null)
            return _timeZones;

        // A Windows host lists Windows ids, which are converted here so the
        // database stays portable between hosts.
        var zones = new Dictionary<string, TimeZoneInfo>(StringComparer.Ordinal);
        foreach (var zone in TimeZoneInfo.GetSystemTimeZones())
        {
            if (zone.HasIanaId)
            {
                zones.TryAdd(zone.Id, zone);
                continue;
            }

            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaID) && TimeZoneInfo.TryFindSystemTimeZoneById(ianaID, out var ianaZone))
                zones.TryAdd(ianaZone.Id, ianaZone);
        }

        return _timeZones = zones.Values
            .OrderBy(zone => zone.BaseUtcOffset)
            .ThenBy(zone => zone.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <inheritdoc/>
    public bool TryGetTimeZone(string idOrOffset, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        ArgumentNullException.ThrowIfNull(idOrOffset);

        return TryResolveTimeZone(idOrOffset, out zone);
    }

    /// <summary>
    /// Resolve a stored or submitted time zone, which is either a system id in
    /// either shape or a fixed <c>±HH:MM</c> offset the framework knows nothing
    /// about.
    /// </summary>
    /// <param name="idOrOffset">The id or offset to resolve.</param>
    /// <param name="zone">The resolved zone.</param>
    /// <returns><c>true</c> when the value names a zone this host can build.</returns>
    internal static bool TryResolveTimeZone(string idOrOffset, [NotNullWhen(true)] out TimeZoneInfo? zone)
    {
        zone = null;
        if (string.IsNullOrWhiteSpace(idOrOffset))
            return false;

        var value = idOrOffset.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(value, out var systemZone))
        {
            // Prefer the IANA form of a Windows id, so the same zone resolves
            // to the same thing whatever host it was stored on.
            if (!systemZone.HasIanaId &&
                TimeZoneInfo.TryConvertWindowsIdToIanaId(systemZone.Id, out var ianaID) &&
                TimeZoneInfo.TryFindSystemTimeZoneById(ianaID, out var ianaZone))
                systemZone = ianaZone;

            zone = systemZone;
            return true;
        }

        if (OffsetRegex().Match(value) is not { Success: true } match)
            return false;

        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
        if (hours > 14 || minutes > 59)
            return false;

        var offset = new TimeSpan(hours, minutes, 0);
        if (match.Groups["sign"].Value is "-")
            offset = offset.Negate();

        var id = FormatOffset(offset);
        zone = TimeZoneInfo.CreateCustomTimeZone(id, offset, id, id);
        return true;
    }

    /// <summary>
    /// The id a submitted zone is stored under: its IANA id where it has one,
    /// the IANA form of a Windows id where it doesn't, and a fixed offset for a
    /// custom zone built from one.
    /// </summary>
    /// <param name="zone">The zone the provider resolved.</param>
    /// <returns>The id to store, or <c>null</c> when there is no zone.</returns>
    /// <exception cref="TimeZoneNotFoundException">The zone can't be normalised to an IANA id or an offset.</exception>
    internal static string? NormalizeTimeZone(TimeZoneInfo? zone)
    {
        if (zone is null)
            return null;
        if (zone.HasIanaId)
            return zone.Id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(zone.Id, out var ianaID))
            return ianaID;
        if (OffsetRegex().IsMatch(zone.Id))
            return zone.Id;
        if (!zone.SupportsDaylightSavingTime)
            return FormatOffset(zone.BaseUtcOffset);

        throw new TimeZoneNotFoundException($"The time zone \"{zone.Id}\" can't be normalised to an IANA id or a fixed offset.");
    }

    /// <summary>
    /// Format an offset the way a stored fixed zone is spelled.
    /// </summary>
    /// <param name="offset">The offset from UTC.</param>
    /// <returns>The <c>±HH:MM</c> form.</returns>
    private static string FormatOffset(TimeSpan offset)
        => $"{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration().Hours:D2}:{offset.Duration().Minutes:D2}";

    #endregion
}
