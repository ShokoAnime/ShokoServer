using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Extensions;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Models.CrossReference;
using Shoko.Server.Models.TMDB;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;

#nullable enable
namespace Shoko.Server.Models;

public class SVR_AniDB_Anime : AniDB_Anime, ISeries
{
    #region Properties & Methods

    #region General

    [XmlIgnore]
    public AniDB_Vote? UserVote
        => RepoFactory.AniDB_Vote.GetByAnimeID(AnimeID);

    public List<(string Type, string Name, string URL)> Resources
    {
        get
        {
            var result = new List<(string Type, string Name, string URL)>();
            if (!string.IsNullOrEmpty(Site_EN))
                foreach (var site in Site_EN.Split('|'))
                    result.Add((Type: "source", Name: "Official Site (EN)", URL: site));

            if (!string.IsNullOrEmpty(Site_JP))
                foreach (var site in Site_JP.Split('|'))
                    result.Add((Type: "source", Name: "Official Site (JP)", URL: site));

            if (!string.IsNullOrEmpty(Wikipedia_ID))
                result.Add((Type: "wiki", Name: "Wikipedia (EN)", URL: $"https://en.wikipedia.org/{Wikipedia_ID}"));

            if (!string.IsNullOrEmpty(WikipediaJP_ID))
                result.Add((Type: "wiki", Name: "Wikipedia (JP)", URL: $"https://en.wikipedia.org/{WikipediaJP_ID}"));

            if (!string.IsNullOrEmpty(CrunchyrollID))
                result.Add((Type: "streaming", Name: "Crunchyroll", URL: $"https://crunchyroll.com/anime/{CrunchyrollID}"));

            if (!string.IsNullOrEmpty(FunimationID))
                result.Add((Type: "streaming", Name: "Funimation", URL: FunimationID));

            if (!string.IsNullOrEmpty(HiDiveID))
                result.Add((Type: "streaming", Name: "HiDive", URL: $"https://www.hidive.com/{HiDiveID}"));

            if (AllCinemaID.HasValue && AllCinemaID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "allcinema", URL: $"https://allcinema.net/cinema/{AllCinemaID.Value}"));

            if (AnisonID.HasValue && AnisonID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "Anison", URL: $"https://anison.info/data/program/{AnisonID.Value}.html"));

            if (SyoboiID.HasValue && SyoboiID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "syoboi", URL: $"https://cal.syoboi.jp/tid/{SyoboiID.Value}/time"));

            if (BangumiID.HasValue && BangumiID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: "bangumi", URL: $"https://bgm.tv/subject/{BangumiID.Value}"));

            if (LainID.HasValue && LainID.Value > 0)
                result.Add((Type: "foreign-metadata", Name: ".lain", URL: $"http://lain.gr.jp/mediadb/media/{LainID.Value}"));

            if (ANNID.HasValue && ANNID.Value > 0)
                result.Add((Type: "english-metadata", Name: "AnimeNewsNetwork", URL: $"https://www.animenewsnetwork.com/encyclopedia/php?id={ANNID.Value}"));

            if (VNDBID.HasValue && VNDBID.Value > 0)
                result.Add((Type: "english-metadata", Name: "VNDB", URL: $"https://vndb.org/v{VNDBID.Value}"));

            return result;
        }
    }

    public IEnumerable<(int Year, AnimeSeason Season)> Seasons
    {
        get
        {
            if (AirDate is null) yield break;

            var beginYear = AirDate.Value.Year;
            var endYear = EndDate?.Year ?? DateTime.Today.Year;
            for (var year = beginYear; year <= endYear; year++)
            {
                if (beginYear < year && year < endYear)
                {
                    yield return (year, AnimeSeason.Winter);
                    yield return (year, AnimeSeason.Spring);
                    yield return (year, AnimeSeason.Summer);
                    yield return (year, AnimeSeason.Fall);
                    continue;
                }

                if (this.IsInSeason(AnimeSeason.Winter, year)) yield return (year, AnimeSeason.Winter);
                if (this.IsInSeason(AnimeSeason.Spring, year)) yield return (year, AnimeSeason.Spring);
                if (this.IsInSeason(AnimeSeason.Summer, year)) yield return (year, AnimeSeason.Summer);
                if (this.IsInSeason(AnimeSeason.Fall, year)) yield return (year, AnimeSeason.Fall);
            }
        }
    }

    public List<CustomTag> CustomTags
        => RepoFactory.CustomTag.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Tag> AnimeTags
        => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);

    public List<AniDB_Tag> Tags
        => GetAniDBTags();

    public List<AniDB_Tag> GetAniDBTags(bool onlyVerified = true)
        => onlyVerified
            ? AnimeTags
                .Select(tag => RepoFactory.AniDB_Tag.GetByTagID(tag.TagID))
                .WhereNotNull()
                .Where(tag => tag.Verified)
                .ToList()
            : AnimeTags
                .Select(tag => RepoFactory.AniDB_Tag.GetByTagID(tag.TagID))
                .WhereNotNull()
                .ToList();

    public List<SVR_AniDB_Anime_Relation> RelatedAnime
        => RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Similar> SimilarAnime
        => RepoFactory.AniDB_Anime_Similar.GetByAnimeID(AnimeID);

    public List<AniDB_Anime_Character> Characters
        => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID);

    #endregion

    #region Titles

    public List<SVR_AniDB_Anime_Title> Titles
        => RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);

    public string PreferredTitle
    {
        get
        {
            // Get the titles now if they were not provided as an argument.
            var titles = Titles;

            // Check each preferred language in order.
            foreach (var namingLanguage in Languages.PreferredNamingLanguages)
            {
                var thisLanguage = namingLanguage.Language;
                if (thisLanguage == TitleLanguage.Main)
                    return MainTitle;

                // First check the main title.
                var title = titles.FirstOrDefault(t => t.TitleType == TitleType.Main && t.Language == thisLanguage);
                if (title != null) return title.Title;

                // Then check for an official title.
                title = titles.FirstOrDefault(t => t.TitleType == TitleType.Official && t.Language == thisLanguage);
                if (title != null) return title.Title;

                // Then check for _any_ title at all, if there is no main or official title in the language.
                if (Utils.SettingsProvider.GetSettings().Language.UseSynonyms)
                {
                    title = titles.FirstOrDefault(t => t.Language == thisLanguage);
                    if (title != null) return title.Title;
                }
            }

            // Otherwise just use the cached main title.
            return MainTitle;
        }
    }

    #endregion

    #region Images

    [XmlIgnore]
    public string PosterPath
    {
        get
        {
            if (string.IsNullOrEmpty(Picname))
            {
                return string.Empty;
            }

            return Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
        }
    }

    public AniDB_Anime_PreferredImage? PreferredPoster
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, ImageEntityType.Poster);

    public AniDB_Anime_PreferredImage? PreferredBackdrop
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, ImageEntityType.Backdrop);

    public AniDB_Anime_PreferredImage? PreferredBanner
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, ImageEntityType.Banner);

    public string PreferredOrDefaultPosterPath
        => PreferredPoster?.GetImageMetadata() is { } defaultPoster ? defaultPoster.LocalPath! : PosterPath;

    public IImageMetadata PreferredOrDefaultPoster
        => PreferredPoster?.GetImageMetadata() ?? this.GetImageMetadata();


    public IImageMetadata? GetPreferredImageForType(ImageEntityType entityType)
        => RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, entityType)?.GetImageMetadata();

    public IReadOnlyList<IImageMetadata> GetImages(ImageEntityType? entityType = null)
    {
        var preferredImages = (entityType.HasValue ? [RepoFactory.AniDB_Anime_PreferredImage.GetByAnidbAnimeIDAndType(AnimeID, entityType.Value)!] : RepoFactory.AniDB_Anime_PreferredImage.GetByAnimeID(AnimeID))
            .WhereNotNull()
            .Select(preferredImage => preferredImage.GetImageMetadata())
            .WhereNotNull()
            .ToDictionary(image => image.ImageType);
        var images = new List<IImageMetadata>();
        if (!entityType.HasValue || entityType.Value is ImageEntityType.Poster)
        {
            var poster = this.GetImageMetadata(false);
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

    public List<SVR_AniDB_Episode> AniDBEpisodes => RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);

    #endregion

    #region TvDB

    public List<CrossRef_AniDB_TvDB> TvdbSeriesCrossReferences
        => RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AnimeID);

    public List<TvDB_Series> TvDBSeries
        => TvdbSeriesCrossReferences.Select(xref => xref.GetTvDBSeries()).WhereNotNull().ToList();

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            var results = new List<TvDB_Episode>();
            var id = TvdbSeriesCrossReferences.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_Episode.GetBySeriesID(id).OrderBy(a => a.SeasonNumber)
                    .ThenBy(a => a.EpisodeNumber));
            }

            return results;
        }
    }

    public List<TvDB_ImageFanart> TvdbBackdrops
    {
        get
        {
            var results = new List<TvDB_ImageFanart>();
            var id = TvdbSeriesCrossReferences.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_ImageFanart.GetBySeriesID(id));
            }

            return results;
        }
    }

    public List<TvDB_ImageWideBanner> TvdbBanners
    {
        get
        {
            var results = new List<TvDB_ImageWideBanner>();
            var id = TvdbSeriesCrossReferences.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(id));
            }

            return results;
        }
    }

    #endregion

    #region Trakt

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
    {
        return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AnimeID);
    }

    #endregion

    #region TMDB

    public IReadOnlyList<CrossRef_AniDB_TMDB_Show> TmdbShowCrossReferences
        => RepoFactory.CrossRef_AniDB_TMDB_Show.GetByAnidbAnimeID(AnimeID);

    public IReadOnlyList<TMDB_Show> TmdbShows
        => TmdbShowCrossReferences
            .Select(xref => RepoFactory.TMDB_Show.GetByTmdbShowID(xref.TmdbShowID))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<TMDB_Image> TmdbShowBackdrops
        => TmdbShowCrossReferences
            .SelectMany(xref => RepoFactory.TMDB_Image.GetByTmdbShowIDAndType(xref.TmdbShowID, ImageEntityType.Backdrop))
            .ToList();


    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> TmdbMovieCrossReferences
        => RepoFactory.CrossRef_AniDB_TMDB_Movie.GetByAnidbAnimeID(AnimeID);

    public IReadOnlyList<TMDB_Movie> TmdbMovies
        => TmdbMovieCrossReferences
            .Select(xref => RepoFactory.TMDB_Movie.GetByTmdbMovieID(xref.TmdbMovieID))
            .WhereNotNull()
            .ToList();

    public IReadOnlyList<TMDB_Image> TmdbMovieBackdrops
        => TmdbMovieCrossReferences
            .SelectMany(xref => RepoFactory.TMDB_Image.GetByTmdbMovieIDAndType(xref.TmdbMovieID, ImageEntityType.Backdrop))
            .ToList();

    #endregion

    #region MAL

    public List<CrossRef_AniDB_MAL> GetCrossRefMAL()
    {
        return RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);
    }

    #endregion

    #endregion

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => AnimeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => MainTitle;

    string IWithTitles.PreferredTitle => PreferredTitle;

    IReadOnlyList<AnimeTitle> IWithTitles.Titles => Titles
        .Select(a => new AnimeTitle
        {
            Source = DataSourceEnum.AniDB,
            LanguageCode = a.LanguageCode,
            Language = a.Language,
            Title = a.Title,
            Type = a.TitleType,
        })
        .Where(a => a.Type != TitleType.None)
        .ToList();

    #endregion

    #region IWithDescription Implementation

    string IWithDescriptions.DefaultDescription => Description;

    string IWithDescriptions.PreferredDescription => Description;

    IReadOnlyList<TextDescription> IWithDescriptions.Descriptions => [
        new()
        {
            Source = DataSourceEnum.AniDB,
            Language = TitleLanguage.English,
            LanguageCode = "en",
            Value = string.Empty,
        },
    ];

    #endregion

    #region ISeries Implementation

    AnimeType ISeries.Type => (AnimeType)AnimeType;

    IReadOnlyList<int> ISeries.ShokoSeriesIDs => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series.AnimeSeriesID] : [];

    double ISeries.Rating => Rating / 100D;

    bool ISeries.Restricted => Restricted == 1;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series] : [];

    IImageMetadata? ISeries.DefaultPoster => this.GetImageMetadata();

    IReadOnlyList<IRelatedMetadata<ISeries>> ISeries.RelatedSeries =>
        RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

    IReadOnlyList<IRelatedMetadata<IMovie>> ISeries.RelatedMovies => [];

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID);

    IReadOnlyList<IEpisode> ISeries.Episodes => AniDBEpisodes;

    IReadOnlyList<IVideo> ISeries.Videos =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    EpisodeCounts ISeries.EpisodeCounts
    {
        get
        {
            var episodes = (this as ISeries).Episodes;
            return new()
            {
                Episodes = episodes.Count(a => a.Type == AbstractEpisodeType.Episode),
                Credits = episodes.Count(a => a.Type == AbstractEpisodeType.Credits),
                Others = episodes.Count(a => a.Type == AbstractEpisodeType.Other),
                Parodies = episodes.Count(a => a.Type == AbstractEpisodeType.Parody),
                Specials = episodes.Count(a => a.Type == AbstractEpisodeType.Special),
                Trailers = episodes.Count(a => a.Type == AbstractEpisodeType.Trailer)
            };
        }
    }

    #endregion
}
