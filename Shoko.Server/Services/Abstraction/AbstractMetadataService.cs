using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Events;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Services;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;
using Shoko.Server.Repositories.Direct.TMDB.Optional;

#nullable enable
namespace Shoko.Server.Services.Abstraction;

public partial class AbstractMetadataService : IMetadataService
{
    private readonly AnimeGroupRepository _groupRepository;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AnimeEpisodeRepository _episodeRepository;

    private readonly CustomTagRepository _customTagRepository;

    private readonly CrossRef_CustomTagRepository _customTagXrefRepository;

    private readonly AniDB_AnimeRepository _anidbSeriesRepository;

    private readonly AniDB_EpisodeRepository _anidbEpisodeRepository;

    private readonly TMDB_CollectionRepository _tmdbCollectionRepository;

    private readonly TMDB_ShowRepository _tmdbSeriesRepository;

    private readonly TMDB_SeasonRepository _tmdbSeasonRepository;

    private readonly TMDB_AlternateOrdering_SeasonRepository _tmdbAlternateSeasonRepository;

    private readonly TMDB_EpisodeRepository _tmdbEpisodeRepository;

    private readonly TMDB_MovieRepository _tmdbMovieRepository;

    public AbstractMetadataService(
        AnimeGroupRepository groupRepository,
        AnimeSeriesRepository seriesRepository,
        AnimeEpisodeRepository episodeRepository,
        AniDB_AnimeRepository anidbSeriesRepository,
        AniDB_EpisodeRepository anidbEpisodeRepository,
        CustomTagRepository customTagRepository,
        CrossRef_CustomTagRepository xrefCustomTagRepository,
        TMDB_CollectionRepository tmdbCollectionRepository,
        TMDB_ShowRepository tmdbSeriesRepository,
        TMDB_SeasonRepository tmdbSeasonRepository,
        TMDB_AlternateOrdering_SeasonRepository tmdbAlternateSeasonRepository,
        TMDB_EpisodeRepository tmdbEpisodeRepository,
        TMDB_MovieRepository tmdbMovieRepository
    )
    {
        _groupRepository = groupRepository;
        _seriesRepository = seriesRepository;
        _episodeRepository = episodeRepository;
        _anidbSeriesRepository = anidbSeriesRepository;
        _anidbEpisodeRepository = anidbEpisodeRepository;
        _customTagRepository = customTagRepository;
        _customTagXrefRepository = xrefCustomTagRepository;
        _tmdbCollectionRepository = tmdbCollectionRepository;
        _tmdbSeriesRepository = tmdbSeriesRepository;
        _tmdbSeasonRepository = tmdbSeasonRepository;
        _tmdbAlternateSeasonRepository = tmdbAlternateSeasonRepository;
        _tmdbEpisodeRepository = tmdbEpisodeRepository;
        _tmdbMovieRepository = tmdbMovieRepository;

        ShokoEventHandler.Instance.SeriesUpdated += OnSeriesUpdated;
        ShokoEventHandler.Instance.SeasonUpdated += OnSeasonUpdated;
        ShokoEventHandler.Instance.EpisodeUpdated += OnEpisodeUpdated;
        ShokoEventHandler.Instance.MovieUpdated += OnMovieUpdated;
    }

    ~AbstractMetadataService()
    {
        ShokoEventHandler.Instance.SeriesUpdated -= OnSeriesUpdated;
        ShokoEventHandler.Instance.SeasonUpdated -= OnSeasonUpdated;
        ShokoEventHandler.Instance.EpisodeUpdated -= OnEpisodeUpdated;
        ShokoEventHandler.Instance.MovieUpdated -= OnMovieUpdated;
    }

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
    public IEnumerable<IMovie> GetAllMoviesForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => [],
            IMetadataService.ProviderName.AniDB => [],
            IMetadataService.ProviderName.TMDB => _tmdbMovieRepository.GetAll(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public IMovie? GetMovieByProviderID(int providerID, IMetadataService.ProviderName providerName)
        => providerID <= 0 ? null : providerName switch
        {
            IMetadataService.ProviderName.Shoko => null,
            IMetadataService.ProviderName.AniDB => null,
            IMetadataService.ProviderName.TMDB => _tmdbMovieRepository.GetByID(providerID),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
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
    public IEnumerable<IEpisode> GetAllEpisodesForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => _episodeRepository.GetAll(),
            IMetadataService.ProviderName.AniDB => _anidbEpisodeRepository.GetAll(),
            IMetadataService.ProviderName.TMDB => _tmdbEpisodeRepository.GetAll(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public IEpisode? GetEpisodeByProviderID(int providerID, IMetadataService.ProviderName providerName)
        => providerID <= 0 ? null : providerName switch
        {
            IMetadataService.ProviderName.Shoko => _episodeRepository.GetByID(providerID),
            IMetadataService.ProviderName.AniDB => _anidbEpisodeRepository.GetByID(providerID),
            IMetadataService.ProviderName.TMDB => _tmdbEpisodeRepository.GetByID(providerID),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
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

    internal const int SeasonIdHexLength = 24;

    [GeneratedRegex(@"^(?:[0-9]{1,23}|[a-f0-9]{24})$")]
    private partial Regex SeasonIdRegex();

    /// <inheritdoc />
    public IEnumerable<ISeason> GetAllSeasonsForProvider(IMetadataService.ProviderName providerName, bool includeAlternateSeasons = false)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => [],
            IMetadataService.ProviderName.AniDB => [],
            IMetadataService.ProviderName.TMDB => includeAlternateSeasons
                ? _tmdbSeasonRepository.GetAll().Cast<ISeason>().Concat(_tmdbAlternateSeasonRepository.GetAll())
                : _tmdbSeasonRepository.GetAll(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public ISeason? GetSeasonByProviderID(string providerID, IMetadataService.ProviderName providerName)
        => string.IsNullOrWhiteSpace(providerID) ? null : providerName switch
        {
            IMetadataService.ProviderName.Shoko => null,
            IMetadataService.ProviderName.AniDB => null,
            IMetadataService.ProviderName.TMDB => SeasonIdRegex().Match(providerID) is { Success: true } result
                ? providerID.Length == SeasonIdHexLength
                    ? _tmdbAlternateSeasonRepository.GetByTmdbEpisodeGroupID(providerID)
                    : _tmdbSeasonRepository.GetByTmdbSeasonID(int.Parse(providerID))
                : null,
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
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
    public IEnumerable<ISeries> GetAllSeriesForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => _seriesRepository.GetAll(),
            IMetadataService.ProviderName.AniDB => _anidbSeriesRepository.GetAll(),
            IMetadataService.ProviderName.TMDB => _tmdbSeriesRepository.GetAll(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public ISeries? GetSeriesByProviderID(int providerID, IMetadataService.ProviderName providerName)
        => providerID <= 0 ? null : providerName switch
        {
            IMetadataService.ProviderName.Shoko => _seriesRepository.GetByID(providerID),
            IMetadataService.ProviderName.AniDB => _anidbSeriesRepository.GetByID(providerID),
            IMetadataService.ProviderName.TMDB => _tmdbSeriesRepository.GetByID(providerID),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
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
    public IShokoTag CreateCustomTag(string name, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_customTagRepository.GetByTagName(name) is not null)
            throw new DuplicateNameException($"Tag with name '{name}' already exists.");

        var tag = new CustomTag
        {
            TagName = name,
            TagDescription = description ?? string.Empty,
        };
        _customTagRepository.Save(tag);
        return tag;
    }

    public IShokoTag UpdateCustomTag(IShokoTag tag, string? name = null, string? description = null)
    {
        if (tag is not (CustomTag or AnimeTag) || _customTagRepository.GetByID(tag.ID) is not { } customTag)
            throw new ArgumentException("Invalid tag supplied.", nameof(tag));

        var updated = false;
        if (!string.IsNullOrWhiteSpace(name))
        {
            customTag.TagName = name;
            updated = true;
        }
        if (!string.IsNullOrEmpty(description))
        {
            customTag.TagDescription = description;
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

        var xrefs = _customTagXrefRepository.GetByCustomTagID(tag.ID);
        _customTagRepository.Delete(tag.ID);
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
            .Select(x => x is CustomTag or AnimeTag ? _customTagRepository.GetByID(x.ID) as IShokoTag : null)
            .WhereNotNull()
            .ToList();
        if (tagList.Count != count)
            throw new ArgumentException("One or more invalid tags supplied.", nameof(tags));

        var existingTagIds = _customTagXrefRepository.GetByAnimeID(series.AnidbAnimeID);
        var toAdd = tagList
            .ExceptBy(existingTagIds.Select(xref => xref.CustomTagID), tag => tag.ID)
            .Select(tag => new CrossRef_CustomTag
            {
                CrossRefID = series.AnidbAnimeID,
                CustomTagID = tag.ID,
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
            .Select(x => x is CustomTag or AnimeTag ? _customTagRepository.GetByID(x.ID) as IShokoTag : null)
            .WhereNotNull()
            .ToList();
        if (tagList.Count != count)
            throw new ArgumentException("One or more invalid tags supplied.", nameof(tags));

        var existingTagIds = _customTagXrefRepository.GetByAnimeID(series.AnidbAnimeID);
        var toRemove = existingTagIds
            .IntersectBy(tagList.Select(tag => tag.ID), xref => xref.CustomTagID)
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

    #region Collection

    /// <inheritdoc />
    public IEnumerable<ICollection> GetAllCollectionsForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => _groupRepository.GetAll(),
            IMetadataService.ProviderName.AniDB => [],
            IMetadataService.ProviderName.TMDB => _tmdbCollectionRepository.GetAll(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public ICollection? GetCollectionByProviderID(int providerID, IMetadataService.ProviderName providerName)
        => providerID <= 0 ? null : providerName switch
        {
            IMetadataService.ProviderName.Shoko => _groupRepository.GetByID(providerID),
            IMetadataService.ProviderName.AniDB => null,
            IMetadataService.ProviderName.TMDB => _tmdbCollectionRepository.GetByTmdbCollectionID(providerID),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public IEnumerable<IShokoGroup> GetAllShokoGroups()
        => _groupRepository.GetAll();

    /// <inheritdoc />
    public IShokoGroup? GetShokoGroupByID(int groupID)
        => groupID <= 0 ? null : _groupRepository.GetByID(groupID);

    #endregion
}

