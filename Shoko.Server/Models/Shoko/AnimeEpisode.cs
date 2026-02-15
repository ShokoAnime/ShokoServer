using System;
using System.Collections.Generic;
using System.Linq;
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
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Models.Shoko;

public class AnimeEpisode : IShokoEpisode, IEquatable<AnimeEpisode>
{
    #region DB Columns

    /// <summary>
    /// Local <see cref="AnimeEpisode"/> id.
    /// </summary>
    public int AnimeEpisodeID { get; set; }

    /// <summary>
    /// Local <see cref="Shoko.AnimeSeries"/> id.
    /// </summary>
    public int AnimeSeriesID { get; set; }

    /// <summary>
    /// The universally unique anidb episode id.
    /// </summary>
    /// <remarks>
    /// Also see <seealso cref="AniDB.AniDB_Episode"/> for a local representation
    /// of the anidb episode data.
    /// </remarks>
    public int AniDB_EpisodeID { get; set; }

    /// <summary>
    /// Timestamp for when the entry was first created.
    /// </summary>
    public DateTime DateTimeCreated { get; set; }

    /// <summary>
    /// Timestamp for when the entry was last updated.
    /// </summary>
    public DateTime DateTimeUpdated { get; set; }

    /// <summary>
    /// Hidden episodes will not show up in the UI unless explicitly
    /// requested, and will also not count towards the unwatched count for
    /// the series.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Episode name override.
    /// </summary>
    /// <value></value>
    public string? EpisodeNameOverride { get; set; }

    #endregion

    public EpisodeType EpisodeType => AniDB_Episode?.EpisodeType ?? EpisodeType.Episode;

    #region Titles

    public string Title => !string.IsNullOrEmpty(EpisodeNameOverride) ? EpisodeNameOverride : (PreferredTitle ?? DefaultTitle).Value;

    private ITitle? _defaultTitle;

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

                // Fallback to English if available.
                if (RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English) is { Count: > 0 } titles)
                    return _defaultTitle = titles[0];

                return _defaultTitle = new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = $"<AniDB Episode {AniDB_EpisodeID}>",
                    Source = DataSource.None,
                };
            }
        }
    }

    public ITitle? PreferredTitle
    {
        get
        {
            // Return the override if it's set.
            if (!string.IsNullOrEmpty(EpisodeNameOverride))
                return new TitleStub()
                {
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = EpisodeNameOverride,
                    Source = DataSource.User,
                    Type = TitleType.Main,
                };

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.SeriesTitleSourceOrder;
            var languageOrder = Languages.PreferredEpisodeNamingLanguages;

            // Lazy load AniDB titles if needed.
            IReadOnlyList<AniDB_Episode_Title>? anidbTitles = null;
            IReadOnlyList<AniDB_Episode_Title> GetAnidbTitles()
                => anidbTitles ??= RepoFactory.AniDB_Episode_Title.GetByEpisodeID(AniDB_EpisodeID);

            // Lazy load TMDB titles if needed.
            IReadOnlyList<TMDB_Title>? tmdbTitles = null;
            IReadOnlyList<TMDB_Title> GetTmdbTitles()
                => tmdbTitles ??= (
                    TmdbEpisodes is { Count: > 0 } tmdbEpisodes
                        ? tmdbEpisodes[0].GetAllTitles()
                        : TmdbMovies is { Count: 1 } tmdbMovies
                            ? tmdbMovies[0].GetAllTitles()
                            : []
                );

            // Loop through all languages and sources, first by language, then by source.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    ITitle? title = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.Main
                                ? GetAnidbTitles().FirstOrDefault(x => x.Language is TitleLanguage.English)
                                : GetAnidbTitles().FirstOrDefault(x => x.Language == language.Language),
                        DataSource.TMDB =>
                            GetTmdbTitles().GetByLanguage(language.Language),
                        _ => null,
                    };
                    if (title is not null)
                        return title;
                }

            // The most "default" title we have, even if AniDB isn't a preferred source.
            return DefaultTitle;
        }
    }

    public IReadOnlyList<ITitle> Titles
    {
        get
        {
            var titles = new List<ITitle>();
            var episodeOverrideTitle = !string.IsNullOrEmpty(EpisodeNameOverride);
            if (episodeOverrideTitle)
            {
                titles.Add(new TitleStub()
                {
                    Source = DataSource.User,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Value = EpisodeNameOverride!,
                    Type = TitleType.Main,
                });
            }

            var animeTitles = (this as IShokoEpisode).AnidbEpisode.Titles.ToList();
            if (episodeOverrideTitle)
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
                        Type = TitleType.None,
                    });
                }
            }
            titles.AddRange(animeTitles);
            titles.AddRange((this as IShokoEpisode).LinkedEpisodes.Where(e => e.Source is not DataSource.AniDB).SelectMany(ep => ep.Titles));

            return titles;
        }
    }

    public IText? PreferredOverview
    {
        get
        {
            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.DescriptionSourceOrder;
            var languageOrder = Languages.PreferredDescriptionNamingLanguages;
            var anidbOverview = AniDB_Episode?.Description;

            // Lazy load TMDB overviews if needed.
            IReadOnlyList<TMDB_Overview>? tmdbOverviews = null;
            IReadOnlyList<TMDB_Overview> GetTmdbOverviews()
                => tmdbOverviews ??= (
                    TmdbEpisodes is { Count: > 0 } tmdbEpisodes
                        ? tmdbEpisodes[0].GetAllOverviews()
                        : TmdbMovies is { Count: 1 } tmdbMovies
                            ? tmdbMovies[0].GetAllOverviews()
                            : []
                );

            // Check each language and source in the most preferred order.
            foreach (var language in languageOrder)
                foreach (var source in sourceOrder)
                {
                    IText? overview = source switch
                    {
                        DataSource.AniDB =>
                            language.Language is TitleLanguage.English && !string.IsNullOrEmpty(anidbOverview)
                                ? new TextStub()
                                {
                                    Language = TitleLanguage.English,
                                    LanguageCode = "en",
                                    Value = anidbOverview,
                                    Source = DataSource.AniDB,
                                }
                                : null,
                        DataSource.TMDB =>
                            GetTmdbOverviews().GetByLanguage(language.Language),
                        _ => null,
                    };
                    if (overview is not null)
                        return overview;
                }
            // Return nothing if no provider had an overview in the preferred language.
            return null;
        }
    }

    #endregion

    #region Images

    public IImage? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(AniDB_EpisodeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImage> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (
                entityType.HasValue
                    ? [RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(AniDB_EpisodeID, entityType.Value)!]
                    : RepoFactory.AniDB_Episode_PreferredImage.GetByEpisodeID(AniDB_EpisodeID)
            )
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
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
        foreach (var xref in TmdbEpisodeCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));
        foreach (var xref in TmdbMovieCrossReferences)
            images.AddRange(xref.GetImages(entityType, preferredImages));

        return images
            .DistinctBy(image => (image.ImageType, image.Source, image.ID))
            .ToList();
    }

    #endregion

    #region Shoko

    public AnimeEpisode_User? GetUserRecord(int userID)
        => userID <= 0 ? null : RepoFactory.AnimeEpisode_User.GetByUserAndEpisodeID(userID, AnimeEpisodeID);

    /// <summary>
    /// Gets the AnimeSeries this episode belongs to
    /// </summary>
    public AnimeSeries? AnimeSeries
        => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    public IReadOnlyList<VideoLocal> VideoLocals
        => RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
        => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    #endregion

    #region AniDB

    public AniDB_Episode? AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public AniDB_Anime? AniDB_Anime => AniDB_Episode?.AniDB_Anime;

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies =>
        TmdbMovieCrossReferences
            .Select(xref => xref.TmdbMovie)
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<CrossRef_AniDB_TMDB_Episode> TmdbEpisodeCrossReferences =>
        RepoFactory.CrossRef_AniDB_TMDB_Episode.GetByAnidbEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<TMDB_Episode> TmdbEpisodes =>
        TmdbEpisodeCrossReferences
            .Select(xref => xref.TmdbEpisode)
            .WhereNotNull()
            .ToList();

    #endregion

    public bool Equals(AnimeEpisode? other)
        => other is not null &&
            AnimeEpisodeID == other.AnimeEpisodeID &&
            AnimeSeriesID == other.AnimeSeriesID &&
            AniDB_EpisodeID == other.AniDB_EpisodeID &&
            DateTimeUpdated == other.DateTimeUpdated &&
            DateTimeCreated == other.DateTimeCreated;

    public override bool Equals(object? obj)
        => obj is not null && (ReferenceEquals(this, obj) || (obj is AnimeEpisode ep && Equals(ep)));

    public override int GetHashCode()
        => HashCode.Combine(AnimeEpisodeID, AnimeSeriesID, AniDB_EpisodeID, DateTimeUpdated, DateTimeCreated);

    #region IMetadata Implementation

    DataSource IMetadata.Source => DataSource.Shoko;

    int IMetadata<int>.ID => AnimeEpisodeID;

    #endregion

    #region IWithDescription Implementation

    IText? IWithDescriptions.DefaultDescription => AniDB_Episode?.Description is { Length: > 0 }
        ? new TextStub()
        {
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = AniDB_Episode.Description,
            Source = DataSource.AniDB,
        }
        : null;

    IText? IWithDescriptions.PreferredDescription => PreferredOverview;

    IReadOnlyList<IText> IWithDescriptions.Descriptions => (this as IShokoEpisode).LinkedEpisodes.SelectMany(ep => ep.Descriptions).ToList();

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
            if (AniDB_Episode is IEpisode anidbEpisode)
                list.AddRange(anidbEpisode.Cast);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Cast);
            foreach (var episode in TmdbEpisodes)
                list.AddRange(episode.Cast);
            return list;
        }
    }

    IReadOnlyList<ICrew> IWithCastAndCrew.Crew
    {
        get
        {
            var list = new List<ICrew>();
            if (AniDB_Episode is IEpisode anidbEpisode)
                list.AddRange(anidbEpisode.Crew);
            foreach (var movie in TmdbMovies)
                list.AddRange(movie.Crew);
            foreach (var episode in TmdbEpisodes)
                list.AddRange(episode.Crew);
            return list;
        }
    }

    #endregion

    #region IEpisode Implementation

    int IEpisode.SeriesID => AnimeSeriesID;

    IReadOnlyList<int> IEpisode.ShokoEpisodeIDs => [AnimeEpisodeID];

    EpisodeType IEpisode.Type => EpisodeType;

    int IEpisode.EpisodeNumber => AniDB_Episode?.EpisodeNumber ?? 1;

    int? IEpisode.SeasonNumber => EpisodeType switch { EpisodeType.Episode => 1, EpisodeType.Special => 0, _ => null };

    double IEpisode.Rating => AniDB_Episode?.RatingDouble ?? 0;

    int IEpisode.RatingVotes => AniDB_Episode?.VotesInt ?? 0;

    IImage? IEpisode.DefaultThumbnail => TmdbEpisodes is { Count: > 0 } tmdbEpisodes
        ? tmdbEpisodes[0].DefaultThumbnail
        : null;

    TimeSpan IEpisode.Runtime => TimeSpan.FromSeconds(AniDB_Episode?.LengthSeconds ?? 0);

    DateOnly? IEpisode.AirDate
    {
        get
        {
            // TODO: Add AniList Episode air date check here

            if (AniDB_Episode?.GetAirDateAsDateOnly() is { } airDate)
                return airDate;

            foreach (var xref in TmdbEpisodeCrossReferences)
            {
                if (xref.TmdbEpisode?.AiredAt is { } tmdbAirDate)
                    return tmdbAirDate;
            }

            return null;
        }
    }

    DateTime? IEpisode.AirDateWithTime
    {
        get
        {
            // TODO: Add AniList Episode air date check here

            if (AniDB_Episode?.GetAirDateAsDate() is { } airDate)
                return airDate;

            foreach (var xref in TmdbEpisodeCrossReferences)
            {
                if (xref.TmdbEpisode?.AiredAt is { } tmdbAirDate)
                    return tmdbAirDate.ToDateTime().Date;
            }

            return null;
        }
    }

    ISeries? IEpisode.Series => AnimeSeries;

    IReadOnlyList<IShokoEpisode> IEpisode.ShokoEpisodes => [this];

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    IReadOnlyList<IVideo> IEpisode.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    #endregion

    #region IShokoEpisode Implementation

    int IShokoEpisode.AnidbEpisodeID => AniDB_EpisodeID;

    IShokoSeries? IShokoEpisode.Series => AnimeSeries;

    IAnidbEpisode IShokoEpisode.AnidbEpisode => AniDB_Episode ??
        throw new NullReferenceException($"Unable to find AniDB Episode {AniDB_EpisodeID} for AnimeEpisode {AnimeEpisodeID}");

    IReadOnlyList<IAnilistEpisode> IShokoEpisode.AnilistEpisodes => [];

    IReadOnlyList<IAnilistEpisodeCrossReference> IShokoEpisode.AnilistEpisodeCrossReferences => [];

    IReadOnlyList<ITmdbEpisode> IShokoEpisode.TmdbEpisodes => TmdbEpisodes;

    IReadOnlyList<IMovie> IShokoEpisode.TmdbMovies => TmdbMovies;

    IReadOnlyList<ITmdbEpisodeCrossReference> IShokoEpisode.TmdbEpisodeCrossReferences => TmdbEpisodeCrossReferences;

    IReadOnlyList<ITmdbMovieCrossReference> IShokoEpisode.TmdbMovieCrossReferences => TmdbMovieCrossReferences;

    IReadOnlyList<IEpisode> IShokoEpisode.LinkedEpisodes
    {
        get
        {
            var episodeList = new List<IEpisode>();

            var anidbEpisode = AniDB_Episode;
            if (anidbEpisode is not null)
                episodeList.Add(anidbEpisode);

            episodeList.AddRange(TmdbEpisodes);

            // Add more episodes here as needed.

            return episodeList;
        }
    }

    IReadOnlyList<IMovie> IShokoEpisode.LinkedMovies
    {
        get
        {
            var movieList = new List<IMovie>();

            movieList.AddRange(TmdbMovies);

            // Add more movies here as needed.

            return movieList;
        }
    }

    IEpisodeUserData IShokoEpisode.GetUserData(IUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (user.ID is 0 || RepoFactory.JMMUser.GetByID(user.ID) is null)
            throw new ArgumentException("User is not stored in the database!", nameof(user));
        var userData = GetUserRecord(user.ID)
            ?? new() { JMMUserID = user.ID, AnimeEpisodeID = AnimeEpisodeID, AnimeSeriesID = AnimeSeriesID };
        if (userData.AnimeEpisode_UserID is 0)
            RepoFactory.AnimeEpisode_User.Save(userData);
        return userData;
    }

    #endregion
}
