using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Anidb;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.DataModels.Tmdb;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Providers.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;
using AnimeTitle = Shoko.Plugin.Abstractions.DataModels.AnimeTitle;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AnimeEpisode : AnimeEpisode, IShokoEpisode
{
    #region DB Columns

    /// <summary>
    /// Episode name override.
    /// </summary>
    /// <value></value>
    public string? EpisodeNameOverride { get; set; }

    #endregion

    public EpisodeType EpisodeTypeEnum => (EpisodeType)(AniDB_Episode?.EpisodeType ?? 1);

    public double UserRating
    {
        get
        {
            var vote = RepoFactory.AniDB_Vote.GetByEntityAndType(AnimeEpisodeID, AniDBVoteType.Episode);
            if (vote != null) return vote.VoteValue / 100D;
            return -1;
        }
    }

    #region Titles

    public string DefaultTitle
    {
        get
        {
            // Fallback to English if available.
            return RepoFactory.AniDB_Episode_Title.GetByEpisodeIDAndLanguage(AniDB_EpisodeID, TitleLanguage.English)
                .FirstOrDefault()
                ?.Title ?? $"<AniDB Episode {AniDB_EpisodeID}>";
        }
    }

    public string PreferredTitle
    {
        get
        {
            // Return the override if it's set.
            if (!string.IsNullOrEmpty(EpisodeNameOverride))
                return EpisodeNameOverride;

            var settings = Utils.SettingsProvider.GetSettings();
            var sourceOrder = settings.Language.SeriesTitleSourceOrder;
            var languageOrder = Languages.PreferredEpisodeNamingLanguages;

            // Lazy load AniDB titles if needed.
            IReadOnlyList<SVR_AniDB_Episode_Title>? anidbTitles = null;
            IReadOnlyList<SVR_AniDB_Episode_Title> GetAnidbTitles()
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
                    var title = source switch
                    {
                        DataSourceType.AniDB =>
                            language.Language is TitleLanguage.Main
                                ? GetAnidbTitles().FirstOrDefault(x => x.Language is TitleLanguage.English)?.Title ?? $"<AniDB Episode {AniDB_EpisodeID}>"
                                : GetAnidbTitles().FirstOrDefault(x => x.Language == language.Language)?.Title,
                        DataSourceType.TMDB =>
                            GetTmdbTitles().GetByLanguage(language.Language)?.Value,
                        _ => null,
                    };
                    if (!string.IsNullOrEmpty(title))
                        return title;
                }

            // The most "default" title we have, even if AniDB isn't a preferred source.
            return DefaultTitle;
        }
    }

    public string PreferredOverview
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
                    var overview = source switch
                    {
                        DataSourceType.AniDB =>
                            language.Language is TitleLanguage.English && !string.IsNullOrEmpty(anidbOverview)
                                ? anidbOverview
                                : null,
                        DataSourceType.TMDB =>
                            GetTmdbOverviews().GetByLanguage(language.Language)?.Value,
                        _ => null,
                    };
                    if (!string.IsNullOrEmpty(overview))
                        return overview;
                }
            // Return nothing if no provider had an overview in the preferred language.
            return string.Empty;
        }
    }

    #endregion

    #region Images

    public IImageMetadata? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(AniDB_EpisodeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Episode_PreferredImage.GetByAnidbEpisodeIDAndType(AniDB_EpisodeID, entityType.Value)!] : RepoFactory.AniDB_Episode_PreferredImage.GetByEpisodeID(AniDB_EpisodeID))
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

    public SVR_AnimeEpisode_User? GetUserRecord(int userID)
        => userID <= 0 ? null : RepoFactory.AnimeEpisode_User.GetByUserIDAndEpisodeID(userID, AnimeEpisodeID);

    /// <summary>
    /// Gets the AnimeSeries this episode belongs to
    /// </summary>
    public SVR_AnimeSeries? AnimeSeries
        => RepoFactory.AnimeSeries.GetByID(AnimeSeriesID);

    public IReadOnlyList<VideoLocal> VideoLocals
        => RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

    public IReadOnlyList<CrossRef_File_Episode> FileCrossReferences
        => RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    #endregion

    #region AniDB

    public SVR_AniDB_Episode? AniDB_Episode => RepoFactory.AniDB_Episode.GetByEpisodeID(AniDB_EpisodeID);

    public SVR_AniDB_Anime? AniDB_Anime => AniDB_Episode?.AniDB_Anime;

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

    protected bool Equals(SVR_AnimeEpisode other)
        => AnimeEpisodeID == other.AnimeEpisodeID &&
            AnimeSeriesID == other.AnimeSeriesID &&
            AniDB_EpisodeID == other.AniDB_EpisodeID &&
            DateTimeUpdated == other.DateTimeUpdated &&
            DateTimeCreated == other.DateTimeCreated;

    public override bool Equals(object? obj)
        => obj is not null && (ReferenceEquals(this, obj) || (obj is SVR_AnimeEpisode ep && Equals(ep)));

    public override int GetHashCode()
        => HashCode.Combine(AnimeEpisodeID, AnimeSeriesID, AniDB_EpisodeID, DateTimeUpdated, DateTimeCreated);

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.Shoko;

    int IMetadata<int>.ID => AnimeEpisodeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => DefaultTitle;

    string IWithTitles.PreferredTitle => PreferredTitle;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles
    {
        get
        {
            var titles = new List<AnimeTitle>();
            var episodeOverrideTitle = !string.IsNullOrEmpty(EpisodeNameOverride);
            if (episodeOverrideTitle)
            {
                titles.Add(new()
                {
                    Source = DataSourceEnum.Shoko,
                    Language = TitleLanguage.Unknown,
                    LanguageCode = "unk",
                    Title = EpisodeNameOverride!,
                    Type = TitleType.Main,
                });
            }

            var animeTitles = (this as IShokoEpisode).AnidbEpisode.Titles;
            if (episodeOverrideTitle)
            {
                var mainTitle = animeTitles.FirstOrDefault(title => title.Type == TitleType.Main);
                if (mainTitle is not null)
                    mainTitle.Type = TitleType.Official;
            }
            titles.AddRange(animeTitles);
            titles.AddRange((this as IShokoEpisode).LinkedEpisodes.Where(e => e.Source is not DataSourceEnum.AniDB).SelectMany(ep => ep.Titles));

            return titles;
        }
    }

    #endregion

    #region IWithDescription Implementation

    string IWithDescriptions.DefaultDescription => AniDB_Episode?.Description ?? string.Empty;

    string IWithDescriptions.PreferredDescription => AniDB_Episode?.Description ?? string.Empty;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => (this as IShokoEpisode).LinkedEpisodes.SelectMany(ep => ep.Descriptions).ToList() is { Count: > 0 } titles
        ? titles
        : [new() { Source = DataSourceEnum.AniDB, Language = TitleLanguage.English, LanguageCode = "en", Value = string.Empty }];

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

    AbstractEpisodeType IEpisode.Type => (AbstractEpisodeType)EpisodeTypeEnum;

    int IEpisode.EpisodeNumber => AniDB_Episode?.EpisodeNumber ?? 1;

    int? IEpisode.SeasonNumber => EpisodeTypeEnum == EpisodeType.Episode ? 1 : null;

    TimeSpan IEpisode.Runtime => TimeSpan.FromSeconds(AniDB_Episode?.LengthSeconds ?? 0);

    DateTime? IEpisode.AirDate => AniDB_Episode?.GetAirDateAsDate();

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

    IReadOnlyList<ITmdbEpisode> IShokoEpisode.TmdbEpisodes => TmdbEpisodes;

    IReadOnlyList<IMovie> IShokoEpisode.TmdbMovies => TmdbMovies;

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

    #endregion
}
