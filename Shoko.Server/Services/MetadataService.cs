using System;
using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Microsoft.Extensions.Logging;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Filtering;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Airing;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Events;
using Shoko.Abstractions.Metadata.Providers;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Storage;
using Shoko.Abstractions.User;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.AniDB.Embedded;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.CrossReference.Embedded;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.Airing;
using Shoko.Server.Repositories.Cached.AniDB;

namespace Shoko.Server.Services;

public class MetadataService : IMetadataService
{
    private List<IResourceResolver>? _resourceResolvers;

    private readonly IMetadataCrossReferenceStore _crossReferences;

    private readonly ILogger<MetadataService> _logger;

    private readonly ConcurrentDictionary<IWithResources, bool> _isResolving = new();

    private readonly AnimeGroupRepository _groupRepository;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AnimeEpisodeRepository _episodeRepository;

    private readonly CustomTagRepository _customTagRepository;

    private readonly CrossRef_CustomTagRepository _customTagXrefRepository;

    private readonly VideoLocalRepository _videoRepository;

    private readonly JMMUserRepository _userRepository;

    private readonly FilterPresetRepository _filterRepository;

    private readonly AiringChannelRepository _channelRepository;

    private readonly AniDB_AnimeRepository _anidbSeriesRepository;

    private readonly AniDB_EpisodeRepository _anidbEpisodeRepository;

    private readonly AniDB_CreatorRepository _anidbCreatorRepository;

    private readonly AniDB_CharacterRepository _anidbCharacterRepository;

    private readonly AniDB_TagRepository _anidbTagRepository;










    private readonly IMetadataSeriesStore _seriesStore;

    private readonly IMetadataMovieStore _movieStore;

    private readonly IMetadataCollectionStore _collectionStore;

    private readonly IMetadataPeopleStore _peopleStore;

    private readonly IMetadataTagStore _tagStore;

    private readonly IMetadataStudioStore _studioStore;

    private readonly MetadataOrderingService _orderings;

    private readonly Lazy<IMetadataProviderManager> _providerManager;

    public MetadataService(
        AnimeGroupRepository groupRepository,
        AnimeSeriesRepository seriesRepository,
        AnimeEpisodeRepository episodeRepository,
        VideoLocalRepository videoRepository,
        JMMUserRepository userRepository,
        FilterPresetRepository filterRepository,
        AiringChannelRepository channelRepository,
        AniDB_AnimeRepository anidbSeriesRepository,
        AniDB_EpisodeRepository anidbEpisodeRepository,
        AniDB_CreatorRepository anidbCreatorRepository,
        AniDB_CharacterRepository anidbCharacterRepository,
        AniDB_TagRepository anidbTagRepository,
        IMetadataSeriesStore seriesStore,
        IMetadataMovieStore movieStore,
        IMetadataCollectionStore collectionStore,
        IMetadataPeopleStore peopleStore,
        IMetadataTagStore tagStore,
        IMetadataStudioStore studioStore,
        MetadataOrderingService orderings,
        CustomTagRepository customTagRepository,
        CrossRef_CustomTagRepository xrefCustomTagRepository,
        IMetadataCrossReferenceStore crossReferences,
        Lazy<IMetadataProviderManager> providerManager,
        ILogger<MetadataService> logger
    )
    {
        _groupRepository = groupRepository;
        _seriesRepository = seriesRepository;
        _episodeRepository = episodeRepository;
        _videoRepository = videoRepository;
        _userRepository = userRepository;
        _filterRepository = filterRepository;
        _channelRepository = channelRepository;
        _anidbSeriesRepository = anidbSeriesRepository;
        _anidbEpisodeRepository = anidbEpisodeRepository;
        _anidbCreatorRepository = anidbCreatorRepository;
        _anidbCharacterRepository = anidbCharacterRepository;
        _anidbTagRepository = anidbTagRepository;
        _seriesStore = seriesStore;
        _movieStore = movieStore;
        _collectionStore = collectionStore;
        _peopleStore = peopleStore;
        _tagStore = tagStore;
        _studioStore = studioStore;
        _orderings = orderings;
        _customTagRepository = customTagRepository;
        _customTagXrefRepository = xrefCustomTagRepository;
        _crossReferences = crossReferences;
        _providerManager = providerManager;
        _logger = logger;
        (_coreLookups, _storedLookups) = BuildLookups();

        ShokoEventHandler.Instance.SeriesUpdated += OnSeriesUpdated;
        ShokoEventHandler.Instance.SeasonUpdated += OnSeasonUpdated;
        ShokoEventHandler.Instance.EpisodeUpdated += OnEpisodeUpdated;
        ShokoEventHandler.Instance.MovieUpdated += OnMovieUpdated;
    }

    ~MetadataService()
    {
        ShokoEventHandler.Instance.SeriesUpdated -= OnSeriesUpdated;
        ShokoEventHandler.Instance.SeasonUpdated -= OnSeasonUpdated;
        ShokoEventHandler.Instance.EpisodeUpdated -= OnEpisodeUpdated;
        ShokoEventHandler.Instance.MovieUpdated -= OnMovieUpdated;
    }

    #region Resource Providers

    public IReadOnlyList<IResourceResolver> ResourceResolvers => _resourceResolvers ?? [];

    /// <summary>
    ///   Takes the resource resolvers and the metadata resolvers the plugins
    ///   provide, in plugin load order. Each source and kind goes to the first
    ///   resolver claiming it; a pair on a core source, or one another
    ///   resolver already took, is refused and logged, and the resolver keeps
    ///   the rest. Called once during start-up; later calls have no effect.
    /// </summary>
    /// <param name="resourceResolvers">The resource resolvers.</param>
    /// <param name="metadataResolvers">The metadata resolvers, in plugin load order.</param>
    public void AddParts(IEnumerable<IResourceResolver> resourceResolvers, IEnumerable<IMetadataResolver> metadataResolvers)
    {
        if (_resourceResolvers is not null)
            return;

        _resourceResolvers = [.. resourceResolvers];
        foreach (var resolver in metadataResolvers)
        {
            MetadataEntityScope scope;
            try
            {
                scope = resolver.Scope;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refusing metadata resolver {Resolver}: reading its scope failed.", resolver.Name);
                continue;
            }

            if (scope is not { IsEmpty: false })
            {
                _logger.LogError("Refusing metadata resolver {Resolver}: it names no source or kind.", resolver.Name);
                continue;
            }

            var took = false;
            foreach (var (source, entityType) in scope)
            {
                if (source.IsCore || MetadataProviderManager.CoreReservedSources.Contains(source))
                {
                    _logger.LogError(
                        "Refusing metadata resolver {Resolver} for {Source}://{EntityType}: the core resolves every {Source} entry itself.",
                        resolver.Name, source.Value, entityType.Value, source.Value
                    );
                    continue;
                }

                if (_metadataResolversByKind.TryGetValue((source, entityType), out var existing))
                {
                    _logger.LogError(
                        "Refusing metadata resolver {Resolver} for {Source}://{EntityType}: resolver {ExistingResolver} already resolves it.",
                        resolver.Name, source.Value, entityType.Value, existing.Name
                    );
                    continue;
                }

                _metadataResolversByKind[(source, entityType)] = resolver;
                took = true;
            }

            if (took)
                _metadataResolvers.Add(resolver);
        }
    }

    /// <inheritdoc />
    public IEnumerable<Resource> GatherResourcesForEntity(IWithResources entity)
    {
        if (_resourceResolvers is null)
            return [];

        // Re-entrance (a provider or the Resources getter calling back in) gets
        // nothing instead of recursing.
        if (!_isResolving.TryAdd(entity, true))
            return [];

        try
        {
            return [.. _resourceResolvers.SelectMany(r => r.Resolve(entity))];
        }
        finally
        {
            _isResolving.TryRemove(entity, out _);
        }
    }

    #endregion

    #region Lookup

    /// <summary>
    ///   Finds one kind of entry of one source by its ID.
    /// </summary>
    /// <param name="id">The entry, always of the source and kind the lookup is kept under.</param>
    /// <returns>The entry, or <see langword="null"/> when there is none.</returns>
    private delegate IMetadata? EntryLookup(MetadataGuid id);

    /// <summary>
    ///   The lookups of the core's own sources, by source and then by kind. A
    ///   kind a core source has no lookup for names no entry.
    /// </summary>
    private readonly FrozenDictionary<MetadataSource, FrozenDictionary<MetadataEntityType, EntryLookup>> _coreLookups;

    /// <summary>
    ///   The lookups of every other source, by kind, from the metadata stores
    ///   and the ordering service.
    /// </summary>
    private readonly FrozenDictionary<MetadataEntityType, EntryLookup> _storedLookups;

    private readonly List<IMetadataResolver> _metadataResolvers = [];

    private readonly Dictionary<(MetadataSource Source, MetadataEntityType EntityType), IMetadataResolver> _metadataResolversByKind = [];

    /// <inheritdoc />
    public IReadOnlyList<IMetadataResolver> MetadataResolvers => _metadataResolvers;

    /// <inheritdoc />
    public IMetadata? GetEntry(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        if (id.Source.IsCore)
            return _coreLookups.TryGetValue(id.Source, out var coreLookups) && coreLookups.TryGetValue(id.EntityType, out var coreLookup) ? coreLookup(id) : null;

        // A plugin's resolver answers before the stores, which answer for
        // whatever it does not hold.
        if (_metadataResolversByKind.TryGetValue((id.Source, id.EntityType), out var resolver) && Resolve(resolver, id) is { } resolved)
            return resolved;

        return _storedLookups.TryGetValue(id.EntityType, out var storedLookup) ? storedLookup(id) : null;
    }

    /// <inheritdoc />
    public TMetadata? GetEntry<TMetadata>(MetadataGuid id) where TMetadata : class, IMetadata
    {
        ArgumentNullException.ThrowIfNull(id);

        // A plugin's own kind may be answered by a type that also is one of
        // the core's kinds, so only an ID of another core kind is refused.
        return EntryKind<TMetadata>.Value is { } entityType && entityType != id.EntityType && _mappedKinds.Contains(id.EntityType)
            ? null
            : GetEntry(id) as TMetadata;
    }

    /// <inheritdoc />
    public ISeries? GetSeries(MetadataGuid id)
        => GetEntryOfKind<ISeries>(id, MetadataEntityType.Series);

    /// <inheritdoc />
    public ISeason? GetSeason(MetadataGuid id)
        => GetEntryOfKind<ISeason>(id, MetadataEntityType.Season);

    /// <inheritdoc />
    public IEpisode? GetEpisode(MetadataGuid id)
        => GetEntryOfKind<IEpisode>(id, MetadataEntityType.Episode);

    /// <inheritdoc />
    public IMovie? GetMovie(MetadataGuid id)
        => GetEntryOfKind<IMovie>(id, MetadataEntityType.Movie);

    /// <inheritdoc />
    public ICollection? GetCollection(MetadataGuid id)
        => GetEntryOfKind<ICollection>(id, MetadataEntityType.Collection);

    /// <inheritdoc />
    public IReadOnlyList<ICollection> GetCollectionsWith(MetadataGuid member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (member.Source == MetadataSource.Shoko)
        {
            if (!member.TryGetNumericID<int>(out var localID) || localID <= 0)
                return [];

            int? groupID = null;
            if (member.EntityType == MetadataEntityType.Series)
                groupID = _seriesRepository.GetByID(localID)?.AnimeGroupID;
            else if (member.EntityType == MetadataEntityType.Collection)
                groupID = _groupRepository.GetByID(localID)?.AnimeGroupParentID;

            return groupID is { } id && _groupRepository.GetByID(id) is { } group ? [group] : [];
        }

        return member.Source.IsCore ? [] : _collectionStore.GetCollectionsWith(member);
    }

    /// <summary>
    ///   Looks up an entry of one kind, finding nothing for an ID of any other
    ///   kind.
    /// </summary>
    /// <typeparam name="TMetadata">The type of entry.</typeparam>
    /// <param name="id">The entry.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <returns>The entry, or <see langword="null"/> when nothing holds it or the ID names another kind.</returns>
    private TMetadata? GetEntryOfKind<TMetadata>(MetadataGuid id, MetadataEntityType entityType) where TMetadata : class, IMetadata
    {
        ArgumentNullException.ThrowIfNull(id);
        return id.EntityType == entityType ? GetEntry(id) as TMetadata : null;
    }

    /// <summary>
    ///   Asks a plugin's resolver for an entry, logging and answering nothing
    ///   when it throws.
    /// </summary>
    /// <param name="resolver">The resolver for the entry's source and kind.</param>
    /// <param name="id">The entry.</param>
    /// <returns>The entry, or <see langword="null"/> when the resolver has none.</returns>
    private IMetadata? Resolve(IMetadataResolver resolver, MetadataGuid id)
    {
        try
        {
            return resolver.GetEntry(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Metadata resolver {Resolver} failed to resolve {ID}.", resolver.Name, id);
            return null;
        }
    }

    #region Lookup | Routing

    /// <summary>
    ///   Builds the lookup of every kind of entry each source holds.
    /// </summary>
    /// <returns>The lookups of the core's sources, and those of every other source.</returns>
    private (FrozenDictionary<MetadataSource, FrozenDictionary<MetadataEntityType, EntryLookup>> Core, FrozenDictionary<MetadataEntityType, EntryLookup> Stored) BuildLookups()
    {
        var shoko = new Dictionary<MetadataEntityType, EntryLookup>
        {
            [MetadataEntityType.Series] = id => TryNumber(id, out var number) ? _seriesRepository.GetByID(number) : null,
            [MetadataEntityType.Season] = GetShokoSeason,
            [MetadataEntityType.Episode] = id => TryNumber(id, out var number) ? _episodeRepository.GetByID(number) : null,
            [MetadataEntityType.Collection] = id => TryNumber(id, out var number) ? _groupRepository.GetByID(number) : null,
            [MetadataEntityType.Video] = GetVideo,
            [MetadataEntityType.User] = id => TryNumber(id, out var number) ? _userRepository.GetByID(number) : null,
            [MetadataEntityType.Filter] = id => TryNumber(id, out var number) ? _filterRepository.GetByID(number) : null,
            [MetadataEntityType.Channel] = id => Guid.TryParse(id.ID, out var channelID) ? _channelRepository.GetByChannelID(channelID) : null,
            [MetadataEntityType.Ordering] = id => _orderings.GetOrdering(id),
        };

        // The users' own tags, orderings and ordering groups.
        var user = new Dictionary<MetadataEntityType, EntryLookup>
        {
            [MetadataEntityType.Tag] = id => TryNumber(id, out var number) ? _customTagRepository.GetByID(number) : null,
            [MetadataEntityType.Season] = id => _orderings.GetGroup(id),
            [MetadataEntityType.Ordering] = id => _orderings.GetOrdering(id),
        };

        var anidb = new Dictionary<MetadataEntityType, EntryLookup>
        {
            [MetadataEntityType.Series] = id => TryNumber(id, out var number) ? _anidbSeriesRepository.GetByAnimeID(number) : null,
            [MetadataEntityType.Season] = GetAnidbSeason,
            [MetadataEntityType.Episode] = id => TryNumber(id, out var number) ? _anidbEpisodeRepository.GetByEpisodeID(number) : null,
            [MetadataEntityType.Creator] = id => TryNumber(id, out var number) ? _anidbCreatorRepository.GetByCreatorID(number) : null,
            [MetadataEntityType.Character] = id => TryNumber(id, out var number) ? _anidbCharacterRepository.GetByCharacterID(number) : null,
            [MetadataEntityType.Studio] = id => TryNumber(id, out var number) && _anidbCreatorRepository.GetByCreatorID(number) is { } creator ? new AniDB_Studio(creator) : null,
            [MetadataEntityType.Tag] = id => TryNumber(id, out var number) ? _anidbTagRepository.GetByTagID(number) : null,
            [MetadataEntityType.Ordering] = id => _orderings.GetOrdering(id),
        };

        // Every other source keeps its entries in the stores, and its
        // orderings and their groups with the ordering service.
        var stored = new Dictionary<MetadataEntityType, EntryLookup>
        {
            [MetadataEntityType.Series] = id => _seriesStore.GetSeries(id),
            [MetadataEntityType.Season] = id => _seriesStore.GetSeason(id) ?? _orderings.GetGroup(id),
            [MetadataEntityType.Episode] = id => _seriesStore.GetEpisode(id),
            [MetadataEntityType.Movie] = id => _movieStore.GetMovie(id),
            [MetadataEntityType.Collection] = id => _collectionStore.GetCollection(id),
            [MetadataEntityType.Creator] = id => _peopleStore.GetCreator(id),
            [MetadataEntityType.Character] = id => _peopleStore.GetCharacter(id),
            [MetadataEntityType.Tag] = id => _tagStore.GetTag(id),
            [MetadataEntityType.Studio] = id => _studioStore.GetStudio(id),
            [MetadataEntityType.Network] = id => _studioStore.GetNetwork(id),
            [MetadataEntityType.Ordering] = id => _orderings.GetOrdering(id),
        };

        var core = new Dictionary<MetadataSource, FrozenDictionary<MetadataEntityType, EntryLookup>>
        {
            [MetadataSource.Shoko] = shoko.ToFrozenDictionary(),
            [MetadataSource.User] = user.ToFrozenDictionary(),
            [MetadataSource.AniDB] = anidb.ToFrozenDictionary(),
        };
        return (core.ToFrozenDictionary(), stored.ToFrozenDictionary());
    }

    /// <summary>
    ///   Reads the positive number an entry is keyed by, such as a Shoko row's
    ///   local ID or an AniDB ID.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <param name="number">The number, when the ID is one.</param>
    /// <returns><see langword="true"/> when the ID is a positive number.</returns>
    private static bool TryNumber(MetadataGuid id, out int number)
        => id.TryGetNumericID(out number) && number > 0;

    /// <summary>
    ///   A Shoko season, keyed by its series' local ID, episode type and number.
    /// </summary>
    /// <param name="id">The season.</param>
    /// <returns>The season, or <see langword="null"/> when there is none.</returns>
    private AnimeSeason? GetShokoSeason(MetadataGuid id)
        => ParseSeasonID(id.ID) is { } season && _seriesRepository.GetByID(season.ID) is { } series
            ? new AnimeSeason(series, season.Type, season.Number)
            : null;

    /// <summary>
    ///   An AniDB season, keyed by its anime's ID, episode type and number.
    /// </summary>
    /// <param name="id">The season.</param>
    /// <returns>The season, or <see langword="null"/> when there is none.</returns>
    private AniDB_Season? GetAnidbSeason(MetadataGuid id)
        => ParseSeasonID(id.ID) is { } season && _anidbSeriesRepository.GetByAnimeID(season.ID) is { } anime
            ? new AniDB_Season(anime, season.Type, season.Number)
            : null;

    /// <summary>
    ///   A video by its own ID, <c>&lt;ED2K&gt;+&lt;file size&gt;</c>, or by
    ///   the local ID that named it before.
    /// </summary>
    /// <param name="id">The video.</param>
    /// <returns>The video, or <see langword="null"/> when there is none.</returns>
    private VideoLocal? GetVideo(MetadataGuid id)
    {
        var separator = id.ID.LastIndexOf('+');
        if (separator < 0)
            return TryNumber(id, out var number) ? _videoRepository.GetByID(number) : null;

        var hash = id.ID[..separator];
        return hash.Length > 0 && long.TryParse(id.ID[(separator + 1)..], out var fileSize) && fileSize > 0
            ? _videoRepository.GetByEd2kAndSize(hash, fileSize)
            : null;
    }

    /// <summary>
    ///   Reads the ID the Shoko and AniDB seasons are keyed by: the series'
    ///   ID, the episode type and the season number, joined by colons.
    /// </summary>
    /// <param name="id">The season's ID.</param>
    /// <returns>The parts, or <see langword="null"/> when the ID is not one.</returns>
    private static (int ID, EpisodeType Type, int Number)? ParseSeasonID(string id)
        => id.Split(':') is not { Length: 3 } parts ||
            !int.TryParse(parts[0], out var seriesID) ||
            !Enum.TryParse<EpisodeType>(parts[1], true, out var episodeType) ||
            !int.TryParse(parts[2], out var seasonNumber)
                ? null
                : (seriesID, episodeType, seasonNumber);

    #endregion

    #region Lookup | Kinds

    /// <summary>
    ///   The one kind of entry a type of entry can be, worked out once for
    ///   each type, so a typed lookup of another kind finds nothing without
    ///   looking.
    /// </summary>
    /// <typeparam name="TMetadata">The type of entry.</typeparam>
    private static class EntryKind<TMetadata> where TMetadata : class, IMetadata
    {
        /// <summary>
        ///   The kind, or <see langword="null"/> when the type can be of any
        ///   kind or of several.
        /// </summary>
        public static readonly MetadataEntityType? Value = KindOf(typeof(TMetadata));
    }

    /// <summary>
    ///   The kind each entry interface stands for.
    /// </summary>
    private static readonly (Type Type, MetadataEntityType EntityType)[] _entryKinds =
    [
        (typeof(ISeries), MetadataEntityType.Series),
        (typeof(ISeason), MetadataEntityType.Season),
        (typeof(IEpisode), MetadataEntityType.Episode),
        (typeof(IMovie), MetadataEntityType.Movie),
        (typeof(ICollection), MetadataEntityType.Collection),
        (typeof(IOrdering), MetadataEntityType.Ordering),
        (typeof(ICreator), MetadataEntityType.Creator),
        (typeof(ICharacter), MetadataEntityType.Character),
        (typeof(IStudio), MetadataEntityType.Studio),
        (typeof(INetwork), MetadataEntityType.Network),
        (typeof(ITag), MetadataEntityType.Tag),
        (typeof(IVideo), MetadataEntityType.Video),
        (typeof(IUser), MetadataEntityType.User),
        (typeof(IFilterPreset), MetadataEntityType.Filter),
        (typeof(IAiringChannel), MetadataEntityType.Channel),
    ];

    /// <summary>
    ///   The kinds of <see cref="_entryKinds"/>, the only kinds a typed lookup
    ///   refuses without looking.
    /// </summary>
    private static readonly FrozenSet<MetadataEntityType> _mappedKinds = _entryKinds.Select(entry => entry.EntityType).ToFrozenSet();

    /// <summary>
    ///   The one kind of entry a type stands for.
    /// </summary>
    /// <param name="type">The type of entry.</param>
    /// <returns>The kind, or <see langword="null"/> when the type stands for none of them or for several.</returns>
    private static MetadataEntityType? KindOf(Type type)
    {
        MetadataEntityType? found = null;
        foreach (var (entryType, entityType) in _entryKinds)
        {
            if (!type.IsAssignableTo(entryType))
                continue;
            if (found is not null)
                return null;
            found = entityType;
        }

        return found;
    }

    #endregion

    #endregion

    #region Site URLs

    /// <summary>
    ///   The core's own answers for the pairs a plugin's resolver would answer
    ///   on any other source, since no resolver may take a core source.
    /// </summary>
    private static readonly FrozenDictionary<(MetadataSource Source, MetadataEntityType EntityType), Func<IMetadata, string?>> _coreSiteUrls =
        new Dictionary<(MetadataSource Source, MetadataEntityType EntityType), Func<IMetadata, string?>>
        {
            [(MetadataSource.AniDB, MetadataEntityType.Series)] = AnidbSiteUrls.ForEntry,
            [(MetadataSource.AniDB, MetadataEntityType.Episode)] = AnidbSiteUrls.ForEntry,
            [(MetadataSource.AniDB, MetadataEntityType.Creator)] = AnidbSiteUrls.ForEntry,
            [(MetadataSource.AniDB, MetadataEntityType.Character)] = AnidbSiteUrls.ForEntry,
            [(MetadataSource.AniDB, MetadataEntityType.Studio)] = AnidbSiteUrls.ForEntry,
        }.ToFrozenDictionary();

    /// <summary>
    ///   The registered providers, kept once there are any; their enabled
    ///   kinds are updated in place.
    /// </summary>
    private IReadOnlyList<MetadataProviderInfo>? _siteUrlProviders;

    /// <inheritdoc />
    public string? GetSiteUrl(IMetadata entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // The core's answers and a resolver's go first; one answering nothing
        // leaves the entry to the provider, as a lookup leaves it to the stores.
        var id = entry.ID;
        var pair = (id.Source, id.EntityType);
        if (_coreSiteUrls.TryGetValue(pair, out var core) && AskSiteUrl("The core", id, () => core(entry)) is { } coreUrl)
            return coreUrl;

        if (_metadataResolversByKind.TryGetValue(pair, out var resolver) && AskSiteUrl(resolver.Name, id, () => resolver.GetSiteUrl(entry)) is { } resolvedUrl)
            return resolvedUrl;

        foreach (var (provider, ask) in SiteUrlOwners(id))
            if (AskSiteUrl(provider.Name, id, () => ask(entry)) is { } url)
                return url;

        return null;
    }

    /// <inheritdoc />
    public string? GetSiteUrl(MetadataGuid id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return GetSiteUrl(GetEntry(id) ?? new BareEntry(id));
    }

    /// <summary>
    ///   The providers owning an entry's page, in the order to ask them: the
    ///   source's series providers for a series, season or episode, its movie
    ///   providers for a movie or collection, and for a creator, character,
    ///   studio or network its entity providers, then its series and movie
    ///   providers. Within each, the one assigned the kind comes first.
    /// </summary>
    /// <param name="id">The entry.</param>
    /// <returns>Each provider once, with how to ask it.</returns>
    private IEnumerable<(IMetadataProvider Provider, Func<IMetadata, string?> Ask)> SiteUrlOwners(MetadataGuid id)
    {
        var kind = id.EntityType;
        var isEntity = kind == MetadataEntityType.Creator || kind == MetadataEntityType.Character ||
            kind == MetadataEntityType.Studio || kind == MetadataEntityType.Network;
        var isMovie = kind == MetadataEntityType.Movie || kind == MetadataEntityType.Collection;
        var isSeries = kind == MetadataEntityType.Series || kind == MetadataEntityType.Season || kind == MetadataEntityType.Episode;
        if (!isEntity && !isMovie && !isSeries)
            yield break;

        var providers = _siteUrlProviders;
        if (providers is null)
        {
            providers = _providerManager.Value.MetadataProviders;
            if (providers.Count > 0)
                _siteUrlProviders = providers;
        }

        var asked = new HashSet<IMetadataProvider>(ReferenceEqualityComparer.Instance);
        if (isEntity)
            foreach (var provider in Owners<IMetadataEntityProvider>(providers, id, kind))
                if (asked.Add(provider))
                    yield return (provider, provider.GetSiteUrl);

        if (isSeries || isEntity)
            foreach (var provider in Owners<IMetadataSeriesProvider>(providers, id, kind))
                if (asked.Add(provider))
                    yield return (provider, provider.GetSiteUrl);

        if (isMovie || isEntity)
            foreach (var provider in Owners<IMetadataMovieProvider>(providers, id, kind))
                if (asked.Add(provider))
                    yield return (provider, provider.GetSiteUrl);
    }

    /// <summary>
    ///   The providers of one capability on an entry's source, the one
    ///   assigned the entry's kind first.
    /// </summary>
    /// <typeparam name="TProvider">The capability.</typeparam>
    /// <param name="providers">The registered providers.</param>
    /// <param name="id">The entry.</param>
    /// <param name="kind">The entry's kind.</param>
    /// <returns>The providers, in the order to ask them.</returns>
    private static IEnumerable<TProvider> Owners<TProvider>(IReadOnlyList<MetadataProviderInfo> providers, MetadataGuid id, MetadataEntityType kind)
        where TProvider : class, IMetadataProvider
        => providers
            .Where(info => info.Source == id.Source && info.Provider is TProvider)
            .OrderBy(info => info.EnabledEntityTypes.Contains(kind) ? 0 : 1)
            .Select(info => (TProvider)info.Provider);

    /// <summary>
    ///   Asks one owner for a page, logging and answering nothing when it
    ///   throws.
    /// </summary>
    /// <param name="owner">The owner's name, for the log.</param>
    /// <param name="id">The entry, for the log.</param>
    /// <param name="ask">Asks the owner.</param>
    /// <returns>The URL, or <see langword="null"/>.</returns>
    private string? AskSiteUrl(string owner, MetadataGuid id, Func<string?> ask)
    {
        try
        {
            return ask() is { Length: > 0 } url ? url : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Owner} failed to give the site URL of {ID}.", owner, id);
            return null;
        }
    }

    /// <summary>
    ///   An entry nothing holds, standing in for itself by its ID alone.
    /// </summary>
    /// <param name="ID">The entry's ID.</param>
    private sealed record BareEntry(MetadataGuid ID) : IMetadata;

    #endregion

    #region Movie

    /// <inheritdoc />
    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieAdded;

    /// <inheritdoc />
    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieUpdated;

    /// <inheritdoc />
    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieRemoved;

    private void OnMovieUpdated(object? sender, MovieInfoUpdatedEventArgs eventArgs)
    {
        switch (eventArgs.Reason)
        {
            case UpdateReason.Added:
                MovieAdded?.Invoke(this, eventArgs);
                break;
            case UpdateReason.Removed:
                MovieRemoved?.Invoke(this, eventArgs);
                break;
            default:
                MovieUpdated?.Invoke(this, eventArgs);
                break;
        }
    }


    /// <inheritdoc />
    public IEnumerable<IMovie> GetAllMoviesForSource(MetadataSource source)
        => source switch
        {
            _ when source.IsCore => [],
            _ => _movieStore.GetAllMovies(source),
        };

    #endregion

    #region Episode

    /// <inheritdoc />
    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeAdded;

    /// <inheritdoc />
    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeUpdated;

    /// <inheritdoc />
    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeRemoved;

    private void OnEpisodeUpdated(object? sender, EpisodeInfoUpdatedEventArgs eventArgs)
    {
        switch (eventArgs.Reason)
        {
            case UpdateReason.Added:
                EpisodeAdded?.Invoke(this, eventArgs);
                break;
            case UpdateReason.Removed:
                EpisodeRemoved?.Invoke(this, eventArgs);
                break;
            default:
                EpisodeUpdated?.Invoke(this, eventArgs);
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<IEpisode> GetAllEpisodesForSource(MetadataSource source)
        => source switch
        {
            _ when source == MetadataSource.Shoko => _episodeRepository.GetAll(),
            _ when source == MetadataSource.AniDB => _anidbEpisodeRepository.GetAll(),
            _ when source.IsCore => [],
            _ => _seriesStore.GetAllEpisodes(source),
        };

    /// <inheritdoc />
    public IEnumerable<IShokoEpisode> GetAllShokoEpisodes()
        => _episodeRepository.GetAll();

    /// <inheritdoc />
    public IShokoEpisode? GetShokoEpisodeByID(int episodeID)
        => episodeID <= 0 ? null : _episodeRepository.GetByID(episodeID);

    /// <inheritdoc />
    public IShokoEpisode? GetShokoEpisodeByAnidbID(int anidbEpisodeID)
        => anidbEpisodeID <= 0 ? null : _episodeRepository.GetByAniDBEpisodeID(anidbEpisodeID);

    #endregion

    #region Season

    /// <inheritdoc />
    public event EventHandler<SeasonInfoUpdatedEventArgs>? SeasonAdded;

    /// <inheritdoc />
    public event EventHandler<SeasonInfoUpdatedEventArgs>? SeasonUpdated;

    /// <inheritdoc />
    public event EventHandler<SeasonInfoUpdatedEventArgs>? SeasonRemoved;

    private void OnSeasonUpdated(object? sender, SeasonInfoUpdatedEventArgs eventArgs)
    {
        switch (eventArgs.Reason)
        {
            case UpdateReason.Added:
                SeasonAdded?.Invoke(this, eventArgs);
                break;
            case UpdateReason.Removed:
                SeasonRemoved?.Invoke(this, eventArgs);
                break;
            default:
                SeasonUpdated?.Invoke(this, eventArgs);
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<ISeason> GetAllSeasonsForSource(MetadataSource source, bool includeAlternateSeasons = false)
        => source switch
        {
            _ when source.IsCore => [],
            _ when includeAlternateSeasons => [.. _seriesStore.GetAllSeasons(source), .. _orderings.GetStoredOrderings(source).SelectMany(ordering => ordering.Seasons)],
            _ => _seriesStore.GetAllSeasons(source),
        };

    #endregion

    #region Series

    /// <inheritdoc />
    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesAdded;

    /// <inheritdoc />
    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesUpdated;

    /// <inheritdoc />
    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesRemoved;

    private void OnSeriesUpdated(object? sender, SeriesInfoUpdatedEventArgs eventArgs)
    {
        switch (eventArgs.Reason)
        {
            case UpdateReason.Added:
                SeriesAdded?.Invoke(this, eventArgs);
                break;
            case UpdateReason.Removed:
                SeriesRemoved?.Invoke(this, eventArgs);
                break;
            default:
                SeriesUpdated?.Invoke(this, eventArgs);
                break;
        }
    }

    /// <inheritdoc />
    public IEnumerable<ISeries> GetAllSeriesForSource(MetadataSource source)
        => source switch
        {
            _ when source == MetadataSource.Shoko => _seriesRepository.GetAll(),
            _ when source == MetadataSource.AniDB => _anidbSeriesRepository.GetAll(),
            _ when source.IsCore => [],
            _ => _seriesStore.GetAllSeries(source),
        };

    /// <inheritdoc />
    public IEnumerable<IShokoSeries> GetAllShokoSeries()
        => _seriesRepository.GetAll();

    /// <inheritdoc />
    public IShokoSeries? GetShokoSeriesByID(int seriesID)
        => seriesID <= 0 ? null : _seriesRepository.GetByID(seriesID);

    /// <inheritdoc />
    public IShokoSeries? GetShokoSeriesByAnidbID(int anidbSeriesID)
        => anidbSeriesID <= 0 ? null : _seriesRepository.GetByAnimeID(anidbSeriesID);

    #region Series | Custom Tags

    /// <inheritdoc />
    public IEnumerable<IShokoTag> GetAllCustomTags()
        => _customTagRepository.GetAll();

    /// <inheritdoc />
    public IShokoTag? GetCustomTagByID(int tagID)
        => tagID <= 0 ? null : _customTagRepository.GetByID(tagID);

    /// <inheritdoc />
    public IShokoTag CreateCustomTag(CustomTagData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(data.Name);
        if (_customTagRepository.GetByTagName(data.Name) is not null)
            throw new DuplicateNameException($"Tag with name '{data.Name}' already exists.");

        var tag = new CustomTag
        {
            TagName = data.Name,
            TagDescription = data.Overview ?? string.Empty,
        };
        _customTagRepository.Save(tag);
        return tag;
    }

    public IShokoTag UpdateCustomTag(IShokoTag tag, CustomTagUpdateData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (tag is not (CustomTag or AnimeTag) || _customTagRepository.GetByID(tag.LocalID) is not { } customTag)
            throw new ArgumentException("Invalid tag supplied.", nameof(tag));

        var updated = false;
        if (!string.IsNullOrWhiteSpace(data.Name))
        {
            if (_customTagRepository.GetByTagName(data.Name) is not null)
                throw new DuplicateNameException($"Tag with name '{data.Name}' already exists.");

            customTag.TagName = data.Name;
            updated = true;
        }
        if (data.Overview is not null)
        {
            customTag.TagDescription = data.Overview;
            updated = true;
        }
        if (updated)
            _customTagRepository.Save(customTag);

        return customTag;
    }

    /// <inheritdoc />
    public void DeleteCustomTag(IShokoTag tag)
    {
        if (tag is not (CustomTag or AnimeTag))
            throw new ArgumentException("Invalid tag supplied.", nameof(tag));

        var xrefs = _customTagXrefRepository.GetByCustomTagID(tag.LocalID);
        _customTagRepository.Delete(tag.LocalID);
        _customTagXrefRepository.Delete(xrefs);
    }

    /// <inheritdoc />
    public bool AddCustomTagsToSeries(IShokoSeries series, IEnumerable<IShokoTag> tags)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(tags);
        var tagList = tags.ToList();
        if (tagList.Count is 0)
            return true;

        var count = tagList.Count;
        tagList = tagList
            .Select(x => x is CustomTag or AnimeTag ? _customTagRepository.GetByID(x.LocalID) as IShokoTag : null)
            .WhereNotNull()
            .ToList();
        if (tagList.Count != count)
            throw new ArgumentException("One or more invalid tags supplied.", nameof(tags));

        var existingTagIds = _customTagXrefRepository.GetByAnimeID(series.AnidbAnimeID);
        var toAdd = tagList
            .ExceptBy(existingTagIds.Select(xref => xref.CustomTagID), tag => tag.LocalID)
            .Select(tag => new CrossRef_CustomTag
            {
                CrossRefID = series.AnidbAnimeID,
                CustomTagID = tag.LocalID,
            })
            .ToList();
        if (toAdd.Count is 0)
            return false;

        _customTagXrefRepository.Save(toAdd);
        return true;
    }

    /// <inheritdoc />
    public bool RemoveCustomTagsFromSeries(IShokoSeries series, IEnumerable<IShokoTag> tags)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(tags);
        var tagList = tags.ToList();
        if (tagList.Count is 0)
            return true;

        var count = tagList.Count;
        tagList = tagList
            .Select(x => x is CustomTag or AnimeTag ? _customTagRepository.GetByID(x.LocalID) as IShokoTag : null)
            .WhereNotNull()
            .ToList();
        if (tagList.Count != count)
            throw new ArgumentException("One or more invalid tags supplied.", nameof(tags));

        var existingTagIds = _customTagXrefRepository.GetByAnimeID(series.AnidbAnimeID);
        var toRemove = existingTagIds
            .IntersectBy(tagList.Select(tag => tag.LocalID), xref => xref.CustomTagID)
            .ToList();
        if (toRemove.Count is 0)
            return false;

        _customTagXrefRepository.Delete(toRemove);
        return true;
    }

    /// <inheritdoc />
    public bool ClearCustomTagsForSeries(IShokoSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);
        var existingTagIds = _customTagXrefRepository.GetByAnimeID(series.AnidbAnimeID);
        if (existingTagIds.Count is 0)
            return false;

        _customTagXrefRepository.Delete(existingTagIds);
        return true;
    }

    #endregion

    #endregion

    #region Linked Entries

    /// <summary>
    ///   The series an anime is linked to, read off the links themselves.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The linked series, from every source.</returns>
    internal IReadOnlyList<ISeries> GetLinkedSeries(int anidbAnimeID)
        => [.. GetSeriesCrossReferences(anidbAnimeID).Select(xref => xref.Provider).OfType<ISeries>()];

    /// <summary>
    ///   The movies an anime is linked to, read off the links themselves.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The linked movies, from every source.</returns>
    internal IReadOnlyList<IMovie> GetLinkedMovies(int anidbAnimeID)
        => [.. GetMovieCrossReferencesForSeries(anidbAnimeID).Select(xref => xref.Provider).OfType<IMovie>()];

    /// <summary>
    ///   The episodes an episode is linked to, read off the links themselves.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode.</param>
    /// <returns>The linked episodes, from every source.</returns>
    internal IReadOnlyList<IEpisode> GetLinkedEpisodes(int anidbEpisodeID)
        => [.. GetEpisodeCrossReferences(anidbEpisodeID).Select(xref => xref.Provider).OfType<IEpisode>()];

    /// <summary>
    ///   The movies an episode is linked to, read off the links themselves.
    /// </summary>
    /// <param name="anidbEpisodeID">The AniDB episode.</param>
    /// <returns>The linked movies, from every source.</returns>
    internal IReadOnlyList<IMovie> GetLinkedMoviesForEpisode(int anidbEpisodeID)
        => [.. GetMovieCrossReferences(anidbEpisodeID).Select(xref => xref.Provider).OfType<IMovie>()];

    /// <summary>
    ///   The seasons an anime's episodes are linked into, projected from the
    ///   episode links.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <returns>The linked seasons, from every source.</returns>
    internal IReadOnlyList<ISeason> GetLinkedSeasons(int anidbAnimeID)
        => [.. GetSeasonCrossReferences(anidbAnimeID).Select(xref => xref.Provider).OfType<ISeason>()];

    /// <summary>
    ///   The season links of a season, worked out from its episodes' links:
    ///   the seasons they point into on the AniDB side, or the links covering
    ///   the season itself on a provider's side.
    /// </summary>
    /// <param name="season">The season.</param>
    /// <param name="links">The episode links of the season's episodes.</param>
    /// <returns>One link per season reached, per anime.</returns>
    internal static IReadOnlyList<IMetadataSeasonCrossReference> GetSeasonCrossReferences(ISeason season, IEnumerable<IMetadataEpisodeCrossReference> links)
    {
        var projected = links
            .GroupBy(link => link.AnidbAnimeID)
            .SelectMany(group => MetadataSeasonCrossReference.Project(group.Key, group));
        var anidbSide = season.SeriesID.Source == MetadataSource.AniDB || season.SeriesID.Source == MetadataSource.Shoko;
        return [.. anidbSide ? projected : projected.Where(link => link.ProviderID == season.ID)];
    }

    #endregion

    #region Cross-References

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeriesCrossReference> GetSeriesCrossReferences(int anidbAnimeID, MetadataSource? source = null)
        => anidbAnimeID <= 0 ? [] : _crossReferences.GetSeriesLinks(anidbAnimeID, source);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferences(int anidbEpisodeID, MetadataSource? source = null)
        => anidbEpisodeID <= 0 ? [] : _crossReferences.GetMovieLinks(anidbEpisodeID, source);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> GetMovieCrossReferencesForSeries(int anidbAnimeID, MetadataSource? source = null)
        => anidbAnimeID <= 0 ? [] : _crossReferences.GetMovieLinksForSeries(anidbAnimeID, source);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferences(int anidbEpisodeID, MetadataSource? source = null)
        => anidbEpisodeID <= 0 ? [] : _crossReferences.GetEpisodeLinks(anidbEpisodeID, source);

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> GetEpisodeCrossReferencesForSeries(int anidbAnimeID, MetadataSource? source = null)
        => anidbAnimeID <= 0 ? [] : _crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source);

    /// <inheritdoc />
    /// <remarks>
    ///   Worked out from the episode links rather than kept: an episode names
    ///   the season it sits in, so the seasons an anime covers are whichever
    ///   ones its episodes point into.
    /// </remarks>
    public IReadOnlyList<IMetadataSeasonCrossReference> GetSeasonCrossReferences(int anidbAnimeID, MetadataSource? source = null)
        => anidbAnimeID <= 0 ? [] : MetadataSeasonCrossReference.Project(anidbAnimeID, _crossReferences.GetEpisodeLinksForSeries(anidbAnimeID, source));

    /// <inheritdoc />
    public IReadOnlyList<IMetadataCrossReference> GetCrossReferencesForProviderEntry(MetadataGuid entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        // A season has no links of its own, so they are projected from the episode
        // links recording it.
        var (source, entityType, providerID) = (entry.Source, entry.EntityType, entry.ID);
        if (entityType == MetadataEntityType.Season)
            return
            [
                .. _crossReferences.GetAllEpisodeLinks(source)
                    .Where(xref => xref.SeasonID is { } seasonID && seasonID.ID == providerID)
                    .GroupBy(xref => xref.AnidbAnimeID)
                    .SelectMany(group => MetadataSeasonCrossReference.Project(group.Key, group)),
            ];

        // Nothing links to the core's other sources, and every plugin source's
        // links are in the shared tables, whether its entries are stored or not.
        return source.IsCore ? [] : _crossReferences.GetLinksTo(entry);
    }

    #endregion

    #region Collection

    /// <inheritdoc />
    public IEnumerable<ICollection> GetAllCollectionsForSource(MetadataSource source)
        => source switch
        {
            _ when source == MetadataSource.Shoko => _groupRepository.GetAll(),
            _ when source.IsCore => [],
            _ => _collectionStore.GetAllCollections(source),
        };

    /// <inheritdoc />
    public IEnumerable<IShokoGroup> GetAllShokoGroups()
        => _groupRepository.GetAll();

    /// <inheritdoc />
    public IShokoGroup? GetShokoGroupByID(int groupID)
        => groupID <= 0 ? null : _groupRepository.GetByID(groupID);

    #endregion
}

