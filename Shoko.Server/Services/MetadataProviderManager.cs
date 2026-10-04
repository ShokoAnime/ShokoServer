using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Config;
using Shoko.Abstractions.Config.Services;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Plugin;
using Shoko.Abstractions.Plugin.Models;
using Shoko.Abstractions.Utilities;
using Shoko.Server.Plugin;
using Shoko.Server.Settings;

namespace Shoko.Server.Services;

public class MetadataProviderManager : IMetadataProviderManager, IMetadataProviderPauseState
{
    private List<ProviderEntry>? _metadataProviders;

    /// <summary>
    ///   A registered provider with the source it serves. Which
    ///   shapes it answers is the type it implements, so there is nothing to
    ///   work out beyond a cast.
    /// </summary>
    /// <param name="Info">The provider and everything a client needs to route to it.</param>
    /// <param name="Sources">The declared source, as a set of one, which is how the settings are read.</param>
    internal sealed record ProviderEntry(
        MetadataProviderInfo Info,
        FrozenSet<MetadataSource> Sources
    )
    {
        public IMetadataProvider Provider => Info.Provider;

        /// <summary>
        ///   Whether this provider can work out what an anime is by itself,
        ///   which is what auto-linking asks of it.
        /// </summary>
        public bool Links => Info.SupportsAutoLinking;

        /// <summary>
        ///   Whether this provider is currently allowed to answer. Re-read
        ///   whenever the settings change.
        /// </summary>
        public bool IsEnabled => Info.Enabled;

        /// <summary>
        ///   The entity types this provider may answer for on its source.
        /// </summary>
        public FrozenSet<MetadataEntityType> Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                Info.EnabledEntityTypes = value;
            }
        }

        private FrozenSet<MetadataEntityType> _enabled = FrozenSet<MetadataEntityType>.Empty;

        /// <summary>
        ///   Whether an entity type is turned on, on the provider's source or,
        ///   when one is given, on that source only.
        /// </summary>
        public bool Allows(MetadataEntityType entityType, MetadataSource? source = null)
            => (source is null || source == Info.Source) && Enabled.Contains(entityType);
    }

    private readonly IPluginManager _pluginManager;

    private readonly IConfigurationService _configurationService;

    private readonly ConfigurationProvider<MetadataServiceSettings> _configurationProvider;

    private readonly ILogger<MetadataProviderManager> _logger;

    private readonly IApplicationPaths _applicationPaths;

    public MetadataProviderManager(
        IApplicationPaths applicationPaths,
        IPluginManager pluginManager,
        IConfigurationService configurationService,
        ConfigurationProvider<MetadataServiceSettings> configurationProvider,
        ILogger<MetadataProviderManager> logger
    )
    {
        _applicationPaths = applicationPaths;
        _pluginManager = pluginManager;
        _configurationService = configurationService;
        _configurationProvider = configurationProvider;
        configurationProvider.Saved += (_, _) => ApplyProviderSettings();
        _logger = logger;
    }

    /// <summary>
    ///   The registered providers, core first, for the metadata service to
    ///   route through.
    /// </summary>
    internal IReadOnlyList<ProviderEntry> Entries => _metadataProviders ?? [];

    /// <inheritdoc />
    public event EventHandler? PausedProvidersChanged;

    private void OnProviderPausedChanged(object? sender, EventArgs eventArgs)
        => PausedProvidersChanged?.Invoke(this, EventArgs.Empty);

    /// <inheritdoc />
    public IReadOnlyList<Type> GetPausedProviderTypes()
        => [.. Entries.Where(entry => entry.Provider is IPausableMetadataProvider pausable && pausable.PauseStatus.IsPaused).Select(entry => entry.Provider.GetType())];

    #region Source Icons

    /// <summary>
    ///   The embedded resource of AniDB's icon, which the core serves without
    ///   a provider.
    /// </summary>
    internal const string AnidbIconResourceName = "Shoko.Server.Resources.Icons.anidb-icon.png";

    /// <summary>
    ///   The icons of the core's sources that have no provider.
    /// </summary>
    private readonly Dictionary<MetadataSource, PackageImageInfo> _coreSourceIcons = [];

    /// <inheritdoc />
    public PackageImageInfo? GetSourceIcon(MetadataSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _coreSourceIcons.TryGetValue(source, out var icon) ? icon : ChooseSourceIcon(MetadataProviders, source);
    }

    /// <summary>
    ///   Chooses a source's one icon among its providers': the first series
    ///   provider's, else the first movie provider's, in registration order.
    /// </summary>
    /// <param name="providers">The registered providers, in registration order.</param>
    /// <param name="source">The source.</param>
    /// <returns>The icon, or <c>null</c> when no provider of the source has one.</returns>
    internal static PackageImageInfo? ChooseSourceIcon(IEnumerable<MetadataProviderInfo> providers, MetadataSource source)
    {
        PackageImageInfo? movieIcon = null;
        foreach (var info in providers)
        {
            if (info.Source != source || info.Icon is not { } icon)
                continue;

            if (info.Provider is IMetadataSeriesProvider)
                return icon;

            movieIcon ??= icon;
        }

        return movieIcon;
    }

    /// <summary>
    ///   The icon a provider names, the series provider's name first when it
    ///   is both.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The embedded resource name, or <c>null</c> when it names none.</returns>
    private static string? DeclaredIcon(IMetadataProvider provider)
        => (provider as IMetadataSeriesProvider)?.EmbeddedIconResourceName ?? (provider as IMetadataMovieProvider)?.EmbeddedIconResourceName;

    /// <summary>
    ///   Loads a source's icon the way a plugin's own icon is loaded: a file
    ///   beside the plugin, or the embedded image extracted there, named
    ///   <c>&lt;source&gt;-icon</c>.
    /// </summary>
    /// <param name="pluginInfo">The plugin the icon ships with.</param>
    /// <param name="assembly">The assembly holding the embedded image.</param>
    /// <param name="resourceName">The embedded resource, if one is named.</param>
    /// <param name="source">The source.</param>
    /// <returns>The icon, or <c>null</c> when there is none or it is neither SVG nor PNG.</returns>
    private PackageImageInfo? LoadSourceIcon(LocalPluginInfo pluginInfo, Assembly assembly, string? resourceName, MetadataSource source)
        => PackageImageLoader.LoadIcon(pluginInfo, assembly, resourceName, SourceIconKind(source), _applicationPaths, _logger);

    /// <summary>
    ///   The image kind a source's icon is stored under beside its plugin.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The kind, e.g. <c>anilist-icon</c>.</returns>
    internal static string SourceIconKind(MetadataSource source)
        => $"{source.Value}-icon";

    #endregion

    #region Providers

    /// <inheritdoc />
    public IReadOnlyList<MetadataProviderInfo> MetadataProviders => [.. _metadataProviders?.Select(entry => entry.Info) ?? []];

    /// <summary>
    ///   Tells whether a provider's own type implements an optional member of
    ///   one of its interfaces, rather than leaving it to the interface's
    ///   default.
    /// </summary>
    /// <param name="providerType">The provider's type.</param>
    /// <param name="interfaceType">The interface declaring the member.</param>
    /// <param name="methodName">The member's name.</param>
    /// <returns>
    ///   <c>true</c> when the type implements the interface and
    ///   its own code answers the member.
    /// </returns>
    internal static bool Overrides(Type providerType, Type interfaceType, string methodName)
    {
        if (!interfaceType.IsAssignableFrom(providerType) || providerType.IsInterface)
            return false;

        var map = providerType.GetInterfaceMap(interfaceType);
        for (var index = 0; index < map.InterfaceMethods.Length; index++)
            if (map.InterfaceMethods[index].Name == methodName)
                return map.TargetMethods[index] is { } target && !target.DeclaringType!.IsInterface;

        return false;
    }

    /// <summary>
    ///   Tells why a provider may not be registered, if it may not: it
    ///   answers nothing, belongs to no loaded plugin, claims no source, or
    ///   claims a reserved source without being the core's.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="pluginInfo">The loaded plugin the provider's assembly belongs to, if any.</param>
    /// <param name="reserved">The sources only the core's own providers may claim.</param>
    /// <returns>The reason to drop the provider, or <c>null</c> to keep it.</returns>
    internal static string? RejectReason(IMetadataProvider provider, LocalPluginInfo? pluginInfo, IReadOnlySet<MetadataSource> reserved)
    {
        // A collection provider alone has nothing to collect; a matcher alone is
        // fine, as it records what it finds through the linking service.
        if (provider is not (IMetadataSeriesProvider or IMetadataMovieProvider or IMetadataAutoLinkingProvider))
            return $"it implements none of {nameof(IMetadataSeriesProvider)}, {nameof(IMetadataMovieProvider)} or {nameof(IMetadataAutoLinkingProvider)}, so there is nothing to ask it.";

        if (pluginInfo is null)
            return "it does not belong to a loaded plugin.";

        if (provider.Source is not { } source)
            return "it claimed no source.";

        // Only the core's own providers may claim a reserved source; everything in
        // the server assembly is attributed to the core plugin.
        if (typeof(CorePlugin) != pluginInfo.PluginType && reserved.Contains(source))
            return $"the core resolves {source} itself.";

        return null;
    }

    /// <summary>
    ///   Takes the metadata providers the plugins provide, dropping the claims
    ///   they are not entitled to. Called once during start-up; later calls
    ///   have no effect.
    /// </summary>
    /// <param name="metadataProviders">The metadata providers.</param>
    public void AddParts(IEnumerable<IMetadataProvider> metadataProviders)
    {
        if (_metadataProviders is not null)
            return;

        _metadataProviders = [];
        foreach (var provider in metadataProviders)
        {
            var providerType = provider.GetType();
            var loadedPlugin = _pluginManager.GetPluginInfo(providerType.Assembly);
            if (RejectReason(provider, loadedPlugin, CoreReservedSources) is { } reason)
            {
                _logger.LogWarning("Dropping metadata provider {Provider}: {Reason}", provider.Name, reason);
                continue;
            }

            // A provider outside a loaded plugin was dropped above.
            var pluginInfo = loadedPlugin!;
            var source = provider.Source;
            var sources = FrozenSet.ToFrozenSet([source]);
            IReadOnlySet<MetadataEntityType> availableEntityTypes = new HashSet<MetadataEntityType>(
                (provider is IMetadataSeriesProvider ? (MetadataEntityType[])[MetadataEntityType.Series, MetadataEntityType.Season, MetadataEntityType.Episode] : [])
                    .Concat(provider is IMetadataMovieProvider ? [MetadataEntityType.Movie] : [])
                    .Concat(provider is IMetadataCollectionProvider ? [MetadataEntityType.Collection] : [])
                    .Concat(provider is IMetadataEntityProvider entityProvider ? EntityKinds(entityProvider) : [])
            );
            var configurationType = providerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IMetadataProvider<>))
                ?.GetGenericArguments()[0];
            var info = new MetadataProviderInfo
            {
                ID = GetResolverID(providerType, pluginInfo),
                Version = provider.Version,
                Name = provider.Name,
                Description = provider.Description?.CleanDescription() ?? string.Empty,
                Provider = provider,
                ConfigurationInfo = configurationType is null ? null : _configurationService.GetConfigurationInfo(configurationType),
                PluginInfo = pluginInfo,
                SupportsSeries = provider is IMetadataSeriesProvider,
                SupportsMovies = provider is IMetadataMovieProvider,
                SupportsCollections = provider is IMetadataCollectionProvider,
                SupportsAutoLinking = provider is IMetadataAutoLinkingProvider,
                Source = source,
                MaxConcurrentJobs = provider.MaxConcurrentJobs is > 0 and var limit ? limit : null,
                SupportsPausing = provider is IPausableMetadataProvider,
                SupportsImages = provider is IMetadataImageProvider,
                SupportsLookup = Overrides(providerType, typeof(IMetadataSeriesLinkingProvider), nameof(IMetadataSeriesLinkingProvider.LookupSeries)) ||
                    Overrides(providerType, typeof(IMetadataMovieLinkingProvider), nameof(IMetadataMovieLinkingProvider.LookupMovie)),
                AvailableEntityTypes = availableEntityTypes,
                EnabledEntityTypes = FrozenSet<MetadataEntityType>.Empty,
                Icon = LoadSourceIcon(pluginInfo, providerType.Assembly, DeclaredIcon(provider), source),
            };
            _metadataProviders.Add(new(info, sources));
        }

        // AniDB has no provider, so the core names its icon here.
        if (_pluginManager.GetPluginInfo(typeof(CorePlugin).Assembly) is { } corePlugin &&
            LoadSourceIcon(corePlugin, typeof(CorePlugin).Assembly, AnidbIconResourceName, MetadataSource.AniDB) is { } anidbIcon)
            _coreSourceIcons[MetadataSource.AniDB] = anidbIcon;

        // Asked in this order, so the core's own answer before whichever
        // plugin happened to load first.
        _metadataProviders = [.. _metadataProviders
            .OrderByDescending(entry => typeof(CorePlugin) == entry.Info.PluginInfo.PluginType)
            .ThenBy(entry => entry.Info.Name, StringComparer.Ordinal)
            .ThenBy(entry => entry.Info.ID)];

        // An unassigned source and type goes to the first provider claiming it, the core's first; a null entry
        // was decided and stays. Saved, as a value an upgrade carried over is gone by the next start.
        var settings = _configurationProvider.Load();
        var carried = SettingsMigrations.ReadAutoLinkCarryOver(_applicationPaths.DataPath);
        var claims = _metadataProviders
            .Select(entry => new ProviderClaim(
                entry.Info.ID,
                entry.Info.Name,
                entry.Sources,
                entry.Info.AvailableEntityTypes,
                entry.Links,
                entry.Provider.AutoLinkByDefault,
                entry.Provider.AutoLinkRestrictedByDefault
            ))
            .ToList();
        var seeded = SeedDecisions(settings, claims, carried, out var autoLinkers);
        foreach (var (source, provider) in autoLinkers)
            _logger.LogInformation("Metadata provider {Provider} now auto-links {Source}, the first to claim it.", provider, source);

        seeded |= AdoptAndLogOrphanedAssignments(settings, claims);

        if (seeded)
            _configurationProvider.Save(settings);

        // Only once it is saved, so a boot that fails before this keeps it.
        SettingsMigrations.ClearAutoLinkCarryOver(_applicationPaths.DataPath);

        ApplyProviderSettings();

        // A provider's pause is read whether or not it is enabled, since a
        // job queued before it was turned off must still wait for it.
        foreach (var entry in _metadataProviders)
            if (entry.Provider is IPausableMetadataProvider pausable)
                pausable.PauseStatusChanged += OnProviderPausedChanged;
        PausedProvidersChanged?.Invoke(this, EventArgs.Empty);

        _logger.LogInformation(
            "Registered {Count} metadata providers: {Providers}.",
            _metadataProviders.Count,
            string.Join(", ", _metadataProviders.Select(entry => entry.Info.Name))
        );
    }

    /// <summary>
    ///   Fills in what was never decided about each source: every entity type
    ///   goes to the first provider claiming it, and auto-linking to the first
    ///   provider able to do it, whenever that one shows up.
    /// </summary>
    /// <remarks>
    ///   Anything already decided is kept: a provider new to an entity type
    ///   joins the end of its order, enabled only while something there is,
    ///   so it never takes over by itself. A source still waiting for a
    ///   linker says so in its settings. What an upgrade carried over is the
    ///   admin's old decision, so it wins over the default whether or not the
    ///   source is new. Older settings naming one provider per type are read
    ///   in first.
    /// </remarks>
    /// <param name="settings">The settings to update.</param>
    /// <param name="providers">Every registered provider and what it claims, in registration order.</param>
    /// <param name="carried">The auto-link decisions an upgrade carried over, by source.</param>
    /// <param name="autoLinkers">Each source handed an auto-linker here, with the provider's name.</param>
    /// <returns>Whether anything was written.</returns>
    internal static bool SeedDecisions(
        MetadataServiceSettings settings,
        IReadOnlyList<ProviderClaim> providers,
        IReadOnlyDictionary<MetadataSource, SettingsMigrations.AutoLinkCarryOver> carried,
        out List<(MetadataSource Source, string Provider)> autoLinkers
    )
    {
        autoLinkers = [];
        var seeded = ReadOlderAssignments(settings);
        var fresh = providers
            .SelectMany(provider => provider.Sources)
            .Where(source => !settings.Sources.ContainsKey(source))
            .ToHashSet();
        seeded |= fresh.Count > 0;
        foreach (var provider in providers)
        {
            foreach (var source in provider.Sources)
            {
                // The auto-linker goes once to the first provider able to link, even
                // one arriving after the source was first seen without one.
                var decisions = Of(settings.Sources, source);
                if (fresh.Remove(source))
                    decisions.AutoLinkerUnclaimed = true;

                if (provider.Links && decisions.AutoLinkerUnclaimed)
                {
                    decisions.AutoLinker = provider.ID;
                    decisions.AutoLink = provider.AutoLinkByDefault;
                    decisions.AutoLinkRestricted = provider.AutoLinkRestrictedByDefault;
                    decisions.AutoLinkerUnclaimed = false;
                    autoLinkers.Add((source, provider.Name));
                    seeded = true;
                }

                if (carried.TryGetValue(source, out var carry))
                {
                    decisions.AutoLink = carry.AutoLink ?? decisions.AutoLink;
                    decisions.AutoLinkRestricted = carry.AutoLinkRestricted ?? decisions.AutoLinkRestricted;
                    seeded = true;
                }

                foreach (var entityType in provider.EntityTypes)
                {
                    if (!decisions.Providers.TryGetValue(entityType, out var order))
                    {
                        decisions.Providers[entityType] = [new(provider.ID, true)];
                        seeded = true;
                        continue;
                    }

                    if (order.Any(slot => slot.ProviderID == provider.ID))
                        continue;

                    order.Add(new(provider.ID, order.Any(slot => slot.IsEnabled)));
                    seeded = true;
                }
            }
        }

        return seeded;
    }

    /// <summary>
    ///   Reads the one provider per entity type older settings kept into the
    ///   order, as its only enabled provider, or none for nobody.
    /// </summary>
    /// <param name="settings">The settings to update.</param>
    /// <returns>Whether anything was read.</returns>
    private static bool ReadOlderAssignments(MetadataServiceSettings settings)
    {
        var read = false;
        foreach (var decisions in settings.Sources.Values)
        {
            if (decisions.Enabled is not { } older)
                continue;

            foreach (var (entityType, providerID) in older)
                if (!decisions.Providers.ContainsKey(entityType))
                    decisions.Providers[entityType] = providerID is { } id ? [new(id, true)] : [];

            decisions.Enabled = null;
            read = true;
        }

        return read;
    }

    /// <summary>
    ///   Hands the assignments whose provider is gone to their heirs, and
    ///   logs each one.
    /// </summary>
    /// <param name="settings">The settings to update.</param>
    /// <param name="claims">Every registered provider and what it claims, in registration order.</param>
    /// <returns>Whether anything changed.</returns>
    private bool AdoptAndLogOrphanedAssignments(MetadataServiceSettings settings, IReadOnlyList<ProviderClaim> claims)
    {
        var adoptions = AdoptOrphanedAssignments(settings, claims, out var changed);
        foreach (var adoption in adoptions)
            _logger.LogInformation(
                "Handed {Source} {What} from the missing provider {Orphan} to {Provider}.",
                adoption.Source, adoption.EntityType?.ToString() ?? "auto-linking", adoption.Orphan, adoption.Heir
            );

        return changed;
    }

    /// <summary>
    ///   Drops the providers gone from each entity type's order, handing the
    ///   type to the next enabled one, or else the first claiming it, when
    ///   the one answering is gone; and hands auto-linking on the same way.
    /// </summary>
    /// <remarks>
    ///   An order nothing registered claims is kept whole, for its providers
    ///   to find on their return. One left with nothing enabled on purpose
    ///   stays that way. Auto-linking only goes to a provider able to do it.
    /// </remarks>
    /// <param name="settings">The settings to update.</param>
    /// <param name="providers">Every registered provider and what it claims, in registration order.</param>
    /// <param name="changed">Whether anything changed.</param>
    /// <returns>What was handed over, the entity type unset for auto-linking.</returns>
    internal static List<(MetadataSource Source, MetadataEntityType? EntityType, Guid Orphan, string Heir)> AdoptOrphanedAssignments(
        MetadataServiceSettings settings,
        IReadOnlyList<ProviderClaim> providers,
        out bool changed
    )
    {
        changed = false;
        var registered = providers.Select(provider => provider.ID).ToHashSet();
        var adoptions = new List<(MetadataSource, MetadataEntityType?, Guid, string)>();
        foreach (var (source, decisions) in settings.Sources)
        {
            var claimants = providers.Where(provider => provider.Sources.Contains(source)).ToList();
            foreach (var (entityType, order) in decisions.Providers)
            {
                var claiming = claimants.Where(provider => provider.EntityTypes.Contains(entityType)).ToList();
                if (claiming.Count is 0 || order.All(slot => claiming.Any(provider => provider.ID == slot.ProviderID)))
                    continue;

                var answering = order.FirstOrDefault(slot => slot.IsEnabled);
                order.RemoveAll(slot => !claiming.Any(provider => provider.ID == slot.ProviderID));
                changed = true;
                if (answering is null || claiming.Any(provider => provider.ID == answering.ProviderID))
                    continue;

                var heir = order.FindIndex(slot => slot.IsEnabled);
                if (heir < 0)
                {
                    if (order.Count is 0)
                        order.Add(new(claiming[0].ID, true));
                    else
                        order[0] = order[0] with { IsEnabled = true };
                    heir = 0;
                }

                adoptions.Add((source, entityType, answering.ProviderID, claiming.First(provider => provider.ID == order[heir].ProviderID).Name));
            }

            if (decisions.AutoLinker is { } orphanedLinker && !registered.Contains(orphanedLinker)
                && claimants.FirstOrDefault(provider => provider.Links) is { } linker)
            {
                decisions.AutoLinker = linker.ID;
                adoptions.Add((source, null, orphanedLinker, linker.Name));
                changed = true;
            }
        }

        return adoptions;
    }

    /// <summary>
    ///   What a registered provider claims, as far as seeding the settings and
    ///   handing it an orphaned assignment are concerned.
    /// </summary>
    /// <param name="ID">The provider's ID.</param>
    /// <param name="Name">The provider's name, for the log.</param>
    /// <param name="Sources">The sources it may serve.</param>
    /// <param name="EntityTypes">The entity types it can answer for.</param>
    /// <param name="Links">Whether it can auto-link.</param>
    /// <param name="AutoLinkByDefault">Whether it links on its own when it first takes a source's auto-linking.</param>
    /// <param name="AutoLinkRestrictedByDefault">Whether it links restricted entries when it first takes a source's auto-linking.</param>
    internal sealed record ProviderClaim(
        Guid ID,
        string Name,
        IReadOnlySet<MetadataSource> Sources,
        IReadOnlySet<MetadataEntityType> EntityTypes,
        bool Links,
        bool AutoLinkByDefault = true,
        bool AutoLinkRestrictedByDefault = false
    );

    /// <summary>
    ///   Re-reads which providers are turned on and brings the subscriptions
    ///   in line, subscribing to one that has just been enabled and dropping
    ///   one that has just been turned off.
    /// </summary>
    /// <remarks>
    ///   Called once the providers are registered and again whenever the
    ///   settings are saved, so toggling one takes effect without a restart.
    /// </remarks>
    public void ApplyProviderSettings()
    {
        if (_metadataProviders is not { Count: > 0 } providers)
            return;

        var settings = _configurationProvider.Load();

        foreach (var entry in providers)
        {
            var wasAnswering = entry.IsEnabled;
            entry.Enabled = Settle(entry);


            var answering = entry.IsEnabled;
            if (answering == wasAnswering)
                continue;

            if (answering)
                _logger.LogInformation("Metadata provider {Provider} is now answering for {Source}.", entry.Info.Name, entry.Info.Source);
            else
                _logger.LogInformation("Metadata provider {Provider} is no longer answering.", entry.Info.Name);
        }

        _enabledProviders = providers
            .Where(entry => entry.Enabled.Count > 0)
            .Select(entry => entry.Info.Source)
            .ToFrozenSet();

        // Auto-linking is only in effect while an enabled linking provider
        // accepts links for the source; the auto-linker itself need not be it.
        var stored = providers
            .Where(entry => (entry.Provider is IMetadataSeriesLinkingProvider && entry.Enabled.Contains(MetadataEntityType.Series))
                || (entry.Provider is IMetadataMovieLinkingProvider && entry.Enabled.Contains(MetadataEntityType.Movie)))
            .Select(entry => entry.Info.Source)
            .ToHashSet();
        foreach (var entry in providers)
        {
            var source = entry.Info.Source;
            var decisions = entry.Links && stored.Contains(source) && settings.Sources.TryGetValue(source, out var found) && found.AutoLinker == entry.Info.ID
                ? found
                : null;
            entry.Info.IsAutoLinker = decisions is not null;
            entry.Info.AutoLink = decisions?.AutoLink ?? false;
            entry.Info.AutoLinkRestricted = decisions?.AutoLinkRestricted ?? false;
        }

        // The entity types a provider answers for: those whose order it is the first enabled claimant of.
        FrozenSet<MetadataEntityType> Settle(ProviderEntry entry)
            => settings.Sources.TryGetValue(entry.Info.Source, out var decisions)
                ? decisions.Providers
                    .Where(pair => entry.Info.AvailableEntityTypes.Contains(pair.Key) && Answering(pair.Value, entry.Info.Source, pair.Key) == entry.Info.ID)
                    .Select(pair => pair.Key)
                    .ToFrozenSet()
                : FrozenSet<MetadataEntityType>.Empty;
    }

    /// <summary>
    ///   The provider answering in an order: the first enabled one that is
    ///   registered and claims the source and entity type.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The entity type.</param>
    /// <returns>The provider's ID, or <c>null</c> when none answers.</returns>
    private Guid? Answering(IEnumerable<MetadataProviderAssignment> order, MetadataSource source, MetadataEntityType entityType)
        => order.FirstOrDefault(slot => slot.IsEnabled && Claims(slot.ProviderID, source, entityType))?.ProviderID;

    /// <summary>
    ///   Whether a registered provider claims an entity type on a source.
    /// </summary>
    /// <param name="providerID">The provider's ID.</param>
    /// <param name="source">The source.</param>
    /// <param name="entityType">The entity type.</param>
    /// <returns><c>true</c> when it does.</returns>
    private bool Claims(Guid providerID, MetadataSource source, MetadataEntityType entityType)
        => Entry(providerID) is { } entry && entry.Sources.Contains(source) && entry.Info.AvailableEntityTypes.Contains(entityType);

    /// <summary>
    ///   Makes a provider answer for exactly the entity types asked for on its
    ///   source: first in each of their orders, and off in every other.
    /// </summary>
    /// <remarks>
    ///   A type left out is written as off rather than dropped, since deciding
    ///   against a provider is a decision and has to survive a restart.
    /// </remarks>
    /// <param name="decisions">The decisions about the provider's source.</param>
    /// <param name="entry">The provider.</param>
    /// <param name="wanted">The entity types asked for.</param>
    /// <returns><c>true</c> when anything changed.</returns>
    private bool Reassign(MetadataSourceSettings decisions, ProviderEntry entry, IReadOnlySet<MetadataEntityType> wanted)
    {
        var (source, id) = (entry.Info.Source, entry.Info.ID);
        var changed = false;
        foreach (var entityType in entry.Info.AvailableEntityTypes)
        {
            if (!decisions.Providers.TryGetValue(entityType, out var order))
                decisions.Providers[entityType] = order = [];

            var index = order.FindIndex(slot => slot.ProviderID == id);
            if (wanted.Contains(entityType))
            {
                if (Answering(order, source, entityType) == id)
                    continue;

                if (index >= 0)
                    order.RemoveAt(index);
                order.Insert(0, new(id, true));
                changed = true;
            }
            else if (index < 0)
            {
                order.Add(new(id, false));
                changed = true;
            }
            else if (order[index].IsEnabled)
            {
                order[index] = order[index] with { IsEnabled = false };
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    ///   The decisions about a source, starting an empty set of them for one
    ///   never decided about.
    /// </summary>
    private static MetadataSourceSettings Of(Dictionary<MetadataSource, MetadataSourceSettings> sources, MetadataSource source)
    {
        if (!sources.TryGetValue(source, out var decisions))
            sources[source] = decisions = new();

        return decisions;
    }

    /// <inheritdoc />
    public IReadOnlySet<MetadataSource> EnabledProviders => _enabledProviders;

    private FrozenSet<MetadataSource> _enabledProviders = FrozenSet<MetadataSource>.Empty;

    /// <inheritdoc />
    public IEnumerable<MetadataProviderInfo> GetAvailableProviders(bool enabledForSeries = false, bool enabledForMovies = false)
        => _metadataProviders?
            .Where(entry => (!enabledForSeries || entry.Allows(MetadataEntityType.Series) || entry.Allows(MetadataEntityType.Season) || entry.Allows(MetadataEntityType.Episode))
                && (!enabledForMovies || entry.Allows(MetadataEntityType.Movie)))
            .Select(entry => entry.Info) ?? [];

    /// <inheritdoc />
    public IEnumerable<MetadataProviderInfo> GetAvailableProviders(MetadataEntityType entityType, MetadataSource? source = null)
        => _metadataProviders?
            .Where(entry => entry.Allows(entityType, source))
            .Select(entry => entry.Info) ?? [];

    /// <inheritdoc />
    public IReadOnlyList<MetadataProviderInfo> GetProviderInfo(IPlugin plugin)
    {
        ArgumentNullException.ThrowIfNull(plugin);

        return [.. _metadataProviders?
            .Where(entry => entry.Info.PluginInfo.ID == plugin.ID)
            .Select(entry => entry.Info) ?? []];
    }

    /// <inheritdoc />
    public bool IsProviderEnabled(IMetadataProvider provider)
        => Entry(provider)?.IsEnabled ?? false;

    /// <inheritdoc />
    public bool IsProviderEnabled(Guid providerID)
        => Entry(providerID)?.IsEnabled ?? false;

    /// <inheritdoc />
    public MetadataProviderInfo? GetProviderInfo(Guid providerID)
        => Entry(providerID)?.Info;

    /// <inheritdoc />
    public MetadataProviderInfo GetProviderInfo(IMetadataProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return Entry(provider)?.Info
            ?? throw new ArgumentException($"Unregistered metadata provider: '{provider.GetType().Name}'", nameof(provider));
    }

    /// <inheritdoc />
    public MetadataProviderInfo GetProviderInfo<TProvider>() where TProvider : class, IMetadataProvider
        => GetProviderInfo(typeof(TProvider));

    /// <inheritdoc />
    public MetadataProviderInfo GetProviderInfo(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);

        return _metadataProviders?.FirstOrDefault(entry => entry.Provider.GetType() == providerType)?.Info
            ?? throw new ArgumentException($"Unregistered metadata provider: '{providerType.Name}'", nameof(providerType));
    }

    private ProviderEntry? Entry(IMetadataProvider provider)
        => _metadataProviders?.FirstOrDefault(entry => ReferenceEquals(entry.Provider, provider));

    private ProviderEntry? Entry(Guid providerID)
        => providerID == Guid.Empty ? null : _metadataProviders?.FirstOrDefault(entry => entry.Info.ID == providerID);

    /// <inheritdoc />
    public void SetProviderEnabled(IMetadataProvider provider, bool enabled)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Entry(provider) is { } entry)
            SetProviderEnabled(entry.Info.ID, enabled);
    }

    /// <inheritdoc />
    public void SetProviderEnabled(Guid providerID, bool enabled)
    {
        if (Entry(providerID) is not { } entry)
            return;

        // Everything it can do, or nothing. The precise form is there for
        // anyone who wants less.
        SetProviderEnabled(providerID, enabled ? entry.Info.AvailableEntityTypes : FrozenSet<MetadataEntityType>.Empty);
    }

    /// <inheritdoc />
    public void SetProviderEnabled(Guid providerID, IReadOnlySet<MetadataEntityType> enabled)
    {
        ArgumentNullException.ThrowIfNull(enabled);
        if (Entry(providerID) is not { } entry)
            return;

        var settings = _configurationProvider.Load();
        if (Reassign(Of(settings.Sources, entry.Info.Source), entry, enabled))
        {
            _configurationProvider.Save(settings);
            ApplyProviderSettings();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<MetadataProviderAssignment> GetProviderOrder(MetadataSource source, MetadataEntityType entityType)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(entityType);
        var claimants = Entries.Where(entry => entry.Sources.Contains(source) && entry.Info.AvailableEntityTypes.Contains(entityType)).ToList();
        if (claimants.Count is 0)
            return [];

        // A claimant not in the order yet, which seeding leaves none of, is shown last and off.
        var order = _configurationProvider.Load().Sources.GetValueOrDefault(source)?.Providers.GetValueOrDefault(entityType) ?? [];
        var listed = order
            .Where(slot => claimants.Any(entry => entry.Info.ID == slot.ProviderID))
            .DistinctBy(slot => slot.ProviderID)
            .ToList();
        listed.AddRange(claimants
            .Where(entry => !listed.Any(slot => slot.ProviderID == entry.Info.ID))
            .Select(entry => new MetadataProviderAssignment(entry.Info.ID, false)));
        return listed;
    }

    /// <inheritdoc />
    public void SetProviderOrder(MetadataSource source, IReadOnlyDictionary<MetadataEntityType, IReadOnlyList<MetadataProviderAssignment>> orders)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(orders);
        foreach (var (entityType, order) in orders)
        {
            ArgumentNullException.ThrowIfNull(order, nameof(orders));
            var seen = new HashSet<Guid>();
            foreach (var slot in order)
            {
                ArgumentNullException.ThrowIfNull(slot, nameof(orders));
                if (!seen.Add(slot.ProviderID))
                    throw new ArgumentException($"'{slot.ProviderID}' is given twice for {entityType}.", nameof(orders));
                if (!Claims(slot.ProviderID, source, entityType))
                    throw new ArgumentException($"'{slot.ProviderID}' does not answer for {entityType} on {source}.", nameof(orders));
            }
        }

        var settings = _configurationProvider.Load();
        var decisions = Of(settings.Sources, source);
        var changed = false;
        foreach (var (entityType, order) in orders)
        {
            var current = decisions.Providers.GetValueOrDefault(entityType) ?? [];
            var given = order.Select(slot => slot.ProviderID).ToHashSet();
            List<MetadataProviderAssignment> next = [.. order, .. current.Where(slot => !given.Contains(slot.ProviderID))];
            if (current.SequenceEqual(next))
                continue;

            decisions.Providers[entityType] = next;
            changed = true;
        }

        if (changed)
        {
            _configurationProvider.Save(settings);
            ApplyProviderSettings();
        }
    }

    /// <inheritdoc />
    public void SetProviderAutoLinker(MetadataSource source, Guid? providerID)
    {
        if (providerID is { } id && (Entry(id) is not { Links: true } entry || !entry.Sources.Contains(source)))
            throw new ArgumentException($"'{id}' cannot work out what an anime is from {source}.", nameof(providerID));

        // Choosing, nobody included, is a decision, so no linker claims the
        // source by itself afterwards.
        var settings = _configurationProvider.Load();
        var decisions = Of(settings.Sources, source);
        if (decisions.AutoLinker == providerID && !decisions.AutoLinkerUnclaimed)
            return;

        decisions.AutoLinker = providerID;
        decisions.AutoLinkerUnclaimed = false;
        _configurationProvider.Save(settings);
        ApplyProviderSettings();
    }

    /// <inheritdoc />
    public void SetProviderAutoLink(MetadataSource source, bool autoLink)
        => UpdateSource(source, autoLink, decisions => decisions.AutoLink, (decisions, value) => decisions.AutoLink = value);

    /// <inheritdoc />
    public void SetProviderAutoLinkRestricted(MetadataSource source, bool autoLinkRestricted)
        => UpdateSource(source, autoLinkRestricted, decisions => decisions.AutoLinkRestricted, (decisions, value) => decisions.AutoLinkRestricted = value);

    /// <summary>
    ///   Sets one decision about a source, saving only if it changed.
    /// </summary>
    private void UpdateSource<T>(MetadataSource source, T value, Func<MetadataSourceSettings, T> get, Action<MetadataSourceSettings, T> set)
    {
        var settings = _configurationProvider.Load();
        var decisions = Of(settings.Sources, source);
        if (EqualityComparer<T>.Default.Equals(get(decisions), value))
            return;

        set(decisions, value);
        _configurationProvider.Save(settings);
        ApplyProviderSettings();
    }

    /// <summary>
    ///   The creator, character, studio and network kinds a provider refreshes
    ///   on its own source, logging each pair of its scope that is dropped.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <returns>The kinds.</returns>
    private IReadOnlySet<MetadataEntityType> EntityKinds(IMetadataEntityProvider provider)
    {
        var (kinds, dropped) = MetadataEntityRefreshScheduler.GetKinds(provider);
        foreach (var (source, entityType) in dropped)
            _logger.LogWarning(
                "Metadata provider {Provider} cannot refresh {Source} {Kind} entries one at a time: "
                    + "only creators, characters, studios and networks of its own source are.",
                provider.Name,
                source,
                entityType
            );

        return kinds;
    }

    private static Guid GetResolverID(Type type, LocalPluginInfo pluginInfo)
        => UuidUtility.GetV5($"MetadataResolver={type.FullName!}", pluginInfo.ID);


    /// <summary>
    ///   The sources a plugin may not claim: the ones the core answers for
    ///   through providers of its own, plus those that record provenance
    ///   rather than name a provider at all.
    /// </summary>
    internal static readonly FrozenSet<MetadataSource> CoreReservedSources = FrozenSet.ToFrozenSet(
    [
        MetadataSource.Shoko,
        MetadataSource.AniDB,
        MetadataSource.User,
        MetadataSource.Generated,
    ]);

    /// <inheritdoc />
    public IReadOnlySet<MetadataSource> ReservedSources => CoreReservedSources;


    #endregion
}
