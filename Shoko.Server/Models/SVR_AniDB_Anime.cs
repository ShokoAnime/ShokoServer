using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using AniDBAPI;
using NLog;
using Shoko.Commons.Extensions;
using Shoko.Commons.Utils;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Repositories;
using Shoko.Server.Repositories.Repos;
using Shoko.Server.Tasks;

namespace Shoko.Server.Models
{
    public class SVR_AniDB_Anime : AniDB_Anime
    {
        #region DB columns

        public int ContractVersion { get; set; }
        public byte[] ContractBlob { get; set; }
        public int ContractSize { get; set; }

        public const int CONTRACT_VERSION = 7;

        #endregion

        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private CL_AniDB_AnimeDetailed _contract;

        private List<AniDB_Anime_DefaultImage> allPosters;

        private Dictionary<int, TvDB_Episode> dictTvDBEpisodes;

        private Dictionary<int, int> dictTvDBSeasons;

        private Dictionary<int, int> dictTvDBSeasonsSpecials;

        // these files come from AniDB but we don't directly save them
        private string reviewIDListRAW;

        public SVR_AniDB_Anime()
        {
            DisableExternalLinksFlag = 0;
        }

        [XmlIgnore]
        [NotMapped]
        public virtual CL_AniDB_AnimeDetailed Contract
        {
            get
            {
                if (_contract == null && ContractBlob != null && ContractBlob.Length > 0 && ContractSize > 0)
                    _contract = CompressionHelper.DeserializeObject<CL_AniDB_AnimeDetailed>(ContractBlob,
                        ContractSize);
                return _contract;
            }
            set
            {
                _contract = value;
                ContractBlob = CompressionHelper.SerializeObject(value, out int outsize);
                ContractSize = outsize;
                ContractVersion = CONTRACT_VERSION;
            }
        }

        [XmlIgnore]
        [NotMapped]
        public string PosterPath
        {
            get
            {
                if (string.IsNullOrEmpty(Picname)) return string.Empty;
                return Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
            }
        }

        [NotMapped]
        public string PosterPathNoDefault
        {
            get
            {
                string fileName = Path.Combine(ImageUtils.GetAniDBImagePath(AnimeID), Picname);
                return fileName;
            }
        }

        [NotMapped]
        public List<AniDB_Anime_DefaultImage> AllPosters
        {
            get
            {
                if (allPosters != null) return allPosters;
                var posters = new List<AniDB_Anime_DefaultImage>();
                posters.Add(new AniDB_Anime_DefaultImage
                {
                    AniDB_Anime_DefaultImageID = AnimeID,
                    ImageType = (int) ImageEntityType.AniDB_Cover
                });
                var tvdbposters = GetTvDBImagePosters()?.Where(img => img != null).Select(img =>
                    new AniDB_Anime_DefaultImage
                    {
                        AniDB_Anime_DefaultImageID = img.TvDB_ImagePosterID,
                        ImageType = (int) ImageEntityType.TvDB_Cover
                    });
                if (tvdbposters != null) posters.AddRange(tvdbposters);

                var moviebposters = GetMovieDBPosters()?.Where(img => img != null).Select(img =>
                    new AniDB_Anime_DefaultImage
                    {
                        AniDB_Anime_DefaultImageID = img.MovieDB_PosterID,
                        ImageType = (int) ImageEntityType.MovieDB_Poster
                    });
                if (moviebposters != null) posters.AddRange(moviebposters);

                allPosters = posters;
                return posters;
            }
        }


        [XmlIgnore]
        [NotMapped]
        public string TagsString
        {
            get
            {
                List<AniDB_Tag> tags = GetTags();
                string temp = string.Empty;
                foreach (AniDB_Tag tag in tags)
                    temp += tag.TagName + "|";
                if (temp.Length > 2)
                    temp = temp.Substring(0, temp.Length - 2);
                return temp;
            }
        }

        [XmlIgnore]
        [NotMapped]
        public List<AniDB_Anime_Review> AnimeReviews => Repo.AniDB_Anime_Review.GetByAnimeID(AnimeID);

        [XmlIgnore]
        [NotMapped]
        public AniDB_Vote UserVote
        {
            get
            {
                try
                {
                    return Repo.AniDB_Vote.GetByAnimeID(AnimeID);
                }
                catch (Exception ex)
                {
                    logger.Error($"Error in  UserVote: {ex}");
                    return null;
                }
            }
        }

        [NotMapped]
        public string PreferredTitle
        {
            get
            {
                List<AniDB_Anime_Title> titles = GetTitles();

                foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
                {
                    string thisLanguage = nlan.Language.Trim().ToUpper();
                    // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                    // we won't use synonyms for these two languages
                    if (thisLanguage == "X-JAT" || thisLanguage == "EN")
                        for (int i = 0; i < titles.Count; i++)
                            if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                                titles[i].TitleType.Trim().ToUpper() ==
                                Shoko.Models.Constants.AnimeTitleType.Main.ToUpper())
                                return titles[i].Title;

                    // now try the official title
                    for (int i = 0; i < titles.Count; i++)
                        if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                            titles[i].TitleType.Trim().ToUpper() ==
                            Shoko.Models.Constants.AnimeTitleType.Official.ToUpper())
                            return titles[i].Title;

                    // try synonyms
                    if (ServerSettings.LanguageUseSynonyms)
                        for (int i = 0; i < titles.Count; i++)
                            if (titles[i].Language.Trim().ToUpper() == thisLanguage &&
                                titles[i].TitleType.Trim().ToUpper() ==
                                Shoko.Models.Constants.AnimeTitleType.Synonym.ToUpper())
                                return titles[i].Title;
                }

                // otherwise just use the main title
                for (int i = 0; i < titles.Count; i++)
                    if (titles[i].TitleType.Trim().ToUpper() == Shoko.Models.Constants.AnimeTitleType.Main.ToUpper())
                        return titles[i].Title;

                return "ERROR";
            }
        }

        [NotMapped]
        [XmlIgnore]
        public List<AniDB_Episode> AniDBEpisodes => Repo.AniDB_Episode.GetByAnimeID(AnimeID);

        public void CollectContractMemory()
        {
            _contract = null;
        }

        public List<TvDB_Episode> GetTvDBEpisodes()
        {
            List<TvDB_Episode> results = new List<TvDB_Episode>();
            int id = GetCrossRefTvDBV2()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
                results.AddRange(Repo.TvDB_Episode.GetBySeriesID(id).OrderBy(a => a.SeasonNumber)
                    .ThenBy(a => a.EpisodeNumber));
            return results;
        }

        public Dictionary<int, TvDB_Episode> GetDictTvDBEpisodes()
        {
            if (dictTvDBEpisodes == null)
            {
                try
                {
                    List<TvDB_Episode> tvdbEpisodes = GetTvDBEpisodes();
                    if (tvdbEpisodes != null)
                    {
                        dictTvDBEpisodes = new Dictionary<int, TvDB_Episode>();
                        // create a dictionary of absolute episode numbers for tvdb episodes
                        // sort by season and episode number
                        // ignore season 0, which is used for specials

                        int i = 1;
                        foreach (TvDB_Episode ep in tvdbEpisodes)
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

        public Dictionary<int, int> GetDictTvDBSeasons()
        {
            if (dictTvDBSeasons == null)
            {
                try
                {
                    dictTvDBSeasons = new Dictionary<int, int>();
                    // create a dictionary of season numbers and the first episode for that season
                    int i = 1;
                    int lastSeason = -999;
                    foreach (TvDB_Episode ep in GetTvDBEpisodes())
                    {
                        if (ep.SeasonNumber != lastSeason)
                            dictTvDBSeasons[ep.SeasonNumber] = i;

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

        public Dictionary<int, int> GetDictTvDBSeasonsSpecials()
        {
            if (dictTvDBSeasonsSpecials == null)
            {
                try
                {
                    dictTvDBSeasonsSpecials = new Dictionary<int, int>();
                    // create a dictionary of season numbers and the first episode for that season
                    int i = 1;
                    int lastSeason = -999;
                    foreach (TvDB_Episode ep in GetTvDBEpisodes())
                    {
                        if (ep.SeasonNumber > 0) continue;

                        int thisSeason = 0;
                        if (ep.AirsBeforeSeason.HasValue) thisSeason = ep.AirsBeforeSeason.Value;
                        if (ep.AirsAfterSeason.HasValue) thisSeason = ep.AirsAfterSeason.Value;

                        if (thisSeason != lastSeason)
                            dictTvDBSeasonsSpecials[thisSeason] = i;

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

        public List<CrossRef_AniDB_TvDB_Episode> GetCrossRefTvDBEpisodes() => Repo.CrossRef_AniDB_TvDB_Episode.GetByAnimeID(AnimeID);

        public List<CrossRef_AniDB_TvDBV2> GetCrossRefTvDBV2() => Repo.CrossRef_AniDB_TvDBV2.GetByAnimeID(AnimeID);

        public List<CrossRef_AniDB_TraktV2> GetCrossRefTraktV2() => Repo.CrossRef_AniDB_TraktV2.GetByAnimeID(AnimeID);

        public List<CrossRef_AniDB_MAL> GetCrossRefMAL() => Repo.CrossRef_AniDB_MAL.GetByAnimeID(AnimeID);

        public TvDB_Series GetTvDBSeries()
        {
            int id = GetCrossRefTvDBV2()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id == -1) return null;
            return Repo.TvDB_Series.GetByTvDBID(id);
        }

        public List<TvDB_ImageFanart> GetTvDBImageFanarts()
        {
            List<TvDB_ImageFanart> results = new List<TvDB_ImageFanart>();
            int id = GetCrossRefTvDBV2()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
                results.AddRange(Repo.TvDB_ImageFanart.GetBySeriesID(id));
            return results;
        }

        public List<TvDB_ImagePoster> GetTvDBImagePosters()
        {
            List<TvDB_ImagePoster> results = new List<TvDB_ImagePoster>();
            int id = GetCrossRefTvDBV2()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
                results.AddRange(Repo.TvDB_ImagePoster.GetBySeriesID(id));
            return results;
        }

        public List<TvDB_ImageWideBanner> GetTvDBImageWideBanners()
        {
            List<TvDB_ImageWideBanner> results = new List<TvDB_ImageWideBanner>();
            int id = GetCrossRefTvDBV2()?.FirstOrDefault()?.TvDBID ?? -1;
            if (id != -1)
                results.AddRange(Repo.TvDB_ImageWideBanner.GetBySeriesID(id));
            return results;
        }

        public CrossRef_AniDB_Other GetCrossRefMovieDB() =>
            Repo.CrossRef_AniDB_Other.GetByAnimeIDAndType(AnimeID, CrossRefType.MovieDB);

        public List<MovieDB_Movie> GetMovieDBMovie()
        {
            CrossRef_AniDB_Other xref = GetCrossRefMovieDB();
            if (xref == null) return null;
            return Repo.MovieDb_Movie.GetByMovieID(int.Parse(xref.CrossRefID));
        }

        public List<MovieDB_Fanart> GetMovieDBFanarts()
        {
            CrossRef_AniDB_Other xref = GetCrossRefMovieDB();
            if (xref == null) return new List<MovieDB_Fanart>();
            return Repo.MovieDB_Fanart.GetByMovieID(int.Parse(xref.CrossRefID));
        }

        public List<MovieDB_Poster> GetMovieDBPosters()
        {
            CrossRef_AniDB_Other xref = GetCrossRefMovieDB();
            if (xref == null) return new List<MovieDB_Poster>();
            return Repo.MovieDB_Poster.GetByMovieID(int.Parse(xref.CrossRefID));
        }

        public AniDB_Anime_DefaultImage GetDefaultPoster() => Repo.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, (int) ImageSizeType.Poster);

        public string GetDefaultPosterPathNoBlanks()
        {
            AniDB_Anime_DefaultImage defaultPoster = GetDefaultPoster();
            if (defaultPoster == null)
                return PosterPathNoDefault;
            ImageEntityType imageType = (ImageEntityType) defaultPoster.ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.AniDB_Cover:
                    return PosterPath;

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster tvPoster =
                        Repo.TvDB_ImagePoster.GetByID(defaultPoster.ImageParentID);
                    if (tvPoster != null)
                        return tvPoster.GetFullImagePath();
                    else
                        return PosterPath;

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster moviePoster =
                        Repo.MovieDB_Poster.GetByID(defaultPoster.ImageParentID);
                    if (moviePoster != null)
                        return moviePoster.GetFullImagePath();
                    else
                        return PosterPath;
            }

            return PosterPath;
        }

        public ImageDetails GetDefaultPosterDetailsNoBlanks()
        {
            ImageDetails details = new ImageDetails {ImageType = ImageEntityType.AniDB_Cover, ImageID = AnimeID};
            AniDB_Anime_DefaultImage defaultPoster = GetDefaultPoster();

            if (defaultPoster == null)
                return details;
            ImageEntityType imageType = (ImageEntityType) defaultPoster.ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.AniDB_Cover:
                    return details;

                case ImageEntityType.TvDB_Cover:
                    TvDB_ImagePoster tvPoster =
                        Repo.TvDB_ImagePoster.GetByID(defaultPoster.ImageParentID);
                    if (tvPoster != null)
                        details = new ImageDetails
                        {
                            ImageType = ImageEntityType.TvDB_Cover,
                            ImageID = tvPoster.TvDB_ImagePosterID
                        };
                    return details;

                case ImageEntityType.MovieDB_Poster:
                    MovieDB_Poster moviePoster =
                        Repo.MovieDB_Poster.GetByID(defaultPoster.ImageParentID);
                    if (moviePoster != null)
                        details = new ImageDetails
                        {
                            ImageType = ImageEntityType.MovieDB_Poster,
                            ImageID = moviePoster.MovieDB_PosterID
                        };
                    return details;
            }

            return details;
        }


        public AniDB_Anime_DefaultImage GetDefaultFanart() =>
            Repo.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, (int) ImageSizeType.Fanart);

        public ImageDetails GetDefaultFanartDetailsNoBlanks()
        {
            Random fanartRandom = new Random();

            ImageDetails details = null;
            if (GetDefaultFanart() == null)
            {
                List<CL_AniDB_Anime_DefaultImage> fanarts = Contract.AniDBAnime.Fanarts;
                if (fanarts == null || fanarts.Count == 0) return null;
                CL_AniDB_Anime_DefaultImage art = fanarts[fanartRandom.Next(0, fanarts.Count)];
                details = new ImageDetails
                {
                    ImageID = art.AniDB_Anime_DefaultImageID,
                    ImageType = (ImageEntityType) art.ImageType
                };
                return details;
            }

            // TODO Move this to contract as well
            AniDB_Anime_DefaultImage fanart = GetDefaultFanart();
            ImageEntityType imageType = (ImageEntityType) fanart.ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart tvFanart = Repo.TvDB_ImageFanart.GetByID(fanart.ImageParentID);
                    if (tvFanart != null)
                        details = new ImageDetails
                        {
                            ImageType = ImageEntityType.TvDB_FanArt,
                            ImageID = tvFanart.TvDB_ImageFanartID
                        };
                    return details;

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart = Repo.MovieDB_Fanart.GetByID(fanart.ImageParentID);
                    if (movieFanart != null)
                        details = new ImageDetails
                        {
                            ImageType = ImageEntityType.MovieDB_FanArt,
                            ImageID = movieFanart.MovieDB_FanartID
                        };
                    return details;
            }

            return null;
        }

        public string GetDefaultFanartOnlineURL()
        {
            Random fanartRandom = new Random();


            if (GetDefaultFanart() == null)
            {
                if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
                {
                    List<MovieDB_Fanart> fanarts = GetMovieDBFanarts();
                    if (fanarts.Count == 0) return string.Empty;

                    MovieDB_Fanart movieFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    return movieFanart.URL;
                }
                else
                {
                    List<TvDB_ImageFanart> fanarts = GetTvDBImageFanarts();
                    if (fanarts.Count == 0) return null;

                    TvDB_ImageFanart tvFanart = fanarts[fanartRandom.Next(0, fanarts.Count)];
                    return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
                }
            }

            // TODO Move this to contract as well
            AniDB_Anime_DefaultImage fanart = GetDefaultFanart();
            ImageEntityType imageType = (ImageEntityType) fanart.ImageParentType;

            switch (imageType)
            {
                case ImageEntityType.TvDB_FanArt:
                    TvDB_ImageFanart tvFanart =
                        Repo.TvDB_ImageFanart.GetByID(GetDefaultFanart().ImageParentID);
                    if (tvFanart != null)
                        return string.Format(Constants.URLS.TvDB_Images, tvFanart.BannerPath);
                    break;

                case ImageEntityType.MovieDB_FanArt:
                    MovieDB_Fanart movieFanart =
                        Repo.MovieDB_Fanart.GetByID(GetDefaultFanart().ImageParentID);
                    if (movieFanart != null)
                        return movieFanart.URL;
                    break;
            }

            return string.Empty;
        }

        public AniDB_Anime_DefaultImage GetDefaultWideBanner() =>
            Repo.AniDB_Anime_DefaultImage.GetByAnimeIDAndImagezSizeType(AnimeID, (int) ImageSizeType.WideBanner);

        public ImageDetails GetDefaultWideBannerDetailsNoBlanks()
        {
            Random bannerRandom = new Random();

            ImageDetails details;
            AniDB_Anime_DefaultImage banner = GetDefaultWideBanner();
            if (banner == null)
            {
                // get a random banner (only tvdb)
                if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie) return null;
                List<CL_AniDB_Anime_DefaultImage> banners = Contract.AniDBAnime.Banners;
                if (banners == null || banners.Count == 0) return null;
                CL_AniDB_Anime_DefaultImage art = banners[bannerRandom.Next(0, banners.Count)];
                details = new ImageDetails
                {
                    ImageID = art.AniDB_Anime_DefaultImageID,
                    ImageType = (ImageEntityType) art.ImageType
                };
                return details;
            }

            ImageEntityType imageType = (ImageEntityType) banner.ImageParentType;

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


        public List<AniDB_Tag> GetTags()
        {
            List<AniDB_Tag> tags = new List<AniDB_Tag>();
            foreach (AniDB_Anime_Tag tag in GetAnimeTags())
            {
                AniDB_Tag newTag = Repo.AniDB_Tag.GetByID(tag.TagID);
                if (newTag != null) tags.Add(newTag);
            }

            return tags;
        }

        public List<CustomTag> GetCustomTagsForAnime() => Repo.CustomTag.GetByAnimeID(AnimeID);

        public List<AniDB_Tag> GetAniDBTags() => Repo.AniDB_Tag.GetByAnimeID(AnimeID);

        public List<AniDB_Anime_Tag> GetAnimeTags() => Repo.AniDB_Anime_Tag.GetByAnimeID(AnimeID);

        public List<AniDB_Anime_Relation> GetRelatedAnime() => Repo.AniDB_Anime_Relation.GetByAnimeID(AnimeID);

        public List<AniDB_Anime_Similar> GetSimilarAnime() => Repo.AniDB_Anime_Similar.GetByAnimeID(AnimeID);


        public List<SVR_AniDB_Anime> GetAllRelatedAnime()
        {
            List<SVR_AniDB_Anime> relList = new List<SVR_AniDB_Anime>();
            List<int> relListIDs = new List<int>();
            List<int> searchedIDs = new List<int>();

            GetRelatedAnimeRecursive(AnimeID, ref relList, ref relListIDs, ref searchedIDs);
            return relList;
        }


        public List<AniDB_Anime_Character> GetAnimeCharacters() => Repo.AniDB_Anime_Character.GetByAnimeID(AnimeID);

        public List<AniDB_Anime_Title> GetTitles() => Repo.AniDB_Anime_Title.GetByAnimeID(AnimeID);

        public string GetFormattedTitle(List<AniDB_Anime_Title> titles)
        {
            foreach (NamingLanguage nlan in Languages.PreferredNamingLanguages)
            {
                string thisLanguage = nlan.Language.Trim().ToUpper();

                // Romaji and English titles will be contained in MAIN and/or OFFICIAL
                // we won't use synonyms for these two languages
                if (thisLanguage.Equals(Shoko.Models.Constants.AniDBLanguageType.Romaji) ||
                    thisLanguage.Equals(Shoko.Models.Constants.AniDBLanguageType.English))
                {
                    foreach (AniDB_Anime_Title title in titles)
                    {
                        string titleType = title.TitleType.Trim().ToUpper();
                        // first try the  Main title
                        if (titleType.Trim().Equals(Shoko.Models.Constants.AnimeTitleType.Main,
                                StringComparison.OrdinalIgnoreCase) &&
                            title.Language.Trim().Equals(thisLanguage, StringComparison.OrdinalIgnoreCase))
                            return title.Title;
                    }
                }

                // now try the official title
                foreach (AniDB_Anime_Title title in titles)
                {
                    string titleType = title.TitleType.Trim();
                    if (titleType.Equals(Shoko.Models.Constants.AnimeTitleType.Official, StringComparison.OrdinalIgnoreCase) && title.Language.Trim().Equals(thisLanguage, StringComparison.OrdinalIgnoreCase))
                        return title.Title;
                }

                // try synonyms
                if (ServerSettings.LanguageUseSynonyms)
                    foreach (AniDB_Anime_Title title in titles)
                    {
                        string titleType = title.TitleType.Trim().ToUpper();
                        if (titleType == Shoko.Models.Constants.AnimeTitleType.Synonym.ToUpper() && title.Language.Trim().ToUpper() == thisLanguage)
                            return title.Title;
                    }
            }

            // otherwise just use the main title
            return MainTitle;
        }

        public string GetFormattedTitle()
        {
            List<AniDB_Anime_Title> thisTitles = GetTitles();
            return GetFormattedTitle(thisTitles);
        }

        public List<AniDB_Episode> GetAniDBEpisodes() => Repo.AniDB_Episode.GetByAnimeID(AnimeID);

        private bool Populate_RA(Raw_AniDB_Anime animeInfo)
        {
            // We need various values to be populated to be considered valid
            if (string.IsNullOrEmpty(animeInfo?.MainTitle) || animeInfo.AnimeID <= 0) return false;
            AirDate = animeInfo.AirDate;
            AllCinemaID = animeInfo.AllCinemaID;
            AnimeID = animeInfo.AnimeID;
            //this.AnimeNfo = animeInfo.AnimeNfoID;
            AnimePlanetID = animeInfo.AnimePlanetID;
            this.SetAnimeTypeRAW(animeInfo.AnimeTypeRAW);
            ANNID = animeInfo.ANNID;
            AvgReviewRating = animeInfo.AvgReviewRating;
            AwardList = animeInfo.AwardList;
            BeginYear = animeInfo.BeginYear;
            DateTimeDescUpdated = DateTime.Now;
            DateTimeUpdated = DateTime.Now;
            Description = animeInfo.Description ?? string.Empty;
            EndDate = animeInfo.EndDate;
            EndYear = animeInfo.EndYear;
            MainTitle = animeInfo.MainTitle;
            AllTitles = string.Empty;
            AllTags = string.Empty;
            //this.EnglishName = animeInfo.EnglishName;
            EpisodeCount = animeInfo.EpisodeCount;
            EpisodeCountNormal = animeInfo.EpisodeCountNormal;
            EpisodeCountSpecial = animeInfo.EpisodeCountSpecial;
            //this.genre
            ImageEnabled = 1;
            //this.KanjiName = animeInfo.KanjiName;
            LatestEpisodeNumber = animeInfo.LatestEpisodeNumber;
            //this.OtherName = animeInfo.OtherName;
            Picname = animeInfo.Picname;
            Rating = animeInfo.Rating;
            //this.relations
            Restricted = animeInfo.Restricted;
            ReviewCount = animeInfo.ReviewCount;
            //this.RomajiName = animeInfo.RomajiName;
            //this.ShortNames = animeInfo.ShortNames.Replace("'", "|");
            //this.Synonyms = animeInfo.Synonyms.Replace("'", "|");
            TempRating = animeInfo.TempRating;
            TempVoteCount = animeInfo.TempVoteCount;
            URL = animeInfo.URL;
            VoteCount = animeInfo.VoteCount;
            return true;
        }

        public static AniDB_Anime PopulateOrCreateFromHTTP(Raw_AniDB_Anime animeInfo, List<Raw_AniDB_Episode> eps,
            List<Raw_AniDB_Anime_Title> titles,
            List<Raw_AniDB_Category> cats, List<Raw_AniDB_Tag> tags, List<Raw_AniDB_Character> chars,
            List<Raw_AniDB_RelatedAnime> rels, List<Raw_AniDB_SimilarAnime> sims,
            List<Raw_AniDB_Recommendation> recs, bool downloadRelations)
        {
            logger.Trace("------------------------------------------------");
            logger.Trace($"PopulateAndSaveFromHTTP: for {animeInfo.AnimeID} - {animeInfo.MainTitle}");
            logger.Trace("------------------------------------------------");

            Stopwatch taskTimer = new Stopwatch();
            Stopwatch totalTimer = Stopwatch.StartNew();

            SVR_AniDB_Anime anime;


            using (var upd = Repo.AniDB_Anime.BeginAddOrUpdate(animeInfo.AnimeID))
            {
                if (!upd.Entity.Populate_RA(animeInfo))
                {
                    logger.Error("AniDB_Anime was unable to populate as it received invalid info. " +
                                 "This is not an error on our end. It is AniDB's issue, " +
                                 "as they did not return either an ID or a title for the anime.");
                    totalTimer.Stop();
                    return null;
                }

                taskTimer.Start();

                upd.Entity.CreateEpisodes_RA(eps);
                taskTimer.Stop();
                logger.Trace("CreateEpisodes in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();

                upd.Entity.CreateTitles_RA(titles);
                taskTimer.Stop();
                logger.Trace("CreateTitles in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();

                upd.Entity.CreateTags_RA(tags);
                taskTimer.Stop();
                logger.Trace("CreateTags in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();

                upd.Entity.CreateCharacters(chars);
                taskTimer.Stop();
                logger.Trace("CreateCharacters in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();

                upd.Entity.CreateRelations(rels, downloadRelations);
                taskTimer.Stop();
                logger.Trace("CreateRelations in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();

                upd.Entity.CreateSimilarAnime(sims);
                taskTimer.Stop();
                logger.Trace("CreateSimilarAnime in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();

                upd.Entity.CreateRecommendations(recs);
                taskTimer.Stop();
                logger.Trace("CreateRecommendations in : " + taskTimer.ElapsedMilliseconds);
                taskTimer.Restart();


                upd.Entity.dictTvDBEpisodes = null;
                upd.Entity.dictTvDBSeasons = null;
                upd.Entity.dictTvDBSeasonsSpecials = null;
                upd.Entity.allPosters = null;

                anime = upd.Commit();
                totalTimer.Stop();
                logger.Trace("TOTAL TIME in : " + totalTimer.ElapsedMilliseconds);
                logger.Trace("------------------------------------------------");
                return anime;
            }
        }

        /// <summary>
        ///     we are depending on the HTTP api call to get most of the info
        ///     we only use UDP to get mssing information
        /// </summary>
        /// <param name="animeInfo"></param>
        public static AniDB_Anime PopulateAndSaveFromUDP(Raw_AniDB_Anime animeInfo)
        {
            using (var upd = Repo.AniDB_Anime.BeginAddOrUpdate(animeInfo.AnimeID))
            {
                upd.Entity.reviewIDListRAW = animeInfo.ReviewIDListRAW;
                upd.Entity.CreateAnimeReviews();
                return upd.Commit();
            }
        }

        private void CreateEpisodes_RA(List<Raw_AniDB_Episode> eps)
        {
            if (eps == null) return;


            EpisodeCountSpecial = 0;
            EpisodeCountNormal = 0;

            List<SVR_AnimeEpisode> animeEpsToDelete = new List<SVR_AnimeEpisode>();
            List<AniDB_Episode> aniDBEpsToDelete = new List<AniDB_Episode>();

            foreach (Raw_AniDB_Episode epraw in eps)
            {
                //
                // we need to do this check because some times AniDB will replace an existing episode with a new episode
                List<AniDB_Episode> existingEps = Repo.AniDB_Episode.GetByAnimeIDAndEpisodeTypeNumber(epraw.AnimeID,
                    (EpisodeType) epraw.EpisodeType, epraw.EpisodeNumber);

                // delete any old records
                foreach (AniDB_Episode epOld in existingEps)
                    if (epOld.EpisodeID != epraw.EpisodeID)
                    {
                        // first delete any AnimeEpisode records that point to the new anidb episode
                        animeEpsToDelete.AddRange(Repo.AnimeEpisode.GetByAniDBEpisodeID(epOld.EpisodeID));
                        aniDBEpsToDelete.Add(epOld);
                    }
            }

            Repo.AnimeEpisode.Delete(animeEpsToDelete);
            Repo.AniDB_Episode.Delete(aniDBEpsToDelete);


            foreach (Raw_AniDB_Episode epraw in eps)
            {
                using (var upd = Repo.AniDB_Episode.BeginAddOrUpdate(epraw.EpisodeID))
                {
                    upd.Entity.Populate(epraw);
                    AniDB_Episode ep = upd.Commit();
                    if (ep.GetEpisodeTypeEnum() == EpisodeType.Episode)
                        EpisodeCountNormal++;

                    if (ep.GetEpisodeTypeEnum() == EpisodeType.Special)
                        EpisodeCountSpecial++;
                }
            }

            EpisodeCount = EpisodeCountSpecial + EpisodeCountNormal;
        }

        private void CreateTitles_RA(List<Raw_AniDB_Anime_Title> titles)
        {
            if (titles == null) return;

            AllTitles = string.Empty;

            using (var atupd = Repo.AniDB_Anime_Title.BeginBatchUpdate(Repo.AniDB_Anime_Title.GetIdsByAnimeID(AnimeID)))
            {
                foreach (Raw_AniDB_Anime_Title rawtitle in titles)
                {
                    atupd.Process(() => Repo.AniDB_Anime_Title.GetByAnimeIDLanguageTypeValue(rawtitle.AnimeID, rawtitle.Language, rawtitle.TitleType, rawtitle.Title),
                        (tit) =>
                        {
                            tit.Populate(rawtitle);
                            if (AllTitles.Length > 0)
                                AllTitles += "|";
                            AllTitles += rawtitle.Title;
                        });
                }
                atupd.Commit();
            }
        }

        private void CreateTags_RA(List<Raw_AniDB_Tag> tags)
        {
            if (tags == null) return;

            AllTags = string.Empty;

            using (var atupd = Repo.AniDB_Anime_Tag.BeginBatchUpdate(Repo.AniDB_Anime_Tag.GetIdsByAnimeID(AnimeID)))
            {
                foreach (Raw_AniDB_Tag rawtag in tags)
                {
                    using (var upd = Repo.AniDB_Tag.BeginAddOrUpdate(rawtag.TagID))
                    {
                        if (upd.Entity.Populate(rawtag))
                        {
                            atupd.Process(() => Repo.AniDB_Anime_Tag.GetByAnimeIDAndTagID(rawtag.AnimeID, rawtag.TagID), (tg) => { tg.Populate(rawtag); });
                            AniDB_Tag tag = upd.Commit();
                            if (AllTags.Length > 0) AllTags += "|";
                            AllTags += tag.TagName;
                        }
                    }
                }
                atupd.Commit();
            }
        }

        private void CreateCharacters(List<Raw_AniDB_Character> chars)
        {
            if (chars == null) return;
            using (var acupd = Repo.AniDB_Anime_Character.BeginBatchUpdate(Repo.AniDB_Anime_Character.GetIdsByAnimeID(AnimeID)))
            using (var acsupd = Repo.AniDB_Character_Seiyuu.BeginBatchUpdate(chars.SelectMany(a => Repo.AniDB_Character_Seiyuu.GetIdsByCharID(a.CharID)).Distinct()))
            {
                foreach (Raw_AniDB_Character rawchar in chars)
                {
                    using (var charupd = Repo.AniDB_Character.BeginAddOrUpdate(rawchar.CharID))
                    {
                        if (charupd.Entity.PopulateFromHTTP(rawchar))
                        {
                            // create cross ref's between anime and character, but don't actually download anything
                            acupd.Process(() => Repo.AniDB_Anime_Character.GetByAnimeIDAndCharID(rawchar.AnimeID, rawchar.CharID), (ac) => { ac.Populate(rawchar); });
                            foreach (Raw_AniDB_Seiyuu rawSeiyuu in rawchar.Seiyuus)
                            {
                                using (var seiupd = Repo.AniDB_Seiyuu.BeginAddOrUpdate(rawSeiyuu.SeiyuuID))
                                {
                                    seiupd.Entity.PicName = rawSeiyuu.PicName;
                                    seiupd.Entity.SeiyuuID = rawSeiyuu.SeiyuuID;
                                    seiupd.Entity.SeiyuuName = rawSeiyuu.SeiyuuName;
                                    seiupd.Commit();
                                }

                                acsupd.Process(() => Repo.AniDB_Character_Seiyuu.GetByCharIDAndSeiyuuID(rawchar.CharID, rawSeiyuu.SeiyuuID), (acs) =>
                                {
                                    acs.CharID = rawchar.CharID;
                                    acs.SeiyuuID = rawSeiyuu.SeiyuuID;
                                });
                            }
                        }
                    }
                }
                acsupd.Commit();
                acupd.Commit();
            }
        }

        private void CreateRelations(List<Raw_AniDB_RelatedAnime> rels, bool downloadRelations)
        {
            if (rels == null) return;

            List<CommandRequest_GetAnimeHTTP> cmdsToSave = new List<CommandRequest_GetAnimeHTTP>();
            using (var arupd = Repo.AniDB_Anime_Relation.BeginBatchUpdate(Repo.AniDB_Anime_Relation.GetIdsByAnimeID(AnimeID)))
            {
                foreach (Raw_AniDB_RelatedAnime rawrel in rels)
                {
                    if (rawrel == null) continue;
                    if (rawrel.AnimeID <= 0 || rawrel.RelatedAnimeID <= 0 || string.IsNullOrEmpty(rawrel.RelationType))
                        continue;
                    arupd.Process(() => Repo.AniDB_Anime_Relation.GetByAnimeIDAndRelationID(rawrel.AnimeID,
                        rawrel.RelatedAnimeID), (aa) => { aa.Populate(rawrel); });
                    if (downloadRelations || ServerSettings.AutoGroupSeries)
                    {
                        logger.Info(
                            $"Adding command to download related anime for {MainTitle} ({AnimeID}), related anime ID = {rawrel.RelatedAnimeID}");

                        // I have disable the downloading of relations here because of banning issues
                        // basically we will download immediate relations, but not relations of relations

                        //CommandRequest_GetAnimeHTTP cr_anime = new CommandRequest_GetAnimeHTTP(rawrel.RelatedAnimeID, false, downloadRelations);
                        CommandRequest_GetAnimeHTTP cr_anime =
                            new CommandRequest_GetAnimeHTTP(rawrel.RelatedAnimeID, false, false);
                        cmdsToSave.Add(cr_anime);
                    }
                }

                arupd.Commit();
            }

            foreach (CommandRequest_GetAnimeHTTP cmd in cmdsToSave)
                cmd.Save();
        }

        private void CreateSimilarAnime(List<Raw_AniDB_SimilarAnime> sims)
        {
            if (sims == null) return;
            using (var arupd = Repo.AniDB_Anime_Similar.BeginBatchUpdate(Repo.AniDB_Anime_Similar.GetIdsByAnimeID(AnimeID)))
            {
                foreach (Raw_AniDB_SimilarAnime rawsim in sims)
                    arupd.Process(() => Repo.AniDB_Anime_Similar.GetByAnimeIDAndSimilarID(rawsim.AnimeID, rawsim.SimilarAnimeID), (asi) => { asi.Populate(rawsim); });
                arupd.Commit();
            }
        }

        private void CreateRecommendations(List<Raw_AniDB_Recommendation> recs)
        {
            if (recs == null) return;

            using (var arupd = Repo.AniDB_Recommendation.BeginBatchUpdate(Repo.AniDB_Recommendation.GetIdsByAnimeID(AnimeID)))
            {
                foreach (Raw_AniDB_Recommendation rawRec in recs)
                    arupd.Process(() => Repo.AniDB_Recommendation.GetByAnimeIDAndUserID(rawRec.AnimeID, rawRec.UserID), (a) => { a.Populate(rawRec); });
                arupd.Commit();
            }
        }

        private void CreateAnimeReviews()
        {
            //Only create relations if the origin of the data if from Raw (WebService/AniDB)
            if (reviewIDListRAW == null || reviewIDListRAW.Trim().Length == 0)
                return;
            string[] revs = reviewIDListRAW.Split(',');
            if (revs.Length == 0) return;
            using (var arupd = Repo.AniDB_Anime_Review.BeginBatchUpdate(Repo.AniDB_Anime_Review.GetIdsByAnimeID(AnimeID)))
            {
                foreach (string review in revs)
                {
                    if ((review.Trim().Length > 0) && (int.TryParse(review.Trim(), out int rev)))
                    {
                        arupd.Process(() => Repo.AniDB_Anime_Review.GetByAnimeIDAndReviewID(AnimeID, rev), (r) =>
                        {
                            r.AnimeID = AnimeID;
                            r.ReviewID = rev;
                        });
                    }
                }

                arupd.Commit();
            }
        }


        private CL_AniDB_Anime GenerateContract(List<AniDB_Anime_Title> titles)
        {
            List<CL_AniDB_Character> characters = GetCharactersContract();
            List<MovieDB_Fanart> movDbFanart = null;
            List<TvDB_ImageFanart> tvDbFanart = null;
            List<TvDB_ImageWideBanner> tvDbBanners = null;

            if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
            {
                movDbFanart = GetMovieDBFanarts();
            }
            else
            {
                tvDbFanart = GetTvDBImageFanarts();
                tvDbBanners = GetTvDBImageWideBanners();
            }

            CL_AniDB_Anime cl = GenerateContract(titles, null, characters, movDbFanart, tvDbFanart, tvDbBanners);
            AniDB_Anime_DefaultImage defFanart = GetDefaultFanart();
            AniDB_Anime_DefaultImage defPoster = GetDefaultPoster();
            AniDB_Anime_DefaultImage defBanner = GetDefaultWideBanner();

            cl.DefaultImageFanart = defFanart?.ToClient();
            cl.DefaultImagePoster = defPoster?.ToClient();
            cl.DefaultImageWideBanner = defBanner?.ToClient();

            return cl;
        }

        private CL_AniDB_Anime GenerateContract(List<AniDB_Anime_Title> titles, DefaultAnimeImages defaultImages,
            List<CL_AniDB_Character> characters, IEnumerable<MovieDB_Fanart> movDbFanart,
            IEnumerable<TvDB_ImageFanart> tvDbFanart,
            IEnumerable<TvDB_ImageWideBanner> tvDbBanners)
        {
            CL_AniDB_Anime cl = this.ToClient();
            cl.FormattedTitle = GetFormattedTitle(titles);
            cl.Characters = characters;

            if (defaultImages != null)
            {
                cl.DefaultImageFanart = defaultImages.Fanart?.ToContract();
                cl.DefaultImagePoster = defaultImages.Poster?.ToContract();
                cl.DefaultImageWideBanner = defaultImages.WideBanner?.ToContract();
            }

            if (this.GetAnimeTypeEnum() == Shoko.Models.Enums.AnimeType.Movie)
            {
                cl.Fanarts = movDbFanart?.Select(a => new CL_AniDB_Anime_DefaultImage
                    {
                        ImageType = (int) ImageEntityType.MovieDB_FanArt,
                        MovieFanart = a,
                        AniDB_Anime_DefaultImageID = a.MovieDB_FanartID
                    })
                    .ToList();
            }
            else // Not a movie
            {
                cl.Fanarts = tvDbFanart?.Select(a => new CL_AniDB_Anime_DefaultImage
                    {
                        ImageType = (int) ImageEntityType.TvDB_FanArt,
                        TVFanart = a,
                        AniDB_Anime_DefaultImageID = a.TvDB_ImageFanartID
                    })
                    .ToList();
                cl.Banners = tvDbBanners?.Select(a => new CL_AniDB_Anime_DefaultImage
                    {
                        ImageType = (int) ImageEntityType.TvDB_Banner,
                        TVWideBanner = a,
                        AniDB_Anime_DefaultImageID = a.TvDB_ImageWideBannerID
                    })
                    .ToList();
            }

            if (cl.Fanarts?.Count == 0) cl.Fanarts = null;
            if (cl.Banners?.Count == 0) cl.Banners = null;

            return cl;
        }

        public List<CL_AniDB_Character> GetCharactersContract()
        {
            List<CL_AniDB_Character> chars = new List<CL_AniDB_Character>();

            try
            {
                List<AniDB_Anime_Character> animeChars = Repo.AniDB_Anime_Character.GetByAnimeID(AnimeID);
                if (animeChars == null || animeChars.Count == 0) return chars;

                foreach (AniDB_Anime_Character animeChar in animeChars)
                {
                    AniDB_Character chr = Repo.AniDB_Character.GetByID(animeChar.CharID);
                    if (chr != null)
                        chars.Add(chr.ToClient(animeChar.CharType));
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.ToString());
            }

            return chars;
        }

        public static void UpdateContractDetailedBatch(List<SVR_AniDB_Anime> animeColl)
        {
            if (animeColl == null)
                throw new ArgumentNullException(nameof(animeColl));
            int[] animeIds = animeColl.Select(a => a.AnimeID).ToArray();

            var titlesByAnime = Repo.AniDB_Anime_Title.GetByAnimeIDs(animeIds);
            var animeTagsByAnime = Repo.AniDB_Anime_Tag.GetByAnimeIDs(animeIds);
            var tagsByAnime = Repo.AniDB_Tag.GetByAnimeIDs(animeIds);
            var custTagsByAnime = Repo.CustomTag.GetByAnimeIDs(animeIds);
            var voteByAnime = Repo.AniDB_Vote.GetByAnimeIDs(animeIds);
            var audioLangByAnime = Repo.Adhoc.GetAudioLanguageStatsByAnime(animeIds);
            var subtitleLangByAnime = Repo.Adhoc.GetSubtitleLanguageStatsByAnime(animeIds);
            var vidQualByAnime = Repo.Adhoc.GetAllVideoQualityByAnime(animeIds);
            var epVidQualByAnime = Repo.Adhoc.GetEpisodeVideoQualityStatsByAnime(animeIds);
            var defImagesByAnime = Repo.AniDB_Anime.GetDefaultImagesByAnime(animeIds);
            var charsByAnime = Repo.AniDB_Character.GetCharacterAndSeiyuuByAnime(animeIds);
            var movDbFanartByAnime = Repo.MovieDB_Fanart.GetByAnimeIDs(animeIds);
            var tvDbBannersByAnime = Repo.TvDB_ImageWideBanner.GetByAnimeIDs(animeIds);
            var tvDbFanartByAnime = Repo.TvDB_ImageFanart.GetByAnimeIDs(animeIds);

            foreach (SVR_AniDB_Anime anime in animeColl)
            {
                using (var upd = Repo.AniDB_Anime.BeginUpdate(anime))
                {
                    var contract = new CL_AniDB_AnimeDetailed();
                    var animeTitles = titlesByAnime[upd.Entity.AnimeID];

                    defImagesByAnime.TryGetValue(upd.Entity.AnimeID, out DefaultAnimeImages defImages);

                    var characterContracts = charsByAnime[upd.Entity.AnimeID].Select(ac => ac.ToClient()).ToList();
                    var movieDbFanart = movDbFanartByAnime[upd.Entity.AnimeID];
                    var tvDbBanners = tvDbBannersByAnime[upd.Entity.AnimeID];
                    var tvDbFanart = tvDbFanartByAnime[upd.Entity.AnimeID];

                    contract.AniDBAnime = upd.Entity.GenerateContract(animeTitles.ToList(), defImages,
                        characterContracts,
                        movieDbFanart, tvDbFanart, tvDbBanners);

                    // Anime titles
                    contract.AnimeTitles = titlesByAnime[upd.Entity.AnimeID]
                        .Select(t => new CL_AnimeTitle
                        {
                            AnimeID = t.AnimeID,
                            Language = t.Language,
                            Title = t.Title,
                            TitleType = t.TitleType
                        })
                        .ToList();

                    // Seasons
                    if (upd.Entity.AirDate != null)
                    {
                        int beginYear = upd.Entity.AirDate.Value.Year;
                        int endYear = upd.Entity.EndDate?.Year ?? DateTime.Today.Year;
                        for (int year = beginYear; year <= endYear; year++)
                            foreach (AnimeSeason season in Enum.GetValues(typeof(AnimeSeason)))
                                if (upd.Entity.IsInSeason(season, year))
                                    contract.Stat_AllSeasons.Add($"{season} {year}");
                    }

                    // Anime tags
                    var dictAnimeTags = animeTagsByAnime[upd.Entity.AnimeID]
                        .ToDictionary(t => t.TagID);

                    contract.Tags = tagsByAnime[upd.Entity.AnimeID]
                        .Select(t =>
                        {
                            CL_AnimeTag ctag = new CL_AnimeTag
                            {
                                GlobalSpoiler = t.GlobalSpoiler,
                                LocalSpoiler = t.LocalSpoiler,
                                TagDescription = t.TagDescription,
                                TagID = t.TagID,
                                TagName = t.TagName,
                                Weight = dictAnimeTags.TryGetValue(t.TagID, out AniDB_Anime_Tag animeTag)
                                    ? animeTag.Weight
                                    : 0
                            };

                            return ctag;
                        })
                        .ToList();

                    // Custom tags
                    contract.CustomTags = custTagsByAnime[upd.Entity.AnimeID];

                    // Vote

                    if (voteByAnime.TryGetValue(upd.Entity.AnimeID, out AniDB_Vote vote)) contract.UserVote = vote;


                    // Subtitle languages
                    contract.Stat_AudioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                    if (audioLangByAnime.TryGetValue(upd.Entity.AnimeID, out LanguageStat langStat))
                        contract.Stat_AudioLanguages.UnionWith(langStat.LanguageNames);

                    // Audio languages
                    contract.Stat_SubtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                    if (subtitleLangByAnime.TryGetValue(upd.Entity.AnimeID, out langStat))
                        contract.Stat_SubtitleLanguages.UnionWith(langStat.LanguageNames);

                    // Anime video quality

                    contract.Stat_AllVideoQuality = vidQualByAnime.TryGetValue(upd.Entity.AnimeID, out HashSet<string> vidQual)
                            ? vidQual
                            : new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                    // Episode video quality

                    contract.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

                    if (epVidQualByAnime.TryGetValue(upd.Entity.AnimeID, out AnimeVideoQualityStat vidQualStat) && vidQualStat.VideoQualityEpisodeCount.Count > 0)
                        contract.Stat_AllVideoQuality_Episodes.UnionWith(vidQualStat.VideoQualityEpisodeCount.Where(kvp => kvp.Value >= upd.Entity.EpisodeCountNormal).Select(kvp => kvp.Key));

                    upd.Entity.Contract = contract;
                    upd.Commit();
                }
            }
        }

        public void UpdateContractDetailed()
        {
            List<AniDB_Anime_Title> animeTitles = Repo.AniDB_Anime_Title.GetByAnimeID(AnimeID);
            CL_AniDB_AnimeDetailed cl = new CL_AniDB_AnimeDetailed
            {
                AniDBAnime = GenerateContract(animeTitles),
                AnimeTitles = new List<CL_AnimeTitle>(),
                Tags = new List<CL_AnimeTag>(),
                CustomTags = new List<CustomTag>()
            };

            // get all the anime titles
            if (animeTitles != null)
                foreach (AniDB_Anime_Title title in animeTitles)
                {
                    CL_AnimeTitle ctitle = new CL_AnimeTitle
                    {
                        AnimeID = title.AnimeID,
                        Language = title.Language,
                        Title = title.Title,
                        TitleType = title.TitleType
                    };
                    cl.AnimeTitles.Add(ctitle);
                }

            if (AirDate != null)
            {
                int beginYear = AirDate.Value.Year;
                int endYear = EndDate?.Year ?? DateTime.Today.Year;
                for (int year = beginYear; year <= endYear; year++)
                    foreach (AnimeSeason season in Enum.GetValues(typeof(AnimeSeason)))
                        if (this.IsInSeason(season, year))
                            cl.Stat_AllSeasons.Add($"{season} {year}");
            }

            Dictionary<int, AniDB_Anime_Tag> dictAnimeTags = new Dictionary<int, AniDB_Anime_Tag>();
            foreach (AniDB_Anime_Tag animeTag in GetAnimeTags())
                dictAnimeTags[animeTag.TagID] = animeTag;

            foreach (AniDB_Tag tag in GetAniDBTags())
            {
                CL_AnimeTag ctag = new CL_AnimeTag
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
                    ctag.Weight = dictAnimeTags[tag.TagID].Weight;
                else
                    ctag.Weight = 0;

                cl.Tags.Add(ctag);
            }


            // Get all the custom tags
            foreach (CustomTag custag in GetCustomTagsForAnime())
                cl.CustomTags.Add(custag);

            if (UserVote != null)
                cl.UserVote = UserVote;

            HashSet<string> audioLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            HashSet<string> subtitleLanguages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            //logger.Trace(" XXXX 06");

            // audio languages
            LanguageStat dicAudio = Repo.Adhoc.GetAudioLanguageStatsByAnime(AnimeID);
            foreach (string lanName in dicAudio.LanguageNames)
                if (!audioLanguages.Contains(lanName))
                    audioLanguages.Add(lanName);

            //logger.Trace(" XXXX 07");

            // subtitle languages
            LanguageStat dicSubtitle = Repo.Adhoc.GetSubtitleLanguageStatsByAnime(AnimeID);
            foreach (string lanName in dicSubtitle.LanguageNames)
                if (!subtitleLanguages.Contains(lanName))
                    subtitleLanguages.Add(lanName);

            //logger.Trace(" XXXX 08");

            cl.Stat_AudioLanguages = audioLanguages;

            //logger.Trace(" XXXX 09");

            cl.Stat_SubtitleLanguages = subtitleLanguages;

            //logger.Trace(" XXXX 10");
            cl.Stat_AllVideoQuality = Repo.Adhoc.GetAllVideoQualityForAnime(AnimeID);

            AnimeVideoQualityStat stat = Repo.Adhoc.GetEpisodeVideoQualityStatsForAnime(AnimeID);
            cl.Stat_AllVideoQuality_Episodes = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            if (stat != null && stat.VideoQualityEpisodeCount.Count > 0)
            {
                foreach (KeyValuePair<string, int> kvp in stat.VideoQualityEpisodeCount)
                {
                    if (kvp.Value >= EpisodeCountNormal)
                        cl.Stat_AllVideoQuality_Episodes.Add(kvp.Key);
                }
            }

            //logger.Trace(" XXXX 11");
            using (var upd = Repo.AniDB_Anime.BeginUpdate(this))
            {
                upd.Entity.Contract = cl;
                upd.Commit();
            }
        }


        public Azure_AnimeFull ToAzure()
        {
            Azure_AnimeFull contract = new Azure_AnimeFull
            {
                Detail = new Azure_AnimeDetail(),
                Characters = new List<Azure_AnimeCharacter>(),
                Comments = new List<Azure_AnimeComment>()
            };
            contract.Detail.AllTags = TagsString;
            contract.Detail.AllCategories = TagsString;
            contract.Detail.AnimeID = AnimeID;
            contract.Detail.AnimeName = MainTitle;
            contract.Detail.AnimeType = this.GetAnimeTypeDescription();
            contract.Detail.Description = Description;
            contract.Detail.EndDateLong = AniDB.GetAniDBDateAsSeconds(EndDate);
            contract.Detail.StartDateLong = AniDB.GetAniDBDateAsSeconds(AirDate);
            contract.Detail.EpisodeCountNormal = EpisodeCountNormal;
            contract.Detail.EpisodeCountSpecial = EpisodeCountSpecial;
            contract.Detail.FanartURL = GetDefaultFanartOnlineURL();
            contract.Detail.OverallRating = this.GetAniDBRating();
            contract.Detail.PosterURL = string.Format(Constants.URLS.AniDB_Images, Picname);
            contract.Detail.TotalVotes = this.GetAniDBTotalVotes();


            List<AniDB_Anime_Character> animeChars = Repo.AniDB_Anime_Character.GetByAnimeID(AnimeID);

            if (animeChars != null && animeChars.Count > 0)
            {
                // first get all the main characters
                foreach (AniDB_Anime_Character animeChar in animeChars.Where(item => item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase)))
                {
                    AniDB_Character chr = Repo.AniDB_Character.GetByID(animeChar.CharID);
                    if (chr != null)
                        contract.Characters.Add(chr.ToContractAzure(animeChar));
                }

                // now get the rest
                foreach (AniDB_Anime_Character animeChar in animeChars.Where(item => !item.CharType.Equals("main character in", StringComparison.InvariantCultureIgnoreCase))
                )
                {
                    AniDB_Character chr = Repo.AniDB_Character.GetByID(animeChar.CharID);
                    if (chr != null)
                        contract.Characters.Add(chr.ToContractAzure(animeChar));
                }
            }


            foreach (AniDB_Recommendation rec in Repo.AniDB_Recommendation.GetByAnimeID(AnimeID))
            {
                Azure_AnimeComment comment = new Azure_AnimeComment
                {
                    UserID = rec.UserID,
                    UserName = string.Empty,

                    // Comment details
                    CommentText = rec.RecommendationText,
                    IsSpoiler = false,
                    CommentDateLong = 0,

                    ImageURL = string.Empty
                };
                AniDBRecommendationType recType = (AniDBRecommendationType) rec.RecommendationType;
                switch (recType)
                {
                    case AniDBRecommendationType.ForFans:
                        comment.CommentType = (int) WhatPeopleAreSayingType.AniDBForFans;
                        break;
                    case AniDBRecommendationType.MustSee:
                        comment.CommentType = (int) WhatPeopleAreSayingType.AniDBMustSee;
                        break;
                    case AniDBRecommendationType.Recommended:
                        comment.CommentType = (int) WhatPeopleAreSayingType.AniDBRecommendation;
                        break;
                }

                comment.Source = "AniDB";
                contract.Comments.Add(comment);
            }

            return contract;
        }


        public SVR_AnimeSeries CreateAnimeSeriesAndGroup(int? existingGroupID = null)
        {
            SVR_AnimeSeries series;
            // Create a new AnimeSeries record
            using (var serupd = Repo.AnimeSeries.BeginAdd())
            {
                serupd.Entity.Populate_RA(this);
                if (existingGroupID == null)
                {
                    SVR_AnimeGroup grp = new AnimeGroupCreator().GetOrCreateSingleGroupForSeries(serupd.Entity);
                    serupd.Entity.AnimeGroupID = grp.AnimeGroupID;
                }
                else
                {
                    SVR_AnimeGroup grp = Repo.AnimeGroup.GetByID(existingGroupID.Value) ??
                                         new AnimeGroupCreator().GetOrCreateSingleGroupForSeries(serupd.Entity);
                    serupd.Entity.AnimeGroupID = grp.AnimeGroupID;
                }

                series = serupd.Commit((false, false, false, false));
            }

            // check for TvDB associations
            if (Restricted == 0)
            {
                CommandRequest_TvDBSearchAnime cmd = new CommandRequest_TvDBSearchAnime(AnimeID, false);
                cmd.Save();

                // check for Trakt associations
                if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                {
                    CommandRequest_TraktSearchAnime cmd2 = new CommandRequest_TraktSearchAnime(AnimeID, false);
                    cmd2.Save();
                }

                if (AnimeType == (int) Shoko.Models.Enums.AnimeType.Movie)
                {
                    CommandRequest_MovieDBSearchAnime cmd3 =
                        new CommandRequest_MovieDBSearchAnime(AnimeID, false);
                    cmd3.Save();
                }
            }

            return series;
        }

        public static void GetRelatedAnimeRecursive(int animeID, ref List<SVR_AniDB_Anime> relList, ref List<int> relListIDs, ref List<int> searchedIDs)
        {
            SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(animeID);
            searchedIDs.Add(animeID);

            foreach (AniDB_Anime_Relation rel in anime.GetRelatedAnime())
            {
                string relationtype = rel.RelationType.ToLower();
                if (SVR_AnimeGroup.IsRelationTypeInExclusions(relationtype)) continue;
                SVR_AniDB_Anime relAnime = Repo.AniDB_Anime.GetByAnimeID(rel.RelatedAnimeID);
                if (relAnime != null && !relListIDs.Contains(relAnime.AnimeID))
                {
                    if (SVR_AnimeGroup.IsRelationTypeInExclusions(relAnime.GetAnimeTypeDescription().ToLower()))
                        continue;
                    relList.Add(relAnime);
                    relListIDs.Add(relAnime.AnimeID);
                    if (!searchedIDs.Contains(rel.RelatedAnimeID))
                        GetRelatedAnimeRecursive(rel.RelatedAnimeID, ref relList, ref relListIDs,
                            ref searchedIDs);
                }
            }
        }

        public static void UpdateStatsByAnimeID(int id)
        {
            SVR_AniDB_Anime an = Repo.AniDB_Anime.GetByAnimeID(id);
            if (an != null)
                Repo.AniDB_Anime.BeginUpdate(an).Commit();
            SVR_AnimeSeries series = Repo.AnimeSeries.GetByAnimeID(id);

            if (series != null)
            {
                using (var serupd = Repo.AnimeSeries.BeginUpdate(series))
                {
                    serupd.Entity.UpdateStats(true, true, false);
                    serupd.Commit((true, false, false, true));
                }
            }
        }


    }
}