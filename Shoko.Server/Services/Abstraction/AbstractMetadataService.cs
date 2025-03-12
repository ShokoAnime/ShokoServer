using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Plugin.Abstractions.Events;
using Shoko.Plugin.Abstractions.Services;
using Shoko.Server.Repositories.Cached;
using Shoko.Server.Repositories.Cached.AniDB;
using Shoko.Server.Repositories.Cached.TMDB;

#nullable enable
namespace Shoko.Server.Services.Abstraction;

public class AbstractMetadataService : IMetadataService
{
    /// <inheritdoc />
    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeAdded;

    /// <inheritdoc />
    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeUpdated;

    /// <inheritdoc />
    public event EventHandler<EpisodeInfoUpdatedEventArgs>? EpisodeRemoved;

    /// <inheritdoc />
    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieAdded;

    /// <inheritdoc />
    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieUpdated;

    /// <inheritdoc />
    public event EventHandler<MovieInfoUpdatedEventArgs>? MovieRemoved;

    /// <inheritdoc />
    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesAdded;

    /// <inheritdoc />
    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesUpdated;

    /// <inheritdoc />
    public event EventHandler<SeriesInfoUpdatedEventArgs>? SeriesRemoved;

    private readonly AnimeGroupRepository _groupRepository;

    private readonly AnimeSeriesRepository _seriesRepository;

    private readonly AnimeEpisodeRepository _episodeRepository;

    private readonly AniDB_AnimeRepository _anidbSeriesRepository;

    private readonly AniDB_EpisodeRepository _anidbEpisodeRepository;

    private readonly TMDB_ShowRepository _tmdbSeriesRepository;

    private readonly TMDB_EpisodeRepository _tmdbEpisodeRepository;

    private readonly TMDB_MovieRepository _tmdbMovieRepository;

    public AbstractMetadataService(
        AnimeGroupRepository groupRepository,
        AnimeSeriesRepository seriesRepository,
        AnimeEpisodeRepository episodeRepository,
        AniDB_AnimeRepository anidbSeriesRepository,
        AniDB_EpisodeRepository anidbEpisodeRepository,
        TMDB_ShowRepository tmdbSeriesRepository,
        TMDB_EpisodeRepository tmdbEpisodeRepository,
        TMDB_MovieRepository tmdbMovieRepository
    )
    {
        _groupRepository = groupRepository;
        _seriesRepository = seriesRepository;
        _episodeRepository = episodeRepository;
        _anidbSeriesRepository = anidbSeriesRepository;
        _anidbEpisodeRepository = anidbEpisodeRepository;
        _tmdbSeriesRepository = tmdbSeriesRepository;
        _tmdbEpisodeRepository = tmdbEpisodeRepository;
        _tmdbMovieRepository = tmdbMovieRepository;

        ShokoEventHandler.Instance.EpisodeUpdated += OnEpisodeUpdated;
        ShokoEventHandler.Instance.MovieUpdated += OnMovieUpdated;
        ShokoEventHandler.Instance.SeriesUpdated += OnSeriesUpdated;
    }

    ~AbstractMetadataService()
    {
        ShokoEventHandler.Instance.EpisodeUpdated -= OnEpisodeUpdated;
        ShokoEventHandler.Instance.MovieUpdated -= OnMovieUpdated;
        ShokoEventHandler.Instance.SeriesUpdated -= OnSeriesUpdated;
    }

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
    public IEnumerable<IEpisode> GetAllEpisodesForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => _episodeRepository.GetAll().AsQueryable(),
            IMetadataService.ProviderName.AniDB => _anidbEpisodeRepository.GetAll().AsQueryable(),
            IMetadataService.ProviderName.TMDB => _tmdbEpisodeRepository.GetAll().AsQueryable(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public IEnumerable<IMovie> GetAllMoviesForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.TMDB => _tmdbMovieRepository.GetAll().AsQueryable(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public IEnumerable<ISeries> GetAllSeriesForProvider(IMetadataService.ProviderName providerName)
        => providerName switch
        {
            IMetadataService.ProviderName.Shoko => _seriesRepository.GetAll().AsQueryable(),
            IMetadataService.ProviderName.AniDB => _anidbSeriesRepository.GetAll().AsQueryable(),
            IMetadataService.ProviderName.TMDB => _tmdbSeriesRepository.GetAll().AsQueryable(),
            _ => throw new ArgumentOutOfRangeException(nameof(providerName), providerName, null),
        };

    /// <inheritdoc />
    public IEnumerable<IShokoEpisode> GetAllShokoEpisodes()
        => _episodeRepository.GetAll();

    /// <inheritdoc />
    public IEnumerable<IShokoGroup> GetAllShokoGroups()
        => _groupRepository.GetAll();

    /// <inheritdoc />
    public IEnumerable<IShokoSeries> GetAllShokoSeries()
        => _seriesRepository.GetAll();

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
    public IMovie? GetMovieByProviderID(int providerID, IMetadataService.ProviderName providerName)
        => providerID <= 0 ? null : providerName switch
        {
            IMetadataService.ProviderName.TMDB => _tmdbMovieRepository.GetByID(providerID),
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
    public IShokoEpisode? GetShokoEpisodeByAnidbID(int anidbEpisodeID)
        => anidbEpisodeID <= 0 ? null : _episodeRepository.GetByAniDBEpisodeID(anidbEpisodeID);

    /// <inheritdoc />
    public IShokoEpisode? GetShokoEpisodeByID(int episodeID)
        => episodeID <= 0 ? null : _episodeRepository.GetByID(episodeID);

    /// <inheritdoc />
    public IShokoGroup? GetShokoGroupByID(int groupID)
        => groupID <= 0 ? null : _groupRepository.GetByID(groupID);

    /// <inheritdoc />
    public IShokoSeries? GetShokoSeriesByAnidbID(int anidbSeriesID)
        => anidbSeriesID <= 0 ? null : _seriesRepository.GetByAnimeID(anidbSeriesID);

    /// <inheritdoc />
    public IShokoSeries? GetShokoSeriesByID(int seriesID)
        => seriesID <= 0 ? null : _seriesRepository.GetByID(seriesID);
}

