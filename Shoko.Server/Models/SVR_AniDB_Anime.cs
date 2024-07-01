using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.Repositories;
using Shoko.Server.Utilities;

using AnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using AbstractEpisodeType = Shoko.Plugin.Abstractions.DataModels.EpisodeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Shoko.Plugin.Abstractions.DataModels.Shoko;

namespace Shoko.Server.Models;

public class SVR_AniDB_Anime : AniDB_Anime, IAnime, ISeries
{
    #region Properties and fields

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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

    public List<TvDB_Episode> TvDBEpisodes
    {
        get
        {
            var results = new List<TvDB_Episode>();
            var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_Episode.GetBySeriesID(id).OrderBy(a => a.SeasonNumber)
                    .ThenBy(a => a.EpisodeNumber));
            }

            return results;
        }
    }

    public List<CrossRef_AniDB_TvDB> GetCrossRefTvDB()
    {
        return RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AnimeID);
    }

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
    {
        return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(AnimeID);
    }

    public List<CrossRef_AniDB_MAL> GetCrossRefMAL()
    {
        return RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);
    }

    public List<TvDB_ImageFanart> TvDBImageFanarts
    {
        get
        {
            var results = new List<TvDB_ImageFanart>();
            var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_ImageFanart.GetBySeriesID(id));
            }

            return results;
        }
    }

    public List<TvDB_ImagePoster> TvDBImagePosters
    {
        get
        {
            var results = new List<TvDB_ImagePoster>();
            var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_ImagePoster.GetBySeriesID(id));
            }

            return results;
        }
    }

    public List<TvDB_ImageWideBanner> TvDBImageWideBanners
    {
        get
        {
            var results = new List<TvDB_ImageWideBanner>();
            var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
            {
                results.AddRange(RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(id));
            }

            return results;
        }
    }

    public CrossRef_AniDB_Other CrossRefMovieDB => RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AnimeID, CrossRefType.MovieDB);

    public MovieDB_Movie MovieDBMovie
    {
        get
        {
            var xref = CrossRefMovieDB;
            if (xref == null)
            {
                return null;
            }

            return RepoFactory.MovieDb_Movie.GetByOnlineID(int.Parse(xref.CrossRefID));
        }
    }

    public List<MovieDB_Fanart> MovieDBFanarts
    {
        get
        {
            var xref = CrossRefMovieDB;
            if (xref == null)
            {
                return new List<MovieDB_Fanart>();
            }

            return RepoFactory.MovieDB_Fanart.GetByMovieID(int.Parse(xref.CrossRefID));
        }
    }

    public List<MovieDB_Poster> MovieDBPosters
    {
        get
        {
            var xref = CrossRefMovieDB;
            if (xref == null)
            {
                return new List<MovieDB_Poster>();
            }

            return RepoFactory.MovieDB_Poster.GetByMovieID(int.Parse(xref.CrossRefID));
        }
    }

    public AniDB_Anime_DefaultImage DefaultPoster => RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, ImageSizeType.Poster);

    public string PosterPathNoDefault
    {
        get
        {
            var fileName = Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
            return fileName;
        }
    }

    private List<AniDB_Anime_DefaultImage> allPosters;

    public List<AniDB_Anime_DefaultImage> AllPosters
    {
        get
        {
            if (allPosters != null)
            {
                return allPosters;
            }

            var posters = new List<AniDB_Anime_DefaultImage>();
            posters.Add(new AniDB_Anime_DefaultImage
            {
                AniDB_Anime_DefaultImageID = AnimeID,
                ImageType = (int)ImageEntityType.AniDB_Cover
            });
            var tvdbposters = TvDBImagePosters?.Where(img => img != null).Select(img =>
                new AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = img.TvDB_ImagePosterID,
                    ImageType = (int)ImageEntityType.TvDB_Cover
                });
            if (tvdbposters != null)
            {
                posters.AddRange(tvdbposters);
            }

            var moviebposters = MovieDBPosters?.Where(img => img != null).Select(img =>
                new AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = img.MovieDB_PosterID,
                    ImageType = (int)ImageEntityType.MovieDB_Poster
                });
            if (moviebposters != null)
            {
                posters.AddRange(moviebposters);
            }

            allPosters = posters;
            return posters;
        }
    }

    public List<AniDB_Anime_DefaultImage> AllFanarts
    {
        get
        {
            var fanarts = new List<AniDB_Anime_DefaultImage>();
            var movDbFanart = MovieDBFanarts;
            if (movDbFanart != null && movDbFanart.Any())
            {
                fanarts.AddRange(movDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
                {
                    ImageType = (int)ImageEntityType.MovieDB_FanArt, MovieFanart = a, AniDB_Anime_DefaultImageID = a.MovieDB_FanartID
                }));
            }

            var tvDbFanart = TvDBImageFanarts;
            if (tvDbFanart != null && tvDbFanart.Any())
            {
                fanarts.AddRange(tvDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
                {
                    ImageType = (int)ImageEntityType.TvDB_FanArt, TVFanart = a, AniDB_Anime_DefaultImageID = a.TvDB_ImageFanartID
                }));
            }

            return fanarts;
        }
    }

    public string GetDefaultPosterPathNoBlanks()
    {
        var defaultPoster = DefaultPoster;
        if (defaultPoster == null)
        {
            return PosterPathNoDefault;
        }

        var imageType = (ImageEntityType)defaultPoster.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.AniDB_Cover:
                return PosterPath;

            case ImageEntityType.TvDB_Cover:
                var tvPoster =
                    RepoFactory.TvDB_ImagePoster.GetByID(defaultPoster.ImageParentID);
                if (tvPoster != null)
                {
                    return tvPoster.GetFullImagePath();
                }
                else
                {
                    return PosterPath;
                }

            case ImageEntityType.MovieDB_Poster:
                var moviePoster =
                    RepoFactory.MovieDB_Poster.GetByID(defaultPoster.ImageParentID);
                if (moviePoster != null)
                {
                    return moviePoster.GetFullImagePath();
                }
                else
                {
                    return PosterPath;
                }
        }

        return PosterPath;
    }

    public ImageDetails GetDefaultPosterDetailsNoBlanks()
    {
        var details = new ImageDetails { ImageType = ImageEntityType.AniDB_Cover, ImageID = AnimeID };
        var defaultPoster = DefaultPoster;

        if (defaultPoster == null)
        {
            return details;
        }

        var imageType = (ImageEntityType)defaultPoster.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.AniDB_Cover:
                return details;

            case ImageEntityType.TvDB_Cover:
                var tvPoster =
                    RepoFactory.TvDB_ImagePoster.GetByID(defaultPoster.ImageParentID);
                if (tvPoster != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.TvDB_Cover,
                        ImageID = tvPoster.TvDB_ImagePosterID
                    };
                }

                return details;

            case ImageEntityType.MovieDB_Poster:
                var moviePoster =
                    RepoFactory.MovieDB_Poster.GetByID(defaultPoster.ImageParentID);
                if (moviePoster != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.MovieDB_Poster,
                        ImageID = moviePoster.MovieDB_PosterID
                    };
                }

                return details;
        }

        return details;
    }

    public AniDB_Anime_DefaultImage DefaultFanart => RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, ImageSizeType.Fanart);

    public ImageDetails GetDefaultFanartDetailsNoBlanks()
    {
        var fanartRandom = new Random();

        ImageDetails details = null;
        var fanart = DefaultFanart;
        if (fanart == null)
        {
            var fanarts = AllFanarts;
            if (fanarts.Count == 0) return null;

            var art = fanarts[fanartRandom.Next(0, fanarts.Count)];
            details = new ImageDetails
            {
                ImageID = art.AniDB_Anime_DefaultImageID,
                ImageType = (ImageEntityType)art.ImageType
            };
            return details;
        }

        var imageType = (ImageEntityType)fanart.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.TvDB_FanArt:
                var tvFanart = RepoFactory.TvDB_ImageFanart.GetByID(fanart.ImageParentID);
                if (tvFanart != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.TvDB_FanArt,
                        ImageID = tvFanart.TvDB_ImageFanartID
                    };
                }

                return details;

            case ImageEntityType.MovieDB_FanArt:
                var movieFanart = RepoFactory.MovieDB_Fanart.GetByID(fanart.ImageParentID);
                if (movieFanart != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.MovieDB_FanArt,
                        ImageID = movieFanart.MovieDB_FanartID
                    };
                }

                return details;
        }

        return null;
    }

    public AniDB_Anime_DefaultImage DefaultWideBanner => RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, ImageSizeType.WideBanner);

    [XmlIgnore]
    public string TagsString
    {
        get
        {
            var tags = Tags;
            var temp = string.Empty;
            foreach (var tag in tags)
            {
                temp += tag.TagName + "|";
            }

            if (temp.Length > 2)
            {
                temp = temp.Substring(0, temp.Length - 2);
            }

            return temp;
        }
    }

    public List<AniDB_Tag> Tags
    {
        get
        {
            var tags = new List<AniDB_Tag>();
            foreach (var tag in AnimeTags)
            {
                var newTag = RepoFactory.AniDB_Tag.GetByTagID(tag.TagID);
                if (newTag != null)
                {
                    tags.Add(newTag);
                }
            }

            return tags;
        }
    }

    public IEnumerable<(int Year, AnimeSeason Season)> Seasons
    {
        get
        {
            if (AirDate == null) yield break;

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

    public List<CustomTag> CustomTags => RepoFactory.CustomTag.GetByAnimeID(AnimeID);

    public List<AniDB_Tag> GetAniDBTags(bool onlyVerified = true)
    {
        if (onlyVerified)
            return RepoFactory.AniDB_Tag.GetByAnimeID(AnimeID)
                .Where(tag => tag.Verified)
                .ToList();

        return RepoFactory.AniDB_Tag.GetByAnimeID(AnimeID);
    }

    public List<AniDB_Anime_Tag> AnimeTags => RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);
    public List<SVR_AniDB_Anime_Relation> RelatedAnime => RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);
    public List<AniDB_Anime_Similar> SimilarAnime => RepoFactory.AniDB_Anime_Similar.GetByAnimeID(AnimeID);
    public List<AniDB_Anime_Character> Characters => RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID);
    public List<SVR_AniDB_Anime_Title> Titles => RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);

    private string GetFormattedTitle(List<SVR_AniDB_Anime_Title> titles = null)
    {
        // Get the titles now if they were not provided as an argument.
        titles ??= Titles;

        // Check each preferred language in order.
        foreach (var thisLanguage in Languages.PreferredNamingLanguageNames)
        {
            // First check the main title.
            var title = titles.FirstOrDefault(t => t.TitleType == TitleType.Main && t.Language == thisLanguage);
            if (title != null) return title.Title;

            // Then check for an official title.
            title = titles.FirstOrDefault(t => t.TitleType == TitleType.Official && t.Language == thisLanguage);
            if (title != null) return title.Title;

            // Then check for _any_ title at all, if there is no main or official title in the langugage.
            if (Utils.SettingsProvider.GetSettings().LanguageUseSynonyms)
            {
                title = titles.FirstOrDefault(t => t.Language == thisLanguage);
                if (title != null) return title.Title;
            }
        }

        // Otherwise just use the cached main title.
        return MainTitle;
    }

    [XmlIgnore]
    public AniDB_Vote UserVote
    {
        get
        {
            try
            {
                return RepoFactory.AniDB_Vote.GetByAnimeID(AnimeID);
            }
            catch (Exception ex)
            {
                logger.Error($"Error in  UserVote: {ex}");
                return null;
            }
        }
    }

    private string _cachedTitle;
    public string PreferredTitle => _cachedTitle ??= GetFormattedTitle();

    public List<SVR_AniDB_Episode> AniDBEpisodes => RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);

    #endregion

    public DateTime GetDateTimeUpdated()
    {
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AnimeID);
        return update?.UpdatedAt ?? DateTime.MinValue;
    }

    #region IMetadata Implementation

    DataSourceEnum IMetadata.Source => DataSourceEnum.AniDB;

    int IMetadata<int>.ID => AnimeID;

    #endregion

    #region IWithTitles Implementation

    string IWithTitles.DefaultTitle => MainTitle;

    string IWithTitles.PreferredTitle => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID)?.SeriesName ?? PreferredTitle;

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

    IReadOnlyList<int> ISeries.ShokoGroupIDs => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID)?.AllGroupsAbove.Select(a => a.AnimeGroupID).ToList() ?? [];

    double ISeries.Rating => Rating / 100D;

    bool ISeries.Restricted => Restricted == 1;

    IReadOnlyList<IShokoSeries> ISeries.ShokoSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID) is { } series ? [series] : [];

    IReadOnlyList<IShokoGroup> ISeries.ShokoGroups => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID)?.AllGroupsAbove ?? [];

    IReadOnlyList<ISeries> ISeries.LinkedSeries
    {
        get
        {
            var seriesList = new List<ISeries>();

            var shokoSeries = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
            if (shokoSeries is not null)
                seriesList.Add(shokoSeries);

            // TODO: Add more series here.

            return seriesList;
        }
    }

    IReadOnlyList<IRelatedMetadata<ISeries>> ISeries.RelatedSeries =>
        RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

    IReadOnlyList<IVideoCrossReference> ISeries.CrossReferences =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID);

    IReadOnlyList<IEpisode> ISeries.EpisodeList => AniDBEpisodes;

    IReadOnlyList<IVideo> ISeries.VideoList =>
        RepoFactory.CrossRef_File_Episode.GetByAnimeID(AnimeID)
            .DistinctBy(xref => xref.Hash)
            .Select(xref => xref.VideoLocal)
            .WhereNotNull()
            .ToList();

    EpisodeCounts ISeries.EpisodeCounts => new()
    {
        Episodes = AniDBEpisodes.Count(a => a.EpisodeType == (int)EpisodeType.Episode),
        Credits = AniDBEpisodes.Count(a => a.EpisodeType == (int)EpisodeType.Credits),
        Others = AniDBEpisodes.Count(a => a.EpisodeType == (int)EpisodeType.Other),
        Parodies = AniDBEpisodes.Count(a => a.EpisodeType == (int)EpisodeType.Parody),
        Specials = AniDBEpisodes.Count(a => a.EpisodeType == (int)EpisodeType.Special),
        Trailers = AniDBEpisodes.Count(a => a.EpisodeType == (int)EpisodeType.Trailer)
    };

    #endregion

    #region IAnime Implementation

    IReadOnlyList<IRelatedAnime> IAnime.Relations =>
        RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

    EpisodeCounts IAnime.EpisodeCounts => ((ISeries)this).EpisodeCounts;

    int IAnime.AnimeID => AnimeID;

    #endregion
}
