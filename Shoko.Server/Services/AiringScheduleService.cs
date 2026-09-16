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
using Namotion.Reflection;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.QueueProcessor.Abstractions;
using Shoko.Server.Models.Airing;
using Shoko.Server.Plugin;
using Shoko.Server.Repositories;
using Shoko.Server.Scheduling.Jobs.Airing;
using Shoko.Server.Services.Airing;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;
using Shoko.Server.Utilities.Airing;

#nullable enable
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
    IQueueScheduler schedulerFactory,
    ConfigurationProvider<AiringScheduleServiceSettings> configurationProvider
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

    private Dictionary<Guid, AiringScheduleProviderInfo> _providerInfos = [];

    private List<IAiringScheduleEntityResolver> _resolvers = [];

    private readonly ConcurrentDictionary<int, AiringScheduleProfile> _profiles = [];

    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _refreshLimits = [];

    private readonly ConcurrentDictionary<string, List<TaskCompletionSource<AiringScheduleProviderRefresh>>> _refreshWaiters = [];

    private Dictionary<Guid, int>? _airingIDs;

    private bool _loaded;

    /// <summary>
    /// The registered entity resolvers, in registration order.
    /// </summary>
    internal IReadOnlyList<IAiringScheduleEntityResolver> EntityResolvers => _resolvers;

    /// <inheritdoc/>
    public event EventHandler? ProvidersUpdated;

    /// <inheritdoc/>
    public event EventHandler<AiringChannelEventArgs>? ChannelRegistered;

    /// <inheritdoc/>
    public event EventHandler<AiringScheduleEventArgs>? ScheduleUpdated;

    /// <inheritdoc/>
    public event EventHandler<EpisodeAiringsUpdatedEventArgs>? AiringsUpdated;

    #region Add Parts

    /// <inheritdoc/>
    public void AddParts(IEnumerable<IAiringScheduleProvider> providers, IEnumerable<IAiringScheduleEntityResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(resolvers);

        if (_loaded) return;
        lock (_lock)
        {
            if (_loaded) return;

            logger.LogInformation("Initializing service.");
            _resolvers = resolvers.Where(resolver => resolver is not null).ToList();

            var config = configurationProvider.Load();
            var order = config.Priority;
            var storedKinds = config.EnabledKinds;
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
                    return new AiringScheduleProviderInfo()
                    {
                        ID = id,
                        Version = provider.Version,
                        Name = provider.Name,
                        Description = description,
                        Provider = provider,
                        ConfigurationInfo = configurationInfo,
                        PluginInfo = pluginInfo,
                        Priority = -1,
                        EnabledKinds = enabledKinds,
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

        logger.LogInformation(
            "Loaded {ProviderCount} providers and {ResolverCount} entity resolvers, with {ScheduleCount} schedules holding {AiringCount} airings across {ChannelCount} channels.",
            _providerInfos.Count,
            _resolvers.Count,
            RepoFactory.AiringSchedule.GetAll().Count,
            RepoFactory.EpisodeAiring.GetAll().Count,
            RepoFactory.AiringChannel.GetAll().Count
        );
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

        var changed = kindsChanged;
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
            Priority = info.Priority,
            EnabledKinds = info.EnabledKinds.ToHashSet(),
        };

    /// <summary>
    /// The registered entry for a provider instance, checked by reference
    /// against what <see cref="AddParts"/> received so a forged info object
    /// can't stand in for one.
    /// </summary>
    /// <param name="provider">The provider instance.</param>
    /// <param name="paramName">The name of the argument the provider arrived as.</param>
    /// <returns>The registered provider info.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
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

    #endregion

    #region Channels

    /// <inheritdoc/>
    public IAiringChannel FindOrRegisterChannel(string name, AiringChannelType type)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!_loaded)
            throw new InvalidOperationException("Parts have not been added yet.");

        var normalizedName = AiringScheduleUtility.NormalizeChannelName(name);
        if (normalizedName.Length is 0)
            throw new ArgumentException("A channel needs a name.", nameof(name));

        var channelID = AiringScheduleUtility.GetChannelID(name, type);
        if (RepoFactory.AiringChannel.GetByChannelID(channelID) is { } existing)
            return existing;

        // Registering claims the name: an alias is a hint, a channel called that
        // is the real claim, so any alias holding it is dropped with a warning.
        foreach (var other in RepoFactory.AiringChannel.GetAllByName(name, type))
        {
            if (string.Equals(other.NormalizedName, normalizedName, StringComparison.Ordinal))
                continue;

            logger.LogWarning(
                "Registering channel \"{Name}\" takes the name from channel \"{OtherName}\", which held it as an alias.",
                name.Trim(),
                other.Name
            );
            other.Aliases = other.Aliases
                .Where(alias => !string.Equals(AiringScheduleUtility.NormalizeChannelName(alias), normalizedName, StringComparison.Ordinal))
                .ToList();
            RepoFactory.AiringChannel.Save(other);
        }

        var channel = new AiringChannel(name, type);
        RepoFactory.AiringChannel.Save(channel);
        ChannelRegistered?.Invoke(this, new AiringChannelEventArgs { Reason = UpdateReason.Added, Channel = channel });
        return channel;
    }

    /// <inheritdoc/>
    public IAiringChannel? GetChannelByID(Guid channelID)
        => RepoFactory.AiringChannel.GetByChannelID(channelID);

    /// <inheritdoc/>
    public IAiringChannel? GetChannelByName(string nameOrAlias, AiringChannelType type, bool useAliases = true)
    {
        ArgumentNullException.ThrowIfNull(nameOrAlias);

        return RepoFactory.AiringChannel.GetByName(nameOrAlias, type, useAliases);
    }

    /// <inheritdoc/>
    public IReadOnlyList<IAiringChannel> GetAllChannels(AiringChannelType? type = null)
        => RepoFactory.AiringChannel.GetAll()
            .Where(channel => type is null || channel.Type == type)
            .OrderBy(channel => channel.Type)
            .ThenBy(channel => channel.Name, StringComparer.Ordinal)
            .Select(IAiringChannel (channel) => channel)
            .ToList();

    /// <inheritdoc/>
    public IAiringChannel AddChannelAliases(IAiringChannel channel, IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(aliases);

        var row = RepoFactory.AiringChannel.GetByChannelID(channel.ID)
            ?? throw new ArgumentException($"Unregistered channel: '{channel.ID}'", nameof(channel));
        var added = new List<string>();
        var known = row.Aliases.Select(AiringScheduleUtility.NormalizeChannelName).ToHashSet(StringComparer.Ordinal);
        foreach (var alias in aliases)
        {
            if (alias is null)
                continue;

            var normalizedAlias = AiringScheduleUtility.NormalizeChannelName(alias);
            // An alias equal to the channel's own name, or one it already has, is nothing to do.
            if (normalizedAlias.Length is 0 || string.Equals(normalizedAlias, row.NormalizedName, StringComparison.Ordinal) || !known.Add(normalizedAlias))
                continue;

            // One name, one answer: a caller asserting otherwise should hear about it.
            foreach (var other in RepoFactory.AiringChannel.GetAllByName(alias, row.Type))
            {
                if (other.AiringChannelID == row.AiringChannelID)
                    continue;

                throw new ChannelAliasConflictException(
                    alias.Trim(),
                    other,
                    string.Equals(other.NormalizedName, normalizedAlias, StringComparison.Ordinal),
                    nameof(aliases)
                );
            }

            added.Add(alias.Trim());
        }

        if (added.Count is 0)
            return row;

        row.Aliases = [.. row.Aliases, .. added];
        RepoFactory.AiringChannel.Save(row);
        ChannelRegistered?.Invoke(this, new AiringChannelEventArgs { Reason = UpdateReason.Updated, Channel = row });
        return row;
    }

    /// <inheritdoc/>
    public IAiringChannel RemoveChannelAliases(IAiringChannel channel, IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(aliases);

        var row = RepoFactory.AiringChannel.GetByChannelID(channel.ID)
            ?? throw new ArgumentException($"Unregistered channel: '{channel.ID}'", nameof(channel));
        var unwanted = aliases
            .Where(alias => alias is not null)
            .Select(AiringScheduleUtility.NormalizeChannelName)
            .ToHashSet(StringComparer.Ordinal);
        var remaining = row.Aliases
            .Where(alias => !unwanted.Contains(AiringScheduleUtility.NormalizeChannelName(alias)))
            .ToList();
        if (remaining.Count == row.Aliases.Count)
            return row;

        row.Aliases = remaining;
        RepoFactory.AiringChannel.Save(row);
        ChannelRegistered?.Invoke(this, new AiringChannelEventArgs { Reason = UpdateReason.Updated, Channel = row });
        return row;
    }

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
    /// <returns><see langword="true"/> when the value names a zone this host can build.</returns>
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
    /// <returns>The id to store, or <see langword="null"/> when there is no zone.</returns>
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
