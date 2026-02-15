using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Abstractions.Enums;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;
using Shoko.Abstractions.Metadata.Anilist;
using Shoko.Abstractions.Metadata.Anilist.CrossReferences;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Metadata.Stub;
using Shoko.Abstractions.Metadata.Tmdb;
using Shoko.Abstractions.Metadata.Tmdb.CrossReferences;
using Shoko.Abstractions.User;
using Shoko.Abstractions.UserData;
using Shoko.Abstractions.Video;
using Shoko.Server.Extensions;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.Shoko.Embedded;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.AniDB.Titles;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class AnimeSeries : IShokoSeries
{
    #region DB Columns

    public int AnimeSeriesID { get; set; }

    public int AnimeGroupID { get; set; }

    public int AniDB_ID { get; set; }

    public DateTime DateTimeUpdated { get; set; }

    public DateTime DateTimeCreated { get; set; }

    public string? DefaultAudioLanguage { get; set; }

    public string? DefaultSubtitleLanguage { get; set; }

    public DateTime? EpisodeAddedDate { get; set; }

    public DateTime? LatestEpisodeAirDate { get; set; }

    public DayOfWeek? AirsOn { get; set; }

    public int MissingEpisodeCount { get; set; }

    public int MissingEpisodeCountGroups { get; set; }

    public int HiddenMissingEpisodeCount { get; set; }

    public int HiddenMissingEpisodeCountGroups { get; set; }

    public int LatestLocalEpisodeNumber { get; set; }

    public string? SeriesNameOverride { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DisabledAutoMatchFlag DisableAutoMatchFlags { get; set; } = 0;

    #endregion

    #region Disabled Auto Matching

    public bool IsTMDBAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.TMDB);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.TMDB;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.TMDB;
        }
    }

    public bool IsMALAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.MAL);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.MAL;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.MAL;
        }
    }

    public bool IsAniListAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.AniList);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.AniList;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.AniList;
        }
    }

    public bool IsAnimeshonAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.Animeshon);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.Animeshon;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.Animeshon;
        }
    }

    public bool IsKitsuAutoMatchingDisabled
    {
        get
        {
            return DisableAutoMatchFlags.HasFlag(DisabledAutoMatchFlag.Kitsu);
        }
        set
        {
            if (value)
                DisableAutoMatchFlags |= DisabledAutoMatchFlag.Kitsu;
            else
                DisableAutoMatchFlags &= ~DisabledAutoMatchFlag.Kitsu;
        }
    }

    #endregion

    #region Titles & Overviews

    public string Title => !string.IsNullOrEmpty(SeriesNameOverride) ? SeriesNameOverride : (PreferredTitle ?? DefaultTitle).Value;

    private ITitle? _defaultTitle = null;

    public ITitle DefaultTitle
    {
        get
        {
            if (_defaultTitle is not null)
                return _defaultTitle;

            lock (this)
            {
                if (_defaultTitle is not null)
                    return _defaultTitle;

                // Return the override if it's set.
                if (!string.IsNullOrEmpty(SeriesNameOverride))
                    return _defaultTitle = new TitleStub
                    {
                        Source = DataSource.User,
                        Language = TitleLanguage.Unknown,
                        LanguageCode = "unk",
                        Value = SeriesNameOverride,
                        Type = TitleType.Main,
                    };

                if (AniDB_Anime is { } anime)
                    return _defaultTitle = anime.DefaultTitle;

                var titleHelper = Utils.ServiceContainer.GetRequiredService<AniDBTitleHelper>();
                if (titleHelper.SearchAnimeID(AniDB_ID) is { } titleResponse && titleResponse.Titles.FirstOrDefault(title => title.TitleType == TitleType.Main) is { } defaultTitle)
                    return _defaultTitle = defaultTitle;

                return _defaultTitle = new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = $"<AniDB Anime {AniDB_ID}>",
                    Source = DataSource.None,
                };
            }
        }
    }

    private bool _preferredTitleLoaded = false;

    private ITitle? _preferredTitle = null;

    public ITitle? PreferredTitle => LoadPreferredTitle();

    public void ResetPreferredTitle()
    {
        lock (this)
        {
            if (_preferredTitleLoaded)
                return;
            _preferredTitleLoaded = false;
            _preferredTitle = null;
        }
        LoadPreferredTitle();
    }

    private ITitle? LoadPreferredTitle()
    {
        if (_preferredTitleLoaded)
            return _preferredTitle;

        lock (this)
        {
            if (_preferredTitleLoaded)
                return _preferredTitle;
            _preferredTitleLoaded = true;

            // Return the override if it's set.
            if (!string.IsNullOrEmpty(SeriesNameOverride))
                return _preferredTitle = new TitleStub
                {
                    Source = DataSource.User,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = SeriesNameOverride,
                    Type = TitleType.Main,
                };

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.SeriesTitleSourceOrder;
            var languageOrder = Languages.PreferredNamingLanguages;
            var anime = AniDB_Anime;

            // Lazy load AniDB titles if needed.
            List<AniDB_Anime_Title>? anidbTitles = null;
            List<AniDB_Anime_Title> GetAnidbTitles()
                => anidbTitles ??= RepoFactory.AniDB_Anime_Title.GetByAnimeID(AniDB_ID);

            // Lazy load TMDB titles if needed.
            IReadOnlyList<TMDB_Title>? tmdbTitles = null;

            // Loop through all languages and sources, first by language, then by source.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    ITitle? title = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.Main
                                ? anime?.DefaultTitle
                                : GetAnidbTitles().FirstOrDefault(x => x.TitleType is TitleType.Main or TitleType.Official && x.Language == language.Language)
                                    ?? (settings.Language.UseSynonyms ? GetAnidbTitles().FirstOrDefault(x => x.Language == language.Language) : null),
                        DataSource.TMDB =>
                            (tmdbTitles ??= GetTmdbTitles()).GetByLanguage(language.Language),
                        _ => null,
                    };
                    if (title is not null)
                        return _preferredTitle = title;
                }

            // The most "default" title we have, even if AniDB isn't a preferred source.
            return _preferredTitle = DefaultTitle;
        }
    }

    private IReadOnlyList<TMDB_Title> GetTmdbTitles()
    {
        // If we're linked to multiple TMDB shows or multiple TMDB movies, then
        // don't bother attempting to programmatically find the perfect title.
        if (TmdbShows is { Count: 1 } tmdbShows)
        {
            // TODO: Maybe add argumented season name support. Argumented in the sense that we add the season number or title to the show title per language.
            return tmdbShows[0].GetAllTitles();
        }
        else if (TmdbMovies is { Count: 1 } tmdbMovies)
        {
            return tmdbMovies[0].GetAllTitles();
        }

        return [];
    }

    private List<ITitle>? _animeTitles = null;

    public IReadOnlyList<ITitle> Titles => LoadAnimeTitles();

    public void ResetAnimeTitles()
    {
        _animeTitles = null;
        LoadAnimeTitles();
    }

    private List<ITitle> LoadAnimeTitles()
    {
        if (_animeTitles is not null)
            return _animeTitles;

        lock (this)
        {
            if (_animeTitles is not null)
                return _animeTitles;

            var titles = new List<ITitle>();
            var seriesOverrideTitle = false;
            if (!string.IsNullOrEmpty(SeriesNameOverride))
            {
                titles.Add(new TitleStub()
                {
                    Source = DataSource.User,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = SeriesNameOverride,
                    Type = TitleType.Main,
                });
                seriesOverrideTitle = true;
            }

            var animeTitles = (this as IShokoSeries).AnidbAnime.Titles.ToList();
            if (seriesOverrideTitle)
            {
                var mainTitle = animeTitles.Find(title => title.Type == TitleType.Main);
                if (mainTitle is not null)
                {
                    animeTitles.Remove(mainTitle);
                    animeTitles.Insert(0, new TitleStub()
                    {
                        Language = mainTitle.Language,
                        LanguageCode = mainTitle.LanguageCode,
                        Value = mainTitle.Value,
                        CountryCode = mainTitle.CountryCode,
                        Source = mainTitle.Source,
                        Type = TitleType.Official,
                    });
                }
            }
            titles.AddRange(animeTitles);
            titles.AddRange((this as IShokoSeries).LinkedSeries.Where(e => e.Source is not DataSource.AniDB).SelectMany(ep => ep.Titles));

            return _animeTitles = titles;
        }
    }

    private bool _preferredOverviewLoaded = false;

    private IText? _preferredOverview = null;

    public IText? PreferredOverview => LoadPreferredOverview();

    public void ResetPreferredOverview()
    {
        _preferredOverviewLoaded = false;
        _preferredOverview = null;
        LoadPreferredOverview();
    }

    private IText? LoadPreferredOverview()
    {
        // Return the cached value if it's set.
        if (_preferredOverviewLoaded)
            return _preferredOverview;

        lock (this)
        {
            // Return the cached value if it's set.
            if (_preferredOverviewLoaded)
                return _preferredOverview;
            _preferredOverviewLoaded = true;

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.DescriptionSourceOrder;
            var languageOrder = Languages.PreferredDescriptionNamingLanguages;
            var anidbOverview = AniDB_Anime?.Description;

            // Lazy load TMDB overviews if needed.
            IReadOnlyList<TMDB_Overview>? tmdbOverviews = null;

            // Check each language and source in the most preferred order.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    IText? overview = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.English && !string.IsNullOrEmpty(anidbOverview)
                                ? new TextStub
                                {
                                    Language = TitleLanguage.English,
                                    LanguageCode = "en",
                                    Value = anidbOverview,
                                    Source = DataSource.AniDB,
                                }
                                : null,
                        DataSource.TMDB =>
                            (tmdbOverviews ??= GetTmdbOverviews()).GetByLanguage(language.Language),
                        _ => null,
                    };
                    if (overview is not null)
                        return _preferredOverview = overview;
                }

            // Return nothing if no provider had an overview in the preferred language.
            return _preferredOverview = null;
        }

    }

    private IReadOnlyList<TMDB_Overview> GetTmdbOverviews()
    {
        // Get the overviews from TMDB if we're linked to a single show or
        // movie.
        if (TmdbShows is { Count: 1 } tmdbShows)
        {
            if (TmdbSeasonCrossReferences is not { Count: > 0 } tmdbSeasonCrossReferences)
                return [];

            // If we're linked to season zero, and only season zero, then we're
            // most likely linking an AniDB OVA to one or more specials of the
            // TMDB show, so do some additional logic on that.
            if (tmdbSeasonCrossReferences.Count is 1 && tmdbSeasonCrossReferences[0] is { SeasonNumber: 0 })
            {
                // If we're linking a single episode to a single episode, return
                // the overviews from the linked episode.
                var tmdbEpisodeCrossReferences = TmdbEpisodeCrossReferences
                    .Where(reference => reference.TmdbEpisodeID is not 0)
                    .ToList();
                if (tmdbEpisodeCrossReferences.Count is 1)
                    return tmdbEpisodeCrossReferences[0].TmdbEpisode?.GetAllOverviews() ?? [];

                // If we're linked to multiple episodes, then it will be hard to
                // construct an overview for each language without a lot of
                // additional logic, so just don't bother for now and return an
                // empty list.
                return [];
            }

            // Find the first season that's not season zero. If we found season
            // one, then return the overviews from the show, otherwise return
            // the overviews from the first found season.
            var tmdbSeasonCrossReference = tmdbSeasonCrossReferences
                .OrderBy(e => e.SeasonNumber)
                .First(tmdbSeason => tmdbSeason is not { SeasonNumber: 0 });
            if (tmdbSeasonCrossReference is { SeasonNumber: 1 })
                return tmdbShows[0].GetAllOverviews();

            return tmdbSeasonCrossReference.TmdbSeason?.GetAllOverviews() ?? [];
        }
        else if (TmdbMovies is { Count: 1 } tmdbMovies)
        {
            return tmdbMovies[0].GetAllOverviews();
        }

        return [];
    }

    #endregion

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences => RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    public IReadOnlyList<VideoLocal> VideoLocals => RepoFactory.VideoLocal.GetByAniDBAnimeID(AniDB_ID);

    public IReadOnlyList<AnimeSeason> AnimeSeasons => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID).Any(e => e.EpisodeType is EpisodeType.Special)
        ? [new AnimeSeason(this, EpisodeType.Episode, 1), new AnimeSeason(this, EpisodeType.Special, 0)]
        : [new AnimeSeason(this, EpisodeType.Episode, 1)];

    public IReadOnlyList<AnimeEpisode> AnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Where(episode => !episode.IsHidden)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    public IReadOnlyList<AnimeEpisode> AllAnimeEpisodes => RepoFactory.AnimeEpisode.GetBySeriesID(AnimeSeriesID)
        .Select(episode => (episode, anidbEpisode: episode.AniDB_Episode))
        .OrderBy(tuple => tuple.anidbEpisode?.EpisodeType)
        .ThenBy(tuple => tuple.anidbEpisode?.EpisodeNumber)
        .Select(tuple => tuple.episode)
        .ToList();

    #region Images

    public HashSet<ImageEntityType> GetAvailableImageTypes()
    {
        var images = new List<IImage>();
        var poster = AniDB_Anime?.GetImageMetadata(false);
        if (poster is not null)
            images.Add(poster);
        foreach (var xref in TmdbShowCrossReferences)
            images.AddRange(xref.GetImages());
        foreach (var xref in TmdbSeasonCrossReferences)
            images.AddRange(xref.GetImages());
        foreach (var xref in TmdbMovieCrossReferences.DistinctBy(xref => xref.TmdbMovieID))
            images.AddRange(xref.GetImages());
        return images
            .DistinctBy(image => image.ImageType)
            .Select(image => image.ImageType)
            .ToHashSet();
    }

    public HashSet<ImageEntityType> GetPreferredImageTypes()
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AniDB_ID)
            .Select(preferredImage => preferredImage?.GetImageMetadata())
            .WhereNotNull()
            .Select(image => image.ImageType)
            .ToHashSet();

    public IImage? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AniDB_ID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AniDB_ID, entityType.Value)!] : RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AniDB_ID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .DistinctBy(image => image.ImageType)
            .ToDictionary(image => image.ImageType);
        var images = new List<IImage>();
        if (!entityType.HasValue || entityType.Value is ImageEntityType.Poster)
        {
            var poster = AniDB_Anime?.GetImageMetadata(false);
            if (poster is not null)
                images.Add(preferredImages.TryGetValue(ImageEntityType.Poster, out var preferredPoster) && poster.Equals(preferredPoster)
                    ? preferredPoster
                    : poster
                );
        }
        foreach (var xref in TmdbShowCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbSeasonCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbMovieCrossReferences.DistinctBy(xref => xref.TmdbMovieID))
            images.AddRange(xref.GetImages(entityType, preferredImages));

        return images
            .DistinctBy(image => (image.ImageType, image.Source, image.ID))
            .ToList();
    }

    #endregion

    #region AniDB

    public AniDB_Anime? AniDB_Anime => RepoFactory.AniDB_Anime.GetByAnimeID(AniDB_ID);

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AniDB_ID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies => TmdbMovieCrossReferences
        .DistinctBy(xref => xref.TmdbMovieID)
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

    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> TmdbSeasonCrossReferences =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .DistinctBy(xref => xref.TmdbSeasonID)
            .ToList();

    public IReadOnlyList<TMDB_Season> TmdbSeasons => TmdbSeasonCrossReferences
        .Select(xref => xref.TmdbSeason)
        .WhereNotNull()
        .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Season> GetTmdbSeasonCrossReferences(int? tmdbShowId = null) =>
        GetTmdbEpisodeCrossReferences(tmdbShowId)
            .Select(xref => xref.TmdbSeasonCrossReference)
            .WhereNotNull()
            .Distinct()
            .ToList();

    #endregion

    #region MAL

    public IReadOnlyList<CrossRef_AniDB_MAL> MalCrossReferences
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
                .Where(a => a.EpisodeType is EpisodeType.Episode && a.LengthSeconds > 0 && a.AirDate != 0)
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
            if (AniDB_Anime is not { } anime) return [];
            var startYear = anime.BeginYear;
            if (startYear == 0) return [];

            var endYear = anime.EndYear;
            if (endYear == 0) endYear = DateTime.Today.Year;
            if (endYear < startYear) endYear = startYear;
            if (startYear == endYear) return [startYear];

            return Enumerable.Range(startYear, endYear - startYear + 1).Where(anime.IsInYear).ToHashSet();
        }
    }

    /// <summary>
    /// Gets the direct parent AnimeGroup this series belongs to
    /// </summary>
    public AnimeGroup AnimeGroup => RepoFactory.AnimeGroup.GetByID(AnimeGroupID);

    /// <summary>
    /// Gets the very top level AnimeGroup which this series belongs to
    /// </summary>
    public AnimeGroup TopLevelAnimeGroup
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

    public List<AnimeGroup> AllGroupsAbove
    {
        get
        {
            var grps = new List<AnimeGroup>();
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

    DataSource IMetadata.Source => DataSource.Shoko;

    int IMetadata<int>.ID => AnimeSeriesID;

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => AniDB_Anime is { Description.Length: > 0 } anime
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = anime.Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => PreferredOverview;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => (this as IShokoSeries).LinkedSeries.SelectMany(ep => ep.Descriptions).ToList();

    #endregion

    #region IWithCreationDate Implementation

    DateTime IWithCreationDate.CreatedAt => DateTimeCreated.ToUniversalTime();

    #endregion

    #region IWithUpdateDate Implementation

    DateTime IWithUpdateDate.LastUpdatedAt => DateTimeUpdated.ToUniversalTime();

    #endregion

    #region IWithCastAndCrew Implementation

    IReadOnlyList<ICast> IWithCastAndCrew.Cast
    {
        get
        {
            var list = new List<ICast>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.Cast);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Cast);
            foreach (var show in TmdbShows)
                list.AddRange(show.Cast);
            return list;
        }
    }

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew
    {
        get
        {
            var list = new List<ICrew>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.Crew);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Crew);
            foreach (var show in TmdbShows)
                list.AddRange(show.Crew);
            return list;
        }
    }

    #endregion

    #region IWithStudios Implementation

    IReadOnlyList<IStudio> IWithStudios.Studios
    {
        get
        {
            var list = new List<IStudio>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.Studios);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.TmdbStudios);
            foreach (var show in TmdbShows)
                list.AddRange(show.TmdbStudios);
            return list;
        }
    }

    #endregion

    #region IWithContentRatings Implementation

    IReadOnlyList<IContentRating> IWithContentRatings.ContentRatings
    {
        get
        {
            var list = new List<IContentRating>();
            if (AniDB_Anime is ISeries anidbAnime)
                list.AddRange(anidbAnime.ContentRatings);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.ContentRatings);
            foreach (var show in TmdbShows)
                list.AddRange(show.ContentRatings);
            return list;
        }
    }

    #endregion

    #region IWithYearlySeasons Implementation

    IReadOnlyList<(int Year, YearlySeason Season)> IWithYearlySeasons.YearlySeasons
        => [.. AirDate.GetYearlySeasons(EndDate)];

    #endregion

    #region ISeries Implementation

    AnimeType ISeries.Type => AniDB_Anime?.AnimeType ?? AnimeType.Unknown;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => [AnimeSeriesID];

    double ISeries.Rating => (AniDB_Anime?.Rating ?? 0) / 100D;

    int ISeries.RatingVotes => AniDB_Anime?.VoteCount ?? 0;

    bool ISeries.Restricted => AniDB_Anime?.IsRestricted ?? false;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => [this];

    IImage? ISeries.DefaultPoster => AniDB_Anime?.GetImageMetadata();

    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> ISeries.RelatedSeries => [];

    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AniDB_ID);

    IReadOnlyList<ISeason> ISeries.Seasons => AnimeSeasons;

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

    IReadOnlyList<IShokoTagForSeries> IShokoSeries.Tags => RepoFactory.CustomTag.GetByAnimeID(AniDB_ID)
        .Select(x => new AnimeTag(x, this))
        .OrderBy(x => x.ID)
        .ToList();

    int IShokoSeries.MissingEpisodeCount => MissingEpisodeCount;

    int IShokoSeries.MissingCollectingEpisodeCount => MissingEpisodeCountGroups;

    int IShokoSeries.HiddenMissingEpisodeCount => HiddenMissingEpisodeCount;

    int IShokoSeries.HiddenMissingCollectingEpisodeCount => HiddenMissingEpisodeCountGroups;

    IShokoGroup IShokoSeries.ParentGroup => AnimeGroup;

    IShokoGroup IShokoSeries.TopLevelGroup => TopLevelAnimeGroup;

    IReadOnlyList<IShokoGroup> IShokoSeries.AllParentGroups => AllGroupsAbove;

    IAnidbAnime IShokoSeries.AnidbAnime => AniDB_Anime ??
        throw new NullReferenceException($"Unable to find AniDB anime with id {AniDB_ID} in IShokoSeries.AnidbAnime");

    bool IShokoSeries.AnilistAutoMatchingDisabled => IsAniListAutoMatchingDisabled;

    IReadOnlyList<IAnilistAnime> IShokoSeries.AnilistAnime => [];

    IReadOnlyList<IAnilistAnimeCrossReference> IShokoSeries.AnilistAnimeCrossReferences => [];

    bool IShokoSeries.TmdbAutoMatchingDisabled => IsTMDBAutoMatchingDisabled;

    IReadOnlyList<ITmdbShow> IShokoSeries.TmdbShows => TmdbShows;

    IReadOnlyList<ITmdbSeason> IShokoSeries.TmdbSeasons => TmdbSeasons;

    IReadOnlyList<ITmdbMovie> IShokoSeries.TmdbMovies => TmdbMovies;

    IReadOnlyList<ITmdbShowCrossReference> IShokoSeries.TmdbShowCrossReferences => TmdbShowCrossReferences;

    IReadOnlyList<ITmdbSeasonCrossReference> IShokoSeries.TmdbSeasonCrossReferences => TmdbSeasonCrossReferences;

    IReadOnlyList<ITmdbEpisodeCrossReference> IShokoSeries.TmdbEpisodeCrossReferences => TmdbEpisodeCrossReferences;

    IReadOnlyList<ITmdbMovieCrossReference> IShokoSeries.TmdbMovieCrossReferences => TmdbMovieCrossReferences;

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

    IReadOnlyList<IShokoSeason> IShokoSeries.Seasons => AnimeSeasons;

    IReadOnlyList<IShokoEpisode> IShokoSeries.Episodes => AllAnimeEpisodes;

    ISeriesUserData IShokoSeries.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is 0 || RepoFactory.JMMUser.GetByID(user.ID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        var userData = RepoFactory.AnimeSeries_User.GetByUserAndSeriesID(user.ID, AnimeSeriesID)
            ?? new() { JMMUserID = user.ID, AnimeSeriesID = AnimeSeriesID };
        if (userData.AnimeSeries_UserID is 0)
            RepoFactory.AnimeSeries_User.Save(userData);
        return userData;
    }

    #endregion
}
