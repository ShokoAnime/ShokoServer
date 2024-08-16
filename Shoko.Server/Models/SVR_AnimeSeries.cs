using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AnimeSeries : AnimeSeries, IShokoSeries
{
    #region DB Columns

    public DateTime UpdatedAt { get; set; }

    public DataSourceType DisableAutoMatchFlags { get; set; } = 0;

    #endregion

    #region Disabled Auto Matching

    public bool IsTvDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.TvDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.TvDB;
            else
                DisableAutoMatchFlags &= ~DataSourceType.TvDB;
        }
    }

    public bool IsTMDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.TMDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.TMDB;
            else
                DisableAutoMatchFlags &= ~DataSourceType.TMDB;
        }
    }

    public bool IsTraktAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Trakt);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Trakt;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Trakt;
        }
    }

    public bool IsMALAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.MAL);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.MAL;
            else
                DisableAutoMatchFlags &= ~DataSourceType.MAL;
        }
    }

    public bool IsAniListAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.AniList);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.AniList;
            else
                DisableAutoMatchFlags &= ~DataSourceType.AniList;
        }
    }

    public bool IsAnimeshonAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Animeshon);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Animeshon;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Animeshon;
        }
    }

    public bool IsKitsuAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DataSourceType.Kitsu);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DataSourceType.Kitsu;
            else
                DisableAutoMatchFlags &= ~DataSourceType.Kitsu;
        }
    }

    #endregion

    #region Titles & Overviews

    private string? _preferredTitle = null;

    public string PreferredTitle => LoadPreferredTitle();

    public void ResetPreferredTitle()
    {
        _preferredTitle = null;
        LoadPreferredTitle();
    }

    private string LoadPreferredTitle()
    {
        if (_preferredTitle is not null)
            return _preferredTitle;

        // Return the override if it's set.
        if (!string.IsNullOrEmpty(SeriesNameOverride))
            return _preferredTitle = SeriesNameOverride;
        // Check each provider in the given order.
        foreach (var source in Utils.SettingsProvider.GetSettings().Language.SeriesTitleSourceOrder)
        {
            var title = source switch
            {
                DataSourceType.AniDB =>
                    AniDB_Anime?.PreferredTitle,
                DataSourceType.TvDB =>
                    TvDBSeries.FirstOrDefault(show => !string.IsNullOrEmpty(show.SeriesName) && !show.SeriesName.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.SeriesName,
                DataSourceType.TMDB =>
                    (TmdbShows is { Count: > 0 } tmdbShows ? tmdbShows[0].GetPreferredTitle(false)?.Value : null) ??
                    (TmdbMovies is { Count: 1 } tmdbMovies ? tmdbMovies[0].GetPreferredTitle(false)?.Value : null),
                _ => null,
            };
            if (!string.IsNullOrEmpty(title))
                return _preferredTitle = title;
        }
        // The most "default" title we have, even if AniDB isn't a preferred source.
        return _preferredTitle = AniDB_Anime?.MainTitle ?? $"AniDB Anime {AniDB_ID}";
    }

    private List<AnimeTitle>? _animeTitles = null;

    public IReadOnlyList<AnimeTitle> Titles => LoadAnimeTitles();

    public void ResetAnimeTitles()
    {
        _animeTitles = null;
        LoadAnimeTitles();
    }

    private List<AnimeTitle> LoadAnimeTitles()
    {
        if (_animeTitles is not null)
            return _animeTitles;

        lock (this)
        {
            if (_animeTitles is not null)
                return _animeTitles;

            var titles = new List<AnimeTitle>();
            var seriesOverrideTitle = false;
            if (!string.IsNullOrEmpty(SeriesNameOverride))
            {
                titles.Add(new()
                {
                    Source = DataSourceEnum.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Title = SeriesNameOverride,
                    Type = TitleType.Main,
                });
                seriesOverrideTitle = true;
            }

            var animeTitles = (this as IShokoSeries).AnidbAnime.Titles;
            if (seriesOverrideTitle)
            {
                var mainTitle = animeTitles.FirstOrDefault(title => title.Type == TitleType.Main);
                if (mainTitle is not null)
                    mainTitle.Type = TitleType.Official;
            }
            titles.AddRange(animeTitles);
            titles.AddRange((this as IShokoSeries).LinkedSeries.Where(e => e.Source is not DataSourceEnum.AniDB).SelectMany(ep => ep.Titles));

            return _animeTitles = titles;
        }
    }

    private string? _preferredOverview = null;

    public string PreferredOverview => LoadPreferredOverview();

    public void ResetPreferredOverview()
    {
        _preferredOverview = null;
        LoadPreferredOverview();
    }

    private string LoadPreferredOverview()
    {
        // Return the cached value if it's set.
        if (_preferredOverview is not null)
            return _preferredOverview;

        // Check each provider in the given order.
        foreach (var source in Utils.SettingsProvider.GetSettings().Language.DescriptionSourceOrder)
        {
            var overview = source switch
            {
                DataSourceType.AniDB =>
                    AniDB_Anime?.Description,
                DataSourceType.TvDB =>
                    TvDBSeries.FirstOrDefault(show => !string.IsNullOrEmpty(show.SeriesName) && !show.SeriesName.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.Overview,
                DataSourceType.TMDB =>
                    (TmdbShows is { Count: > 0 } tmdbShows ? tmdbShows[0].GetPreferredOverview(false)?.Value : null) ??
                    (TmdbMovies is { Count: 1 } tmdbMovies ? tmdbMovies[0].GetPreferredOverview(false)?.Value : null),
                _ => null,
            };
            if (!string.IsNullOrEmpty(overview))
                return _preferredOverview = overview;
        }

        // Return nothing if no provider had an overview in the preferred language.
        return _preferredOverview = string.Empty;
    }

    #endregion

    public List<SVR_VideoLocal> VideoLocals => RepoFactory.VideoLocal.GetByAniDBAnimeID(AniDB_ID);

    public IReadOnlyList<SVR_AnimeEpisode> AnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Where(episode => !episode.IsHidden)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public IReadOnlyList<SVR_AnimeEpisode> AllAnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    #region Images

    public IImageMetadata? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AniDB_ID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AniDB_ID, entityType.Value)!] : RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AniDB_ID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .ToDictionary(image => image.ImageType);
        var images = new List<IImageMetadata>();
        if (!entityType.HasValue || entityType.Value is ImageEntityType.Poster)
        {
            var poster = AniDB_Anime?.GetImageMetadata(false);
            if (poster is not null)
                images.Add(preferredImages.TryGetValue(ImageEntityType.Poster, out var preferredPoster) && poster.Equals(preferredPoster)
                    ? preferredPoster
                    : poster
                );
        }
        foreach (var tmdbShow in TmdbShows)
            images.AddRange(tmdbShow.GetImages(entityType, preferredImages));
        foreach (var tmdbMovie in TmdbMovies)
            images.AddRange(tmdbMovie.GetImages(entityType, preferredImages));
        foreach (var tvdbShow in TvDBSeries)
            images.AddRange(tvdbShow.GetImages(entityType, preferredImages));

        return images;
    }

    #endregion

    #region AniDB

    public SVR_AniDB_Anime? AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies => TmdbMovieCrossReferences
        .Select(xref => xref.TmdbMovie)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Show> TmdbShows => TmdbShowCrossReferences
        .Select(xref => xref.TmdbShow)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> GetTmdbEpisodeCrossReferences(int? tmdbShowId = null) => tmdbShowId.HasValue
        ? RepoFactory.CrossRef_AniDB_TMDB_Episode.GetOnlyByAnidbAnimeAndTmdbShowIDs(AniDB_ID, tmdbShowId.Value)
        : RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbAnimeID(AniDB_ID);

    #endregion

    #region TvDB

    public List<CrossRef_AniDB_TvDB> TvdbSeriesCrossReferences => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AniDB_ID);

    public List<TvDB_Series> TvDBSeries => TvdbSeriesCrossReferences.Select(xref => xref.GetTvDBSeries()).WhereNotNull().ToList();

    #endregion

    #region Trakt

    public List<CrossRef_AniDB_TraktV2> TraktShowCrossReferences => RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AniDB_ID);

    public List<Trakt_Show> TraktShow
    {
        get
        {
            var series = new List<Trakt_Show>();
            var xrefs = TraktShowCrossReferences;
            if (xrefs.Count == 0)
                return [];

            using var session = Utils.ServiceContainer.GetRequiredService<DatabaseFactory>().SessionFactory.OpenSession();
            return xrefs
                .Select(xref => xref.GetByTraktShow(session))
                .WhereNotNull()
                .ToList();
        }
    }

    #endregion

    #region MAL

    public List<CrossRef_AniDB_MAL> MALCrossReferences
        => RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AniDB_ID);

    #endregion

    private DateTime? _airDate;

    public DateTime? AirDate
    {
        get
        {
            if (_airDate != null) return _airDate;
            var anime = AniDB_Anime;
            if (anime?.AirDate != null)
                return _airDate = anime.AirDate.Value;

            // This will be slower, but hopefully more accurate
            var ep = RepoFactory.AniDB_Episode.GetByAnimeID(AniDB_ID)
                .Where(a => a.EpisodeType == (int)Shoko.Models.Enums.EpisodeType.Episode && a.LengthSeconds > 0 && a.AirDate != 0)
                .MinBy(a => a.AirDate);
            return _airDate = ep?.GetAirDateAsDate();
        }
    }

    private DateTime? _endDate;
    public DateTime? EndDate
    {
        get
        {
            if (_endDate != null) return _endDate;
            return _endDate = AniDB_Anime?.EndDate;
        }
    }

    public HashSet<int> Years
    {
        get
        {
            var anime = AniDB_Anime;
            var startYear = anime?.BeginYear ?? 0;
            if (startYear == 0) return [];

            var endYear = anime?.EndYear ?? 0;
            if (endYear == 0) endYear = DateTime.Today.Year;
            if (endYear < startYear) endYear = startYear;
            if (startYear == endYear) return [startYear];

            return Enumerable.Range(startYear, endYear - startYear + 1).Where(anime.IsInYear).ToHashSet();
        }
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public SVR_AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public SVR_AnimeGroup TopLevelAnimeGroup
    {
        get
        {
            var parentGroup = RepoFactory.AnimeGroup.GetByID(AnimeGroupID) ??
                throw new NullReferenceException($"Unable to find parent AnimeGroup {AnimeGroupID} for AnimeSeries {AnimeSeriesID}");

            int parentID;
            while ((parentID = parentGroup.AnimeGroupParentID ?? 0) != 0)
            {
                parentGroup = RepoFactory.AnimeGroup.GetByID(parentID) ??
                    throw new NullReferenceException($"Unable to find parent AnimeGroup {parentGroup.AnimeGroupParentID} for AnimeGroup {parentGroup.AnimeGroupID}");
            }

            return parentGroup;
        }
    }

    public List<SVR_AnimeGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<SVR_AnimeGroup>();
            var groupID = AnimeGroupID;
            while (groupID != 0)
            {
                var grp = RepoFactory.AnimeGroup.GetByID(groupID);
                if (grp != null)
                {
                    grps.Add(grp);
                    groupID = grp.AnimeGroupParentID ?? 0;
                }
                else
                {
                    groupID = 0;
                }
            }

            return grps;
        }
    }

    public override string ToString()
    {
        return $"Series: {AniDB_Anime?.MainTitle} ({AnimeSeriesID})";
        //return string.Empty;
    }

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.Shoko;

    int IMetadata<int>.ID => AnimeSeriesID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => SeriesNameOverride ?? AniDB_Anime?.MainTitle ?? $"<Shoko Series {AnimeSeriesID}>";

    string IWithTitles.PreferredTitle => PreferredTitle;

    #endregion

    #region IWithDescription Implementation

    string IWithDescriptions.DefaultDescription => AniDB_Anime?.Description ?? string.Empty;

    string IWithDescriptions.PreferredDescription => PreferredOverview;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => (this as IShokoSeries).LinkedSeries.SelectMany(ep => ep.Descriptions).ToList() is { Count: > 0 } titles
        ? titles
        : [new() { Source = DataSourceEnum.AniDB, Language = TitleLanguage.English, LanguageCode = "en", Value = string.Empty }];

    #endregion

    #region IWithImages Implementation

    #endregion

    #region ISeries Implementation

    AnimeType ISeries.Type => (AnimeType)(AniDB_Anime?.AnimeType ?? -1);

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => [AnimeSeriesID];

    double ISeries.Rating => (AniDB_Anime?.Rating ?? 0) / 100D;

    bool ISeries.Restricted => (AniDB_Anime?.Restricted ?? 0) == 1;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => [this];

    IImageMetadata? ISeries.DefaultPoster => AniDB_Anime?.GetImageMetadata();

    IReadOnlyList<IRelatedMetadata<ISeries>> ISeries.RelatedSeries => [];

    IReadOnlyList<IRelatedMetadata<IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    IReadOnlyList<IEpisode> ISeries.Episodes => AllAnimeEpisodes;

    IReadOnlyList<IVideo> ISeries.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    EpisodeCounts ISeries.EpisodeCounts => (this as IShokoSeries).AnidbAnime.EpisodeCounts;

    #endregion

    #region IShokoSeries Implementation

    int IShokoSeries.AnidbAnimeID => AniDB_ID;

    int IShokoSeries.ParentGroupID => AnimeGroupID;

    int IShokoSeries.TopLevelGroupID => TopLevelAnimeGroup.AnimeGroupID;

    ISeries IShokoSeries.AnidbAnime => AniDB_Anime ??
        throw new NullReferenceException($"Unable to find AniDB anime with id {AniDB_ID} in IShokoSeries.AnidbAnime");

    IShokoGroup IShokoSeries.ParentGroup => AnimeGroup;

    IShokoGroup IShokoSeries.TopLevelGroup => TopLevelAnimeGroup;

    IReadOnlyList<IShokoGroup> IShokoSeries.AllParentGroups => AllGroupsAbove;


    IReadOnlyList<ISeries> IShokoSeries.LinkedSeries
    {
        get
        {
            var seriesList = new List<ISeries>();

            var anidbAnime = AniDB_Anime;
            if (anidbAnime is not null)
                seriesList.Add(anidbAnime);

            seriesList.AddRange(TmdbShows);

            // Add more series here.

            return seriesList;
        }
    }
    IReadOnlyList<IMovie> IShokoSeries.LinkedMovies
    {
        get
        {
            var movieList = new List<IMovie>();

            movieList.AddRange(TmdbMovies);

            // Add more movies here.

            return movieList;
        }
    }

    IReadOnlyList<IShokoEpisode> IShokoSeries.Episodes => AllAnimeEpisodes;

    #endregion
}
