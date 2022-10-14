using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Commands;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;
using AnimeType = Shoko.Plugin.Abstractions.DataModels.AnimeType;
using EpisodeType = Shoko.Models.Enums.EpisodeType;

namespace Shoko.Server.Models;

public class SVR_AniDB_Anime : AniDB_Anime, IAnime
{
    #region DB columns

    public int ContractVersion { get; set; }
    public byte[] ContractBlob { get; set; }
    public int ContractSize { get; set; }

    public const int CONTRACT_VERSION = 7;

    #endregion

    #region Properties and fields

    private CL_AniDB_AnimeDetailed _contract;

    public virtual CL_AniDB_AnimeDetailed Contract
    {
        get
        {
            if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
            {
                _contract = CompressionHelper.DeserializeObject<CL_AniDB_AnimeDetailed>(ContractBlob,
                    ContractSize);
            }

            return _contract;
        }
        set
        {
            _contract = value;
            ContractBlob = CompressionHelper.SerializeObject(value, out var outsize);
            ContractSize = outsize;
            ContractVersion = CONTRACT_VERSION;
        }
    }

    public void CollectContractMemory()
    {
        _contract = null;
    }

    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    // these files come from AniDB but we don't directly save them
    private string reviewIDListRAW;

    public static IList<string> GetAllReleaseGroups()
    {
        var query =
            @"SELECT g.GroupName
FROM AniDB_File a
INNER JOIN AniDB_ReleaseGroup g ON a.GroupID = g.GroupID
INNER JOIN CrossRef_File_Episode xref1 ON xref1.Hash = a.Hash
GROUP BY g.GroupName
ORDER BY count(DISTINCT xref1.AnimeID) DESC, g.GroupName ASC";
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            var result = session.CreateSQLQuery(query).List<string>();
            if (result.Contains("raw/unknown"))
            {
                result.Remove("raw/unknown");
            }

            return result;
        }
    }


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

    public static void GetRelatedAnimeRecursive(ISessionWrapper session, int animeID,
        ref List<SVR_AniDB_Anime> relList,
        ref List<int> relListIDs, ref List<int> searchedIDs)
    {
        var anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
        searchedIDs.Add(animeID);

        foreach (AniDB_Anime_Relation rel in anime.GetRelatedAnime(session))
        {
            var relationtype = rel.RelationType.ToLower();
            if (SVR_AnimeGroup.IsRelationTypeInExclusions(relationtype))
            {
                //Filter these relations these will fix messes, like Gundam , Clamp, etc.
                continue;
            }

            var relAnime = RepoFactory.AniDB_Anime.GetByAnimeID(session, rel.RelatedAnimeID);
            if (relAnime != null && !relListIDs.Contains(relAnime.AnimeID))
            {
                if (SVR_AnimeGroup.IsRelationTypeInExclusions(relAnime.GetAnimeTypeDescription().ToLower()))
                {
                    continue;
                }

                relList.Add(relAnime);
                relListIDs.Add(relAnime.AnimeID);
                if (!searchedIDs.Contains(rel.RelatedAnimeID))
                {
                    GetRelatedAnimeRecursive(session, rel.RelatedAnimeID, ref relList, ref relListIDs,
                        ref searchedIDs);
                }
            }
        }
    }

    public List<TvDB_Episode> GetTvDBEpisodes()
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

    private Dictionary<int, TvDB_Episode> dictTvDBEpisodes;

    public Dictionary<int, TvDB_Episode> GetDictTvDBEpisodes()
    {
        if (dictTvDBEpisodes == null)
        {
            try
            {
                var tvdbEpisodes = GetTvDBEpisodes();
                if (tvdbEpisodes != null)
                {
                    dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                    // create a dictionary of absolute episode numbers for tvdb episodes
                    // sort by season and episode number
                    // ignore season 0, which is used for specials

                    var i = 1;
                    foreach (var ep in tvdbEpisodes)
                    {
                        dictTvDBEpisodes[i] = ep;
                        i++;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        return dictTvDBEpisodes;
    }

    private Dictionary<int, int> dictTvDBSeasons;

    public Dictionary<int, int> GetDictTvDBSeasons()
    {
        if (dictTvDBSeasons == null)
        {
            try
            {
                dictTvDBSeasons = new Dictionary<int, int>();
                // create a dictionary of season numbers and the first episode for that season
                var i = 1;
                var lastSeason = -999;
                foreach (var ep in GetTvDBEpisodes())
                {
                    if (ep.SeasonNumber != lastSeason)
                    {
                        dictTvDBSeasons[ep.SeasonNumber] = i;
                    }

                    lastSeason = ep.SeasonNumber;
                    i++;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        return dictTvDBSeasons;
    }

    private Dictionary<int, int> dictTvDBSeasonsSpecials;

    public Dictionary<int, int> GetDictTvDBSeasonsSpecials()
    {
        if (dictTvDBSeasonsSpecials == null)
        {
            try
            {
                dictTvDBSeasonsSpecials = new Dictionary<int, int>();
                // create a dictionary of season numbers and the first episode for that season
                var i = 1;
                var lastSeason = -999;
                foreach (var ep in GetTvDBEpisodes())
                {
                    if (ep.SeasonNumber > 0)
                    {
                        continue;
                    }

                    var thisSeason = 0;
                    if (ep.AirsBeforeSeason.HasValue)
                    {
                        thisSeason = ep.AirsBeforeSeason.Value;
                    }

                    if (ep.AirsAfterSeason.HasValue)
                    {
                        thisSeason = ep.AirsAfterSeason.Value;
                    }

                    if (thisSeason != lastSeason)
                    {
                        dictTvDBSeasonsSpecials[thisSeason] = i;
                    }

                    lastSeason = thisSeason;
                    i++;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }
        }

        return dictTvDBSeasonsSpecials;
    }

    public List<CrossRef_AniDB_TvDB_Episode_Override> GetCrossRefTvDBEpisodes()
    {
        return RepoFactory.CrossRef_AniDB_TvDB_Episode_Override.GetByAnimeID(AnimeID);
    }

    public List<CrossRef_AniDB_TvDB> GetCrossRefTvDB()
    {
        return RepoFactory.CrossRef_AniDB_TvDB.GetByAnimeID(AnimeID);
    }

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetCrossRefTraktV2(session);
        }
    }

    public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2(ISession session)
    {
        return RepoFactory.CrossRef_AniDB_TraktV2.GetByAnimeID(session, AnimeID);
    }

    public List<CrossRef_AniDB_MAL> GetCrossRefMAL()
    {
        return RepoFactory.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);
    }

    public TvDB_Series GetTvDBSeries()
    {
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id == -1)
        {
            return null;
        }

        return RepoFactory.TvDB_Series.GetByTvDBID(id);
    }

    public List<TvDB_ImageFanart> GetTvDBImageFanarts()
    {
        var results = new List<TvDB_ImageFanart>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_ImageFanart.GetBySeriesID(id));
        }

        return results;
    }

    public List<TvDB_ImagePoster> GetTvDBImagePosters()
    {
        var results = new List<TvDB_ImagePoster>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_ImagePoster.GetBySeriesID(id));
        }

        return results;
    }

    public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners()
    {
        var results = new List<TvDB_ImageWideBanner>();
        var id = GetCrossRefTvDB()?.FirstOrDefault()?.TvDBID ?? -1;
        if (id != -1)
        {
            results.AddRange(RepoFactory.TvDB_ImageWideBanner.GetBySeriesID(id));
        }

        return results;
    }

    public CrossRef_AniDB_Other GetCrossRefMovieDB()
    {
        return RepoFactory.CrossRef_AniDB_Other.GetByAnimeIDAndType(AnimeID,
            CrossRefType.MovieDB);
    }

    public MovieDB_Movie GetMovieDBMovie()
    {
        var xref = GetCrossRefMovieDB();
        if (xref == null)
        {
            return null;
        }

        return RepoFactory.MovieDb_Movie.GetByOnlineID(int.Parse(xref.CrossRefID));
    }

    public List<MovieDB_Fanart> GetMovieDBFanarts()
    {
        var xref = GetCrossRefMovieDB();
        if (xref == null)
        {
            return new List<MovieDB_Fanart>();
        }

        return RepoFactory.MovieDB_Fanart.GetByMovieID(int.Parse(xref.CrossRefID));
    }

    public List<MovieDB_Poster> GetMovieDBPosters()
    {
        var xref = GetCrossRefMovieDB();
        if (xref == null)
        {
            return new List<MovieDB_Poster>();
        }

        return RepoFactory.MovieDB_Poster.GetByMovieID(int.Parse(xref.CrossRefID));
    }

    public AniDB_Anime_DefaultImage GetDefaultPoster()
    {
        return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, ImageSizeType.Poster);
    }

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
                AniDB_Anime_DefaultImageID = AnimeID, ImageType = (int)ImageEntityType.AniDB_Cover
            });
            var tvdbposters = GetTvDBImagePosters()?.Where(img => img != null).Select(img =>
                new AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = img.TvDB_ImagePosterID, ImageType = (int)ImageEntityType.TvDB_Cover
                });
            if (tvdbposters != null)
            {
                posters.AddRange(tvdbposters);
            }

            var moviebposters = GetMovieDBPosters()?.Where(img => img != null).Select(img =>
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

    public string GetDefaultPosterPathNoBlanks()
    {
        var defaultPoster = GetDefaultPoster();
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
        var defaultPoster = GetDefaultPoster();

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
                        ImageType = ImageEntityType.TvDB_Cover, ImageID = tvPoster.TvDB_ImagePosterID
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
                        ImageType = ImageEntityType.MovieDB_Poster, ImageID = moviePoster.MovieDB_PosterID
                    };
                }

                return details;
        }

        return details;
    }

    public AniDB_Anime_DefaultImage GetDefaultFanart()
    {
        return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, ImageSizeType.Fanart);
    }

    public ImageDetails GetDefaultFanartDetailsNoBlanks()
    {
        var fanartRandom = new Random();

        ImageDetails details = null;
        var fanart = GetDefaultFanart();
        if (fanart == null)
        {
            var fanarts = Contract.AniDBAnime.Fanarts;
            if (fanarts == null || fanarts.Count == 0)
            {
                return null;
            }

            var art = fanarts[fanartRandom.Next(0, fanarts.Count)];
            details = new ImageDetails
            {
                ImageID = art.AniDB_Anime_DefaultImageID, ImageType = (ImageEntityType)art.ImageType
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
                        ImageType = ImageEntityType.TvDB_FanArt, ImageID = tvFanart.TvDB_ImageFanartID
                    };
                }

                return details;

            case ImageEntityType.MovieDB_FanArt:
                var movieFanart = RepoFactory.MovieDB_Fanart.GetByID(fanart.ImageParentID);
                if (movieFanart != null)
                {
                    details = new ImageDetails
                    {
                        ImageType = ImageEntityType.MovieDB_FanArt, ImageID = movieFanart.MovieDB_FanartID
                    };
                }

                return details;
        }

        return null;
    }

    public string GetDefaultFanartOnlineURL()
    {
        var fanartRandom = new Random();


        if (GetDefaultFanart() == null)
        {
            // get a random fanart
            if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
            {
                var fanarts = GetMovieDBFanarts();
                if (fanarts.Count == 0)
                {
                    return string.Empty;
                }

                var movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                return movieFanart.URL;
            }
            else
            {
                var fanarts = GetTvDBImageFanarts();
                if (fanarts.Count == 0)
                {
                    return null;
                }

                var tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
            }
        }

        var fanart = GetDefaultFanart();
        var imageType = (ImageEntityType)fanart.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.TvDB_FanArt:
                var tvFanart =
                    RepoFactory.TvDB_ImageFanart.GetByID(fanart.ImageParentID);
                if (tvFanart != null)
                {
                    return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
                }

                break;

            case ImageEntityType.MovieDB_FanArt:
                var movieFanart =
                    RepoFactory.MovieDB_Fanart.GetByID(fanart.ImageParentID);
                if (movieFanart != null)
                {
                    return movieFanart.URL;
                }

                break;
        }

        return string.Empty;
    }

    public AniDB_Anime_DefaultImage GetDefaultWideBanner()
    {
        return RepoFactory.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, ImageSizeType.WideBanner);
    }

    public ImageDetails GetDefaultWideBannerDetailsNoBlanks()
    {
        var bannerRandom = new Random();

        ImageDetails details;
        var banner = GetDefaultWideBanner();
        if (banner == null)
        {
            // get a random banner (only tvdb)
            if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
            {
                // MovieDB doesn't have banners
                return null;
            }

            var banners = Contract.AniDBAnime.Banners;
            if (banners == null || banners.Count == 0)
            {
                return null;
            }

            var art = banners[bannerRandom.Next(0, banners.Count)];
            details = new ImageDetails
            {
                ImageID = art.AniDB_Anime_DefaultImageID, ImageType = (ImageEntityType)art.ImageType
            };
            return details;
        }

        var imageType = (ImageEntityType)banner.ImageParentType;

        switch (imageType)
        {
            case ImageEntityType.TvDB_Banner:
                details = new ImageDetails
                {
                    ImageType = ImageEntityType.TvDB_Banner,
                    ImageID = banner.ToClient().TVWideBanner.TvDB_ImageWideBannerID
                };
                return details;
        }

        return null;
    }


    [XmlIgnore]
    public string TagsString
    {
        get
        {
            var tags = GetTags();
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


    public List<AniDB_Tag> GetTags()
    {
        var tags = new List<AniDB_Tag>();
        foreach (var tag in GetAnimeTags())
        {
            var newTag = RepoFactory.AniDB_Tag.GetByTagID(tag.TagID);
            if (newTag != null)
            {
                tags.Add(newTag);
            }
        }

        return tags;
    }

    public List<CustomTag> GetCustomTagsForAnime()
    {
        return RepoFactory.CustomTag.GetByAnimeID(AnimeID);
    }

    public List<AniDB_Tag> GetAniDBTags()
    {
        return RepoFactory.AniDB_Tag.GetByAnimeID(AnimeID);
    }

    public List<AniDB_Anime_Tag> GetAnimeTags()
    {
        return RepoFactory.AniDB_Anime_Tag.GetByAnimeID(AnimeID);
    }

    public List<SVR_AniDB_Anime_Relation> GetRelatedAnime()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetRelatedAnime(session.Wrap());
        }
    }

    public List<SVR_AniDB_Anime_Relation> GetRelatedAnime(ISessionWrapper session)
    {
        return RepoFactory.AniDB_Anime_Relation.GetByAnimeID(session, AnimeID);
    }

    public List<AniDB_Anime_Similar> GetSimilarAnime()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetSimilarAnime(session);
        }
    }

    public List<AniDB_Anime_Similar> GetSimilarAnime(ISession session)
    {
        return RepoFactory.AniDB_Anime_Similar.GetByAnimeID(session, AnimeID);
    }

    public List<SVR_AniDB_Anime> GetAllRelatedAnime()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetAllRelatedAnime(session.Wrap());
        }
    }

    public List<SVR_AniDB_Anime> GetAllRelatedAnime(ISessionWrapper session)
    {
        var relList = new List<SVR_AniDB_Anime>();
        var relListIDs = new List<int>();
        var searchedIDs = new List<int>();

        GetRelatedAnimeRecursive(session, AnimeID, ref relList, ref relListIDs, ref searchedIDs);
        return relList;
    }

    public List<AniDB_Anime_Character> GetAnimeCharacters()
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return GetAnimeCharacters(session.Wrap());
        }
    }

    public List<AniDB_Anime_Character> GetAnimeCharacters(ISessionWrapper session)
    {
        return RepoFactory.AniDB_Anime_Character.GetByAnimeID(session, AnimeID);
    }

    public List<SVR_AniDB_Anime_Title> GetTitles()
    {
        return RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);
    }

    public string GetFormattedTitle(List<SVR_AniDB_Anime_Title> titles)
    {
        foreach (var nlan in Languages.PreferredNamingLanguages)
        {
            var thisLanguage = nlan.Language;

            // Romaji and English titles will be contained in MAIN and/or OFFICIAL
            // we won't use synonyms for these two languages
            if (thisLanguage == TitleLanguage.Romaji || thisLanguage == TitleLanguage.English)
            {
                foreach (var title in titles)
                {
                    // first try the  Main title
                    if (title.TitleType == TitleType.Main && title.Language == thisLanguage)
                    {
                        return title.Title;
                    }
                }
            }

            // now try the official title
            foreach (var title in titles)
            {
                if (title.TitleType == TitleType.Official && title.Language == thisLanguage)
                {
                    return title.Title;
                }
            }

            // try synonyms
            if (ServerSettings.Instance.LanguageUseSynonyms)
            {
                foreach (var title in titles)
                {
                    if (title.TitleType == TitleType.Synonym && title.Language == thisLanguage)
                    {
                        return title.Title;
                    }
                }
            }
        }

        // otherwise just use the main title
        return MainTitle;
    }

    public string GetFormattedTitle()
    {
        var thisTitles = GetTitles();
        return GetFormattedTitle(thisTitles);
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

    public string PreferredTitle => GetFormattedTitle();


    [XmlIgnore] public List<AniDB_Episode> AniDBEpisodes => RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);

    public List<AniDB_Episode> GetAniDBEpisodes()
    {
        return RepoFactory.AniDB_Episode.GetByAnimeID(AnimeID);
    }

    #endregion

    public SVR_AniDB_Anime()
    {
        DisableExternalLinksFlag = 0;
    }

    #region Init and Populate

    public SVR_AnimeSeries CreateAnimeSeriesAndGroup(SVR_AnimeSeries existingSeries = null, int? existingGroupID = null)
    {
        using (var session = DatabaseFactory.SessionFactory.OpenSession())
        {
            return CreateAnimeSeriesAndGroup(session.Wrap(), existingSeries, existingGroupID);
        }
    }

    public SVR_AnimeSeries CreateAnimeSeriesAndGroup(ISessionWrapper session, SVR_AnimeSeries existingSeries = null,
        int? existingGroupID = null)
    {
        var commandFactory = ShokoServer.ServiceContainer.GetRequiredService<ICommandRequestFactory>();
        // Create a new AnimeSeries record
        var series = existingSeries ?? new SVR_AnimeSeries();

        series.Populate(this);
        // Populate before making a group to ensure IDs and stats are set for group filters.
        RepoFactory.AnimeSeries.Save(series, false, false, true);

        if (existingGroupID == null)
        {
            var grp = new AnimeGroupCreator().GetOrCreateSingleGroupForSeries(session, series);
            series.AnimeGroupID = grp.AnimeGroupID;
        }
        else
        {
            var grp = RepoFactory.AnimeGroup.GetByID(existingGroupID.Value) ??
                      new AnimeGroupCreator().GetOrCreateSingleGroupForSeries(session, series);
            series.AnimeGroupID = grp.AnimeGroupID;
        }

        RepoFactory.AnimeSeries.Save(series, false, false, true);

        // check for TvDB associations
        if (Restricted == 0)
        {
            if (ServerSettings.Instance.TvDB.AutoLink)
            {
                var cmd = commandFactory.Create<CommandRequest_TvDBSearchAnime>(c => c.AnimeID = AnimeID);
                cmd.Save();
            }

            // check for Trakt associations
            if (ServerSettings.Instance.TraktTv.Enabled &&
                !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
            {
                var cmd = commandFactory.Create<CommandRequest_TraktSearchAnime>(c => c.AnimeID = AnimeID);
                cmd.Save();
            }

            if (AnimeType == (int)Shoko.Models.Enums.AnimeType.Movie)
            {
                var cmd = commandFactory.Create<CommandRequest_MovieDBSearchAnime>(c => c.AnimeID = AnimeID);
                cmd.Save();
            }
        }

        return series;
    }

    #endregion

    #region Contracts

    private CL_AniDB_Anime GenerateContract(List<SVR_AniDB_Anime_Title> titles)
    {
        var characters = GetCharactersContract();

        var movDbFanart = GetMovieDBFanarts();
        var tvDbFanart = GetTvDBImageFanarts();
        var tvDbBanners = GetTvDBImageWideBanners();

        var cl = GenerateContract(titles, null, characters, movDbFanart, tvDbFanart, tvDbBanners);
        var defFanart = GetDefaultFanart();
        var defPoster = GetDefaultPoster();
        var defBanner = GetDefaultWideBanner();

        cl.DefaultImageFanart = defFanart?.ToClient();
        cl.DefaultImagePoster = defPoster?.ToClient();
        cl.DefaultImageWideBanner = defBanner?.ToClient();

        return cl;
    }

    private CL_AniDB_Anime GenerateContract(List<SVR_AniDB_Anime_Title> titles, DefaultAnimeImages defaultImages,
        List<CL_AniDB_Character> characters, IEnumerable<MovieDB_Fanart> movDbFanart,
        IEnumerable<TvDB_ImageFanart> tvDbFanart,
        IEnumerable<TvDB_ImageWideBanner> tvDbBanners)
    {
        var cl = this.ToClient();
        cl.FormattedTitle = GetFormattedTitle(titles);
        cl.Characters = characters;

        if (defaultImages != null)
        {
            cl.DefaultImageFanart = defaultImages.Fanart?.ToContract();
            cl.DefaultImagePoster = defaultImages.Poster?.ToContract();
            cl.DefaultImageWideBanner = defaultImages.WideBanner?.ToContract();
        }

        cl.Fanarts = new List<CL_AniDB_Anime_DefaultImage>();
        if (movDbFanart != null && movDbFanart.Any())
        {
            cl.Fanarts.AddRange(movDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
            {
                ImageType = (int)ImageEntityType.MovieDB_FanArt,
                MovieFanart = a,
                AniDB_Anime_DefaultImageID = a.MovieDB_FanartID
            }));
        }

        if (tvDbFanart != null && tvDbFanart.Any())
        {
            cl.Fanarts.AddRange(tvDbFanart.Select(a => new CL_AniDB_Anime_DefaultImage
            {
                ImageType = (int)ImageEntityType.TvDB_FanArt,
                TVFanart = a,
                AniDB_Anime_DefaultImageID = a.TvDB_ImageFanartID
            }));
        }

        cl.Banners = tvDbBanners?.Select(a => new CL_AniDB_Anime_DefaultImage
            {
                ImageType = (int)ImageEntityType.TvDB_Banner,
                TVWideBanner = a,
                AniDB_Anime_DefaultImageID = a.TvDB_ImageWideBannerID
            })
            .ToList();

        if (cl.Fanarts?.Count == 0)
        {
            cl.Fanarts = null;
        }

        if (cl.Banners?.Count == 0)
        {
            cl.Banners = null;
        }

        return cl;
    }

    public List<CL_AniDB_Character> GetCharactersContract()
    {
        var chars = new List<CL_AniDB_Character>();

        try
        {
            var animeChars = RepoFactory.AniDB_Anime_Character.GetByAnimeID(AnimeID);
            if (animeChars == null || animeChars.Count == 0)
            {
                return chars;
            }

            foreach (var animeChar in animeChars)
            {
                var chr = RepoFactory.AniDB_Character.GetByCharID(animeChar.CharID);
                if (chr != null)
                {
                    chars.Add(chr.ToClient(animeChar.CharType));
                }
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, ex.ToString());
        }

        return chars;
    }

    public static void UpdateContractDetailedBatch(ISessionWrapper session,
        IReadOnlyCollection<SVR_AniDB_Anime> animeColl)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (animeColl == null)
        {
            throw new ArgumentNullException(nameof(animeColl));
        }

        var animeIds = animeColl.Select(a => a.AnimeID).ToArray();

        var titlesByAnime = RepoFactory.AniDB_Anime_Title.GetByAnimeIDs(session, animeIds);
        var animeTagsByAnime = RepoFactory.AniDB_Anime_Tag.GetByAnimeIDs(animeIds);
        var tagsByAnime = RepoFactory.AniDB_Tag.GetByAnimeIDs(animeIds);
        var custTagsByAnime = RepoFactory.CustomTag.GetByAnimeIDs(session, animeIds);
        var voteByAnime = RepoFactory.AniDB_Vote.GetByAnimeIDs(animeIds);
        var audioLangByAnime = RepoFactory.Adhoc.GetAudioLanguageStatsByAnime(session, animeIds);
        var subtitleLangByAnime = RepoFactory.Adhoc.GetSubtitleLanguageStatsByAnime(session, animeIds);
        var vidQualByAnime = RepoFactory.Adhoc.GetAllVideoQualityByAnime(session, animeIds);
        var epVidQualByAnime = RepoFactory.Adhoc.GetEpisodeVideoQualityStatsByAnime(session, animeIds);
        var defImagesByAnime = RepoFactory.AniDB_Anime.GetDefaultImagesByAnime(session, animeIds);
        var charsByAnime = RepoFactory.AniDB_Character.GetCharacterAndSeiyuuByAnime(session, animeIds);
        var movDbFanartByAnime = RepoFactory.MovieDB_Fanart.GetByAnimeIDs(session, animeIds);
        var tvDbBannersByAnime = RepoFactory.TvDB_ImageWideBanner.GetByAnimeIDs(session, animeIds);
        var tvDbFanartByAnime = RepoFactory.TvDB_ImageFanart.GetByAnimeIDs(session, animeIds);

        foreach (var anime in animeColl)
        {
            var contract = new CL_AniDB_AnimeDetailed();
            var animeTitles = titlesByAnime[anime.AnimeID];

            defImagesByAnime.TryGetValue(anime.AnimeID, out var defImages);

            var characterContracts = charsByAnime[anime.AnimeID].Select(ac => ac.ToClient()).ToList();
            var movieDbFanart = movDbFanartByAnime[anime.AnimeID];
            var tvDbBanners = tvDbBannersByAnime[anime.AnimeID];
            var tvDbFanart = tvDbFanartByAnime[anime.AnimeID];

            contract.AniDBAnime = anime.GenerateContract(animeTitles.ToList(), defImages, characterContracts,
                movieDbFanart, tvDbFanart, tvDbBanners);

            // Anime titles
            contract.AnimeTitles = titlesByAnime[anime.AnimeID]
                .Select(t => new CL_AnimeTitle
                {
                    AnimeID = t.AnimeID,
                    Language = t.LanguageCode,
                    Title = t.Title,
                    TitleType = t.TitleType.ToString().ToLower()
                })
                .ToList();

            // Seasons
            if (anime.AirDate != null)
            {
                var beginYear = anime.AirDate.Value.Year;
                var endYear = anime.EndDate?.Year ?? DateTime.Today.Year;
                for (var year = beginYear; year <= endYear; year++)
                {
                    foreach (AnimeSeason season in Enum.GetValues(typeof(AnimeSeason)))
                    {
                        if (anime.IsInSeason(season, year))
                        {
                            contract.Stat_AllSeasons.Add($"{season} {year}");
                        }
                    }
                }
            }

            // Anime tags
            var dictAnimeTags = animeTagsByAnime[anime.AnimeID]
                .ToDictionary(t => t.TagID);

            contract.Tags = tagsByAnime[anime.AnimeID]
                .Select(t =>
                {
                    var ctag = new CL_AnimeTag
                    {
                        GlobalSpoiler = t.GlobalSpoiler,
                        LocalSpoiler = t.LocalSpoiler,
                        TagDescription = t.TagDescription,
                        TagID = t.TagID,
                        TagName = t.TagName,
                        Weight = dictAnimeTags.TryGetValue(t.TagID, out var animeTag) ? animeTag.Weight : 0
                    };

                    return ctag;
                })
                .ToList();

            // Custom tags
            contract.CustomTags = custTagsByAnime[anime.AnimeID];

            // Vote

            if (voteByAnime.TryGetValue(anime.AnimeID, out var vote))
            {
                contract.UserVote = vote;
            }


            // Subtitle languages
            contract.Stat_AudioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (audioLangByAnime.TryGetValue(anime.AnimeID, out var langStat))
            {
                contract.Stat_AudioLanguages.UnionWith(langStat.LanguageNames);
            }

            // Audio languages
            contract.Stat_SubtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (subtitleLangByAnime.TryGetValue(anime.AnimeID, out langStat))
            {
                contract.Stat_SubtitleLanguages.UnionWith(langStat.LanguageNames);
            }

            // Anime video quality

            contract.Stat_AllVideoQuality = vidQualByAnime.TryGetValue(anime.AnimeID, out var vidQual)
                ? vidQual
                : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            // Episode video quality

            contract.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (epVidQualByAnime.TryGetValue(anime.AnimeID, out var vidQualStat) &&
                vidQualStat.VideoQualityEpisodeCount.Count > 0)
            {
                contract.Stat_AllVideoQuality_Episodes.UnionWith(vidQualStat.VideoQualityEpisodeCount
                    .Where(kvp => kvp.Value >= anime.EpisodeCountNormal)
                    .Select(kvp => kvp.Key));
            }

            anime.Contract = contract;
        }
    }

    public void UpdateContractDetailed(ISessionWrapper session)
    {
        var animeTitles = RepoFactory.AniDB_Anime_Title.GetByAnimeID(AnimeID);
        var cl = new CL_AniDB_AnimeDetailed
        {
            AniDBAnime = GenerateContract(animeTitles),
            AnimeTitles = new List<CL_AnimeTitle>(),
            Tags = new List<CL_AnimeTag>(),
            CustomTags = new List<CustomTag>()
        };

        // get all the anime titles
        if (animeTitles != null)
        {
            foreach (var title in animeTitles)
            {
                var ctitle = new CL_AnimeTitle
                {
                    AnimeID = title.AnimeID,
                    Language = title.LanguageCode,
                    Title = title.Title,
                    TitleType = title.TitleType.ToString().ToLower()
                };
                cl.AnimeTitles.Add(ctitle);
            }
        }

        if (AirDate != null)
        {
            var beginYear = AirDate.Value.Year;
            var endYear = EndDate?.Year ?? DateTime.Today.Year;
            for (var year = beginYear; year <= endYear; year++)
            {
                foreach (AnimeSeason season in Enum.GetValues(typeof(AnimeSeason)))
                {
                    if (this.IsInSeason(season, year))
                    {
                        cl.Stat_AllSeasons.Add($"{season} {year}");
                    }
                }
            }
        }

        var dictAnimeTags = new Dictionary<int, AniDB_Anime_Tag>();
        foreach (var animeTag in GetAnimeTags())
        {
            dictAnimeTags[animeTag.TagID] = animeTag;
        }

        foreach (var tag in GetAniDBTags())
        {
            var ctag = new CL_AnimeTag
            {
                GlobalSpoiler = tag.GlobalSpoiler,
                LocalSpoiler = tag.LocalSpoiler,
                //ctag.Spoiler = tag.Spoiler;
                //ctag.TagCount = tag.TagCount;
                TagDescription = tag.TagDescription,
                TagID = tag.TagID,
                TagName = tag.TagName
            };
            if (dictAnimeTags.ContainsKey(tag.TagID))
            {
                ctag.Weight = dictAnimeTags[tag.TagID].Weight;
            }
            else
            {
                ctag.Weight = 0;
            }

            cl.Tags.Add(ctag);
        }


        // Get all the custom tags
        foreach (var custag in GetCustomTagsForAnime())
        {
            cl.CustomTags.Add(custag);
        }

        if (UserVote != null)
        {
            cl.UserVote = UserVote;
        }

        var audioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        var subtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        //logger.Trace(" XXXX 06");

        // audio languages
        var dicAudio =
            RepoFactory.Adhoc.GetAudioLanguageStatsByAnime(session, AnimeID);
        foreach (var kvp in dicAudio)
        {
            foreach (var lanName in kvp.Value.LanguageNames)
            {
                if (!audioLanguages.Contains(lanName))
                {
                    audioLanguages.Add(lanName);
                }
            }
        }

        //logger.Trace(" XXXX 07");

        // subtitle languages
        var dicSubtitle =
            RepoFactory.Adhoc.GetSubtitleLanguageStatsByAnime(session, AnimeID);
        foreach (var kvp in dicSubtitle)
        {
            foreach (var lanName in kvp.Value.LanguageNames)
            {
                if (!subtitleLanguages.Contains(lanName))
                {
                    subtitleLanguages.Add(lanName);
                }
            }
        }

        //logger.Trace(" XXXX 08");

        cl.Stat_AudioLanguages = audioLanguages;

        //logger.Trace(" XXXX 09");

        cl.Stat_SubtitleLanguages = subtitleLanguages;

        //logger.Trace(" XXXX 10");
        cl.Stat_AllVideoQuality = RepoFactory.Adhoc.GetAllVideoQualityForAnime(session, AnimeID);

        var stat = RepoFactory.Adhoc.GetEpisodeVideoQualityStatsForAnime(session, AnimeID);
        cl.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        if (stat != null && stat.VideoQualityEpisodeCount.Count > 0)
        {
            foreach (var kvp in stat.VideoQualityEpisodeCount)
            {
                if (kvp.Value >= EpisodeCountNormal)
                {
                    cl.Stat_AllVideoQuality_Episodes.Add(kvp.Key);
                }
            }
        }

        //logger.Trace(" XXXX 11");

        Contract = cl;
    }

    #endregion

    public static void UpdateStatsByAnimeID(int id)
    {
        var an = RepoFactory.AniDB_Anime.GetByAnimeID(id);
        if (an != null)
        {
            RepoFactory.AniDB_Anime.Save(an);
        }

        var series = RepoFactory.AnimeSeries.GetByAnimeID(id);
        // Updating stats saves everything and updates groups
        series?.UpdateStats(true, true, true);
    }

    public DateTime GetDateTimeUpdated()
    {
        var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(AnimeID);
        return update?.UpdatedAt ?? DateTime.MinValue;
    }

    AnimeType IAnime.Type => (AnimeType)AnimeType;

    IReadOnlyList<AnimeTitle> IAnime.Titles => GetTitles()
        .Select(a => new AnimeTitle
            {
                LanguageCode = a.LanguageCode, Language = a.Language, Title = a.Title, Type = a.TitleType
            }
        )
        .Where(a => a.Type != TitleType.None)
        .ToList();

    double IAnime.Rating => Rating / 100D;

    EpisodeCounts IAnime.EpisodeCounts => new()
    {
        Episodes = GetAniDBEpisodes().Count(a => a.EpisodeType == (int)EpisodeType.Episode),
        Credits = GetAniDBEpisodes().Count(a => a.EpisodeType == (int)EpisodeType.Credits),
        Others = GetAniDBEpisodes().Count(a => a.EpisodeType == (int)EpisodeType.Other),
        Parodies = GetAniDBEpisodes().Count(a => a.EpisodeType == (int)EpisodeType.Parody),
        Specials = GetAniDBEpisodes().Count(a => a.EpisodeType == (int)EpisodeType.Special),
        Trailers = GetAniDBEpisodes().Count(a => a.EpisodeType == (int)EpisodeType.Trailer)
    };

    string IAnime.PreferredTitle => RepoFactory.AnimeSeries.GetByAnimeID(AnimeID)?.GetSeriesName() ?? PreferredTitle;
    bool IAnime.Restricted => Restricted == 1;
    IReadOnlyList<IRelatedAnime> IAnime.Relations => RepoFactory.AniDB_Anime_Relation.GetByAnimeID(AnimeID);
}
