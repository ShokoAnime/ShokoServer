using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
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
                ?.Title ?? $"AniDB Episode {AniDB_EpisodeID}";
        }
    }

    public string PreferredTitle
    {
        get
        {
            // Return the override if it's set.
            if (!string.IsNullOrEmpty(EpisodeNameOverride))
                return EpisodeNameOverride;
            // Check each provider in the given order.
            foreach (var source in Utils.SettingsProvider.GetSettings().Language.EpisodeTitleSourceOrder)
            {
                var title = source switch
                {
                    DataSourceType.AniDB =>
                        AniDB_Episode?.PreferredTitle?.Title,
                    DataSourceType.TvDB =>
                        TvDBEpisodes.FirstOrDefault(show => !string.IsNullOrEmpty(show.EpisodeName) && !show.EpisodeName.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.EpisodeName,
                    DataSourceType.TMDB =>
                        (TmdbEpisodes is { Count: > 0 } tmdbShows ? tmdbShows[0].GetPreferredTitle(false)?.Value : null) ??
                        (TmdbMovies is { Count: 1 } tmdbMovies ? tmdbMovies[0].GetPreferredTitle(false)?.Value : null),
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
            // Check each provider in the given order.
            foreach (var source in Utils.SettingsProvider.GetSettings().Language.DescriptionSourceOrder)
            {
                var overview = source switch
                {
                    DataSourceType.AniDB =>
                        AniDB_Episode?.Description,
                    DataSourceType.TvDB =>
                        TvDBEpisodes.FirstOrDefault(show => !string.IsNullOrEmpty(show.EpisodeName) && !show.EpisodeName.Contains("**DUPLICATE", StringComparison.InvariantCultureIgnoreCase))?.Overview,
                    DataSourceType.TMDB =>
                        (TmdbEpisodes is { Count: > 0 } tmdbShows ? tmdbShows[0].GetPreferredOverview(false)?.Value : null) ??
                        (TmdbMovies is { Count: 1 } tmdbMovies ? tmdbMovies[0].GetPreferredOverview(false)?.Value : null),
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
                images.Add(preferredImages.TryGetValue(ImageEntityType.Poster, out var preferredPoster) && poster == preferredPoster
                    ? preferredPoster
                    : poster
                );
        }
        foreach (var tvdbEpisode in TvDBEpisodes)
            images.AddRange(tvdbEpisode.GetImages(entityType, preferredImages));
        foreach (var tmdbEpisode in TmdbEpisodes)
            images.AddRange(tmdbEpisode.GetImages(entityType, preferredImages));
        foreach (var tmdbMovie in TmdbMovies)
            images.AddRange(tmdbMovie.GetImages(entityType, preferredImages));

        return images;
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

    public List<SVR_VideoLocal> VideoLocals
        => RepoFactory.VideoLocal.GetByAniDBEpisodeID(AniDB_EpisodeID);

    public List<SVR_CrossRef_File_Episode> FileCrossReferences
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

    #region TvDB

    public TvDB_Episode? TvDBEpisode
    {
        get
        {
            // Try Overrides first, then regular
            return RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault() ?? RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).FirstOrDefault();
        }
    }

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            // Try Overrides first, then regular
            var overrides = RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAniDBEpisodeID(AniDB_EpisodeID)
                .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
            return overrides.Count > 0
                ? overrides
                : RepoFactory.CrossRef_AniDB_TvDB_Episode.GetByAniDBEpisodeID(AniDB_EpisodeID)
                    .Select(a => RepoFactory.TvDB_Episode.GetByTvDBID(a.TvDBEpisodeID)).Where(a => a != null)
                    .OrderBy(a => a.SeasonNumber).ThenBy(a => a.EpisodeNumber).ToList();
        }
    }

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

    IReadOnlyList<IVideoCrossReference> IEpisode.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID);

    IReadOnlyList<IVideo> IEpisode.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByEpisodeID(AniDB_EpisodeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .OfType<SVR_VideoLocal>()
            .ToList();

    #endregion

    #region IShokoEpisode Implementation

    int IShokoEpisode.AnidbEpisodeID => AniDB_EpisodeID;

    IShokoSeries? IShokoEpisode.Series => AnimeSeries;

    IEpisode IShokoEpisode.AnidbEpisode => AniDB_Episode ??
        throw new NullReferenceException($"Unable to find AniDB Episode {AniDB_EpisodeID} for AnimeEpisode {AnimeEpisodeID}");

    #endregion
}
