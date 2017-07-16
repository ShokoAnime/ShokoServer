using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using AniDBAPI;
using Force.DeepCloner;
using NHibernate;
using NLog;
using Shoko.Models;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.AniDB_API.Raws;
using Shoko.Server.Models;
using Shoko.Server.LZ4;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV.Contracts;

namespace Shoko.Server.Extensions
{
    public static class ModelProviders
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();


        public static Azure_CrossRef_AniDB_MAL_Request ToRequest(this CrossRef_AniDB_MAL c)
        {
            return new Azure_CrossRef_AniDB_MAL_Request
            {
                CrossRef_AniDB_MALID = c.CrossRef_AniDB_MALID,
                AnimeID = c.AnimeID,
                MALID = c.MALID,
                MALTitle = c.MALTitle,
                StartEpisodeType = c.StartEpisodeType,
                StartEpisodeNumber = c.StartEpisodeNumber,
                CrossRefSource = c.CrossRefSource
            };
        }

        public static Azure_CrossRef_AniDB_Other_Request ToRequest(this CrossRef_AniDB_Other c)
        {
            return new Azure_CrossRef_AniDB_Other_Request
            {
                CrossRef_AniDB_OtherID = c.CrossRef_AniDB_OtherID,
                AnimeID = c.AnimeID,
                CrossRefID = c.CrossRefID,
                CrossRefSource = c.CrossRefSource,
                CrossRefType = c.CrossRefType,
            };
        }

        public static Azure_FileHash_Request ToHashRequest(this AniDB_File anifile)
        {
            Azure_FileHash_Request r = new Azure_FileHash_Request
            {
                ED2K = anifile.Hash,
                CRC32 = anifile.CRC,
                MD5 = anifile.MD5,
                SHA1 = anifile.SHA1,
                FileSize = anifile.FileSize
            };
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }

        public static Azure_FileHash_Request ToHashRequest(this SVR_VideoLocal vl)
        {
            Azure_FileHash_Request r = new Azure_FileHash_Request
            {
                ED2K = vl.Hash,
                CRC32 = vl.CRC32,
                MD5 = vl.MD5,
                SHA1 = vl.SHA1,
                FileSize = vl.FileSize
            };
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }

        public static Media ToMedia(this Azure_Media m)
        {
            int size = (m.MediaInfo[0] << 24) | (m.MediaInfo[1] << 16) | (m.MediaInfo[2] << 8) | m.MediaInfo[3];
            byte[] data = new byte[m.MediaInfo.Length - 4];
            Array.Copy(m.MediaInfo, 4, data, 0, data.Length);
            return CompressionHelper.DeserializeObject<Media>(data, size);
        }

        public static Azure_Media_Request ToMediaRequest(this SVR_VideoLocal v)
        {
            Azure_Media_Request r = new Azure_Media_Request
            {
                ED2K = v.ED2KHash
            };
            //Cleanup any File subtitles from media information.
            Media m = v.Media.DeepClone();
            if (m.Parts != null && m.Parts.Count > 0)
            {
                foreach (Part p in m.Parts)
                {
                    if (p.Streams != null)
                    {
                        List<Stream> streams = p.Streams
                            .Where(a => a.StreamType == "3" && !string.IsNullOrEmpty(a.File))
                            .ToList();
                        if (streams.Count > 0)
                            streams.ForEach(a => p.Streams.Remove(a));
                    }
                }
            }
            //Cleanup the VideoLocal id
            m.Id = null;
            byte[] data = CompressionHelper.SerializeObject(m, out int outsize);
            r.ED2K = v.ED2KHash;
            r.MediaInfo = new byte[data.Length + 4];
            r.MediaInfo[0] = (byte)(outsize >> 24);
            r.MediaInfo[1] = (byte)((outsize >> 16) & 0xFF);
            r.MediaInfo[2] = (byte)((outsize >> 8) & 0xFF);
            r.MediaInfo[3] = (byte)(outsize & 0xFF);
            Array.Copy(data, 0, r.MediaInfo, 4, data.Length);
            r.Version = SVR_VideoLocal.MEDIA_VERSION;
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }

        public static Azure_Media_Request ToMediaRequest(this Media m, string ed2k)
        {
            Azure_Media_Request r = new Azure_Media_Request();
            byte[] data = CompressionHelper.SerializeObject(m, out int outsize);
            r.ED2K = ed2k;
            r.MediaInfo = new byte[data.Length + 4];
            r.MediaInfo[0] = (byte)(outsize >> 24);
            r.MediaInfo[1] = (byte)((outsize >> 16) & 0xFF);
            r.MediaInfo[2] = (byte)((outsize >> 8) & 0xFF);
            r.MediaInfo[3] = (byte)(outsize & 0xFF);
            Array.Copy(data, 0, r.MediaInfo, 4, data.Length);
            r.Version = SVR_VideoLocal.MEDIA_VERSION;
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
            return r;
        }

        public static Azure_CrossRef_AniDB_Trakt_Request ToRequest(this CrossRef_AniDB_TraktV2 xref, string animeName)
        {
            Azure_CrossRef_AniDB_Trakt_Request r = new Azure_CrossRef_AniDB_Trakt_Request
            {
                AnimeID = xref.AnimeID,
                AnimeName = animeName,
                AniDBStartEpisodeType = xref.AniDBStartEpisodeType,
                AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber,
                TraktID = xref.TraktID,
                TraktSeasonNumber = xref.TraktSeasonNumber,
                TraktStartEpisodeNumber = xref.TraktStartEpisodeNumber,
                TraktTitle = xref.TraktTitle,
                CrossRefSource = xref.CrossRefSource,

                Username = ServerSettings.AniDB_Username
            };
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;

            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
            return r;
        }

        public static Azure_CrossRef_AniDB_TvDB_Request ToRequest(this CrossRef_AniDB_TvDBV2 xref, string animeName)
        {
            Azure_CrossRef_AniDB_TvDB_Request r = new Azure_CrossRef_AniDB_TvDB_Request
            {
                AnimeID = xref.AnimeID,
                AnimeName = animeName,
                AniDBStartEpisodeType = xref.AniDBStartEpisodeType,
                AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber,
                TvDBID = xref.TvDBID,
                TvDBSeasonNumber = xref.TvDBSeasonNumber,
                TvDBStartEpisodeNumber = xref.TvDBStartEpisodeNumber,
                TvDBTitle = xref.TvDBTitle,
                CrossRefSource = xref.CrossRefSource,
                Username = ServerSettings.AniDB_Username
            };
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
            return r;
        }

        public static Azure_CrossRef_File_Episode_Request ToRequest(this CrossRef_File_Episode xref)
        {
            Azure_CrossRef_File_Episode_Request r = new Azure_CrossRef_File_Episode_Request
            {
                Hash = xref.Hash,
                AnimeID = xref.AnimeID,
                EpisodeID = xref.EpisodeID,
                Percentage = xref.Percentage,
                EpisodeOrder = xref.EpisodeOrder,

                Username = ServerSettings.AniDB_Username
            };
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            return r;
        }

        public static FileNameHash ToFileNameHash(this CrossRef_File_Episode cfe)
        {
            return new FileNameHash
            {
                FileName = cfe.FileName,
                FileSize = cfe.FileSize,
                Hash = cfe.Hash,
                DateTimeUpdated = DateTime.Now,
            };
        }

        public static void Populate(this MovieDB_Fanart m, MovieDB_Image_Result result, int movieID)
        {
            m.MovieId = movieID;
            m.ImageID = result.ImageID;
            m.ImageType = result.ImageType;
            m.ImageSize = result.ImageSize;
            m.ImageWidth = result.ImageWidth;
            m.ImageHeight = result.ImageHeight;
            m.Enabled = 1;
        }

        public static void Populate(this MovieDB_Movie m, MovieDB_Movie_Result result)
        {
            m.MovieId = result.MovieID;
            m.MovieName = result.MovieName;
            m.OriginalName = result.OriginalName;
            m.Overview = result.Overview;
        }

        public static void Populate(this MovieDB_Poster m, MovieDB_Image_Result result, int movieID)
        {
            m.MovieId = movieID;
            m.ImageID = result.ImageID;
            m.ImageType = result.ImageType;
            m.ImageSize = result.ImageSize;
            m.URL = result.URL;
            m.ImageWidth = result.ImageWidth;
            m.ImageHeight = result.ImageHeight;
            m.Enabled = 1;
        }

        public static void Populate(this Trakt_Friend friend, TraktV2User user)
        {
            friend.Username = user.username;
            friend.FullName = user.name;
            friend.LastAvatarUpdate = DateTime.Now;
        }

        public static void Populate(this Trakt_Show show, TraktV2ShowExtended tvshow)
        {
            show.Overview = tvshow.overview;
            show.Title = tvshow.title;
            show.TraktID = tvshow.ids.slug;
            show.TvDB_ID = tvshow.ids.tvdb;
            show.URL = tvshow.ShowURL;
            show.Year = tvshow.year.ToString();
        }

        public static void Populate(this Trakt_Show show, TraktV2Show tvshow)
        {
            show.Overview = tvshow.Overview;
            show.Title = tvshow.Title;
            show.TraktID = tvshow.ids.slug;
            show.TvDB_ID = tvshow.ids.tvdb;
            show.URL = tvshow.ShowURL;
            show.Year = tvshow.Year.ToString();
        }

        [System.Obsolete("This method is deprecated", false)]
        public static void Populate(this TvDB_Episode episode, XmlDocument doc)
        {
            // used when getting information from episode info
            // http://thetvdb.com/api/B178B8940CAF4A2C/episodes/306542/en.xml

            episode.Id = int.Parse(TryGetEpisodeProperty(doc, "id"));
            episode.SeriesID = int.Parse(TryGetEpisodeProperty(doc, "seriesid"));
            episode.SeasonID = int.Parse(TryGetEpisodeProperty(doc, "seasonid"));
            episode.SeasonNumber = int.Parse(TryGetEpisodeProperty(doc, "SeasonNumber"));
            episode.EpisodeNumber = int.Parse(TryGetEpisodeProperty(doc, "EpisodeNumber"));

            if (int.TryParse(TryGetEpisodeProperty(doc, "EpImgFlag"), out int flag))
                episode.EpImgFlag = flag;

            if (int.TryParse(TryGetEpisodeProperty(doc, "absolute_number"), out int abnum))
                episode.AbsoluteNumber = abnum;

            episode.EpisodeName = TryGetEpisodeProperty(doc, "EpisodeName");
            episode.Overview = TryGetEpisodeProperty(doc, "Overview");
            episode.Filename = TryGetEpisodeProperty(doc, "filename");
            //this.FirstAired = TryGetProperty(doc, "FirstAired");

            if (int.TryParse(TryGetEpisodeProperty(doc, "airsafter_season"), out int aas))
                episode.AirsAfterSeason = aas;
            else
                episode.AirsAfterSeason = null;

            if (int.TryParse(TryGetEpisodeProperty(doc, "airsbefore_episode"), out int abe))
                episode.AirsBeforeEpisode = abe;
            else
                episode.AirsBeforeEpisode = null;

            if (int.TryParse(TryGetEpisodeProperty(doc, "airsbefore_season"), out int abs))
                episode.AirsBeforeSeason = abs;
            else
                episode.AirsBeforeSeason = null;
        }

        public static void Populate(this TvDB_Episode episode, int seriesId, TvDbSharper.Clients.Series.Json.BasicEpisode apiEpisode)
        {
            // used when getting information from episode info
            // http://thetvdb.com/api/B178B8940CAF4A2C/episodes/306542/en.xml

            episode.Id = apiEpisode.Id;
            episode.SeriesID = seriesId;
            episode.SeasonID = 0;
            episode.SeasonNumber = apiEpisode.AiredSeason ?? 0;
            episode.EpisodeNumber = apiEpisode.AiredEpisodeNumber ?? 0;
            episode.EpImgFlag = 0;
            episode.AbsoluteNumber = apiEpisode.AbsoluteNumber ?? 0;

            episode.EpisodeName = apiEpisode.EpisodeName;
            episode.Overview = apiEpisode.Overview;
        }

        public static void Populate(this TvDB_Episode episode, TvDbSharper.Clients.Episodes.Json.EpisodeRecord apiEpisode)
        {
            episode.Id = apiEpisode.Id;
            episode.SeriesID = int.Parse(apiEpisode.SeriesId);
            episode.SeasonID = 0;
            episode.SeasonNumber = apiEpisode.AiredSeason ?? 0;
            episode.EpisodeNumber = apiEpisode.AiredEpisodeNumber ?? 0;

            int flag = 0;
            if (apiEpisode.Filename != string.Empty)
                flag = 1;

            episode.EpImgFlag = flag;
            episode.AbsoluteNumber = apiEpisode.AbsoluteNumber ?? 0;
            episode.EpisodeName = apiEpisode.EpisodeName;
            episode.Overview = apiEpisode.Overview;
            episode.Filename = apiEpisode.Filename;
            episode.AirsAfterSeason = apiEpisode.AirsAfterSeason;
            episode.AirsBeforeEpisode = apiEpisode.AirsBeforeEpisode;
            episode.AirsBeforeSeason = apiEpisode.AirsBeforeSeason;
        }

        public static void Populate(this TvDB_Episode episode, XmlNode node)
        {
            // used when getting information from full series info
            // http://thetvdb.com/api/B178B8940CAF4A2C/series/84187/all/en.xml

            episode.Id = int.Parse(TryGetProperty(node, "id"));
            episode.SeriesID = int.Parse(TryGetProperty(node, "seriesid"));
            episode.SeasonID = int.Parse(TryGetProperty(node, "seasonid"));
            episode.SeasonNumber = int.Parse(TryGetProperty(node, "SeasonNumber"));
            episode.EpisodeNumber = int.Parse(TryGetProperty(node, "EpisodeNumber"));

            if (int.TryParse(TryGetProperty(node, "EpImgFlag"), out int flag))
                episode.EpImgFlag = flag;

            if (int.TryParse(TryGetProperty(node, "absolute_number"), out int abnum))
                episode.AbsoluteNumber = abnum;

            episode.EpisodeName = TryGetProperty(node, "EpisodeName");
            episode.Overview = TryGetProperty(node, "Overview");
            episode.Filename = TryGetProperty(node, "filename");
            //this.FirstAired = TryGetProperty(node, "FirstAired");

            if (int.TryParse(TryGetProperty(node, "airsafter_season"), out int aas))
                episode.AirsAfterSeason = aas;
            else
                episode.AirsAfterSeason = null;

            if (int.TryParse(TryGetProperty(node, "airsbefore_episode"), out int abe))
                episode.AirsBeforeEpisode = abe;
            else
                episode.AirsBeforeEpisode = null;

            if (int.TryParse(TryGetProperty(node, "airsbefore_season"), out int abs))
                episode.AirsBeforeSeason = abs;
            else
                episode.AirsBeforeSeason = null;
        }

        private static string TryGetProperty(XmlNode node, string propertyName)
        {
            try
            {
                string prop = node[propertyName].InnerText.Trim();
                return prop;
            }
            catch
            {
                //logger.Error( ex,"Error in TvDB_Episode.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        private static string TryGetEpisodeProperty(XmlDocument doc, string propertyName)
        {
            try
            {
                string prop = doc["Data"]["Episode"][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_Episode.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        private static string TryGetSeriesProperty(XmlDocument doc, string propertyName)
        {
            try
            {
                string prop = doc["Data"]["Series"][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_Series.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        [System.Obsolete("Populate XmlNode is deprecated, please use Populate TvDbSharper.Series.Image instead.")]
        public static bool Populate(this TvDB_ImageFanart fanart, int seriesID, XmlNode node)
        {
            try
            {
                fanart.SeriesID = seriesID;
                fanart.Id = int.Parse(node["id"].InnerText);
                fanart.BannerPath = node["BannerPath"].InnerText;
                fanart.BannerType = node["BannerType"].InnerText;
                fanart.BannerType2 = node["BannerType2"].InnerText;
                fanart.Colors = node["Colors"].InnerText;
                fanart.Language = node["Language"].InnerText;
                fanart.ThumbnailPath = node["ThumbnailPath"].InnerText;
                fanart.VignettePath = node["VignettePath"].InnerText;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImageFanart.Init: " + ex.ToString());
                return false;
            }
        }

        public static bool Populate(this TvDB_ImageFanart fanart, int seriesID, TvDbSharper.Clients.Series.Json.Image image)
        {
            if (image.Id == null) {
                logger.Error("Error in TvDB_ImageFanart.Populate, image.Id is null, series: {0}",seriesID);
                return false;
            }
            try
            {
                fanart.SeriesID = seriesID;
                fanart.Id = image.Id ?? 0;
                fanart.BannerPath = image.FileName;
                fanart.BannerType2 = image.Resolution;
                fanart.Colors = string.Empty;
                fanart.ThumbnailPath = image.Thumbnail;
                fanart.VignettePath = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImageFanart.Init: " + ex.ToString());
                return false;
            }
        }

        public static bool Populate(this TvDB_ImagePoster poster, int seriesID, TvDbSharper.Clients.Series.Json.Image image)
        {
            if (image.Id == null)
            {
                logger.Error("Error in TvDB_ImagePoster.Populate, image.Id is null, series: {0}", seriesID);
                return false;
            }
            try
            {
                poster.SeriesID = seriesID;
                poster.SeasonNumber = null;
                poster.Id = image.Id ?? 0;
                poster.BannerPath = image.FileName;
                poster.BannerType = image.KeyType;
                poster.BannerType2 = image.Resolution;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImagePoster.Populate: " + ex.ToString());
                return false;
            }
        }

        public static bool Populate(this TvDB_ImageWideBanner poster, int seriesID, TvDbSharper.Clients.Series.Json.Image image)
        {
            if (image.Id == null)
            {
                logger.Error("Error in TvDB_ImageWideBanner.Populate, image.Id is null, series: {0}", seriesID);
                return false;
            }
            try
            {
                poster.SeriesID = seriesID;
                try
                {
                    poster.SeasonNumber = int.Parse(image.SubKey);
                }
                catch (FormatException)
                {
                    poster.SeasonNumber = null;
                }

                poster.Id = image.Id ?? 0;
                poster.BannerPath = image.FileName;
                poster.BannerType = image.KeyType;
                poster.BannerType2 = image.Resolution;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImageWideBanner.Populate: " + ex.ToString());
                return false;
            }
        }

        public static void PopulateFromSearch(this TvDB_Series series, XmlDocument doc)
        {
            series.SeriesID = 0;
            series.Overview = string.Empty;
            series.SeriesName = string.Empty;
            series.Status = string.Empty;
            series.Banner = string.Empty;
            series.Fanart = string.Empty;
            series.Lastupdated = string.Empty;
            series.Poster = string.Empty;
            series.SeriesID = int.Parse(TryGetSeriesProperty(doc, "seriesid"));
            series.SeriesName = TryGetSeriesProperty(doc, "SeriesName");
            series.Overview = TryGetSeriesProperty(doc, "Overview");
            series.Banner = TryGetSeriesProperty(doc, "banner");
        }

        [System.Obsolete("PopulateFromSeriesInfo XmlDocument is deprecated, please use PopulateFromSeriesInfo TvDbSharper.Series instead.")]
        public static void PopulateFromSeriesInfo(this TvDB_Series series, XmlDocument doc)
        {
            series.SeriesID = 0;
            series.Overview = string.Empty;
            series.SeriesName = string.Empty;
            series.Status = string.Empty;
            series.Banner = string.Empty;
            series.Fanart = string.Empty;
            series.Lastupdated = string.Empty;
            series.Poster = string.Empty;
            series.SeriesID = int.Parse(TryGetSeriesProperty(doc, "id"));
            series.SeriesName = TryGetSeriesProperty(doc, "SeriesName");
            series.Overview = TryGetSeriesProperty(doc, "Overview");
            series.Banner = TryGetSeriesProperty(doc, "banner");

            series.Status = TryGetSeriesProperty(doc, "Status");
            series.Fanart = TryGetSeriesProperty(doc, "fanart");
            series.Lastupdated = TryGetSeriesProperty(doc, "lastupdated");
            series.Poster = TryGetSeriesProperty(doc, "poster");
        }

        public static void PopulateFromSeriesInfo(this TvDB_Series series, TvDbSharper.Clients.Series.Json.Series apiSeries)
        {
            series.SeriesID = 0;
            series.Overview = string.Empty;
            series.SeriesName = string.Empty;
            series.Status = string.Empty;
            series.Banner = string.Empty;
            series.Fanart = string.Empty;
            series.Lastupdated = string.Empty;
            series.Poster = string.Empty;

            series.SeriesID = apiSeries.Id;
            series.SeriesName = apiSeries.SeriesName;
            series.Overview = apiSeries.Overview;
            series.Banner = apiSeries.Banner;
            series.Status = apiSeries.Status;
            series.Lastupdated = apiSeries.LastUpdated.ToString();
        }

        [System.Obsolete("Populate XmlNode is deprecated, please use Populate TvDbSharper.SeriesSearchResult instead.")]
        public static void Populate(this TVDB_Series_Search_Response response, XmlNode series)
        {
            response.Id = string.Empty;
            response.SeriesID = 0;
            response.Overview = string.Empty;
            response.SeriesName = string.Empty;
            response.Banner = string.Empty;
            if (series["seriesid"] != null) response.SeriesID = int.Parse(series["seriesid"].InnerText);
            if (series["SeriesName"] != null) response.SeriesName = series["SeriesName"].InnerText;
            if (series["id"] != null) response.Id = series["id"].InnerText;
            if (series["Overview"] != null) response.Overview = series["Overview"].InnerText;
            if (series["banner"] != null) response.Banner = series["banner"].InnerText;
            if (series["language"] != null) response.Language = series["language"].InnerText;
        }

        public static void Populate(this TVDB_Series_Search_Response response, TvDbSharper.Clients.Search.Json.SeriesSearchResult series)
        {
            response.Id = string.Empty;
            response.SeriesID = series.Id;
            response.SeriesName = series.SeriesName;
            response.Overview = series.Overview;
            response.Banner = series.Banner;
            response.Language = string.Empty;
        }

        public static void Populate(this AniDB_Anime_Character character, Raw_AniDB_Character rawChar)
        {
            character.CharID = rawChar.CharID;
            character.AnimeID = rawChar.AnimeID;
            character.CharType = rawChar.CharType;
            character.EpisodeListRaw = rawChar.EpisodeListRaw;
        }

        public static void Populate(this AniDB_Anime_Relation rel, Raw_AniDB_RelatedAnime rawRel)
        {
            rel.AnimeID = rawRel.AnimeID;
            rel.RelatedAnimeID = rawRel.RelatedAnimeID;
            rel.RelationType = rawRel.RelationType;
        }

        public static void Populate(this AniDB_Anime_Similar similar, Raw_AniDB_SimilarAnime rawSim)
        {
            similar.AnimeID = rawSim.AnimeID;
            similar.Approval = rawSim.Approval;
            similar.Total = rawSim.Total;
            similar.SimilarAnimeID = rawSim.SimilarAnimeID;
        }

        public static void Populate(this AniDB_Anime_Tag tag, Raw_AniDB_Tag rawTag)
        {
            tag.AnimeID = rawTag.AnimeID;
            tag.TagID = rawTag.TagID;
            tag.Approval = 100;
            tag.Weight = rawTag.Weight;
        }

        public static void Populate(this AniDB_Anime_Title title, Raw_AniDB_Anime_Title rawTitle)
        {
            title.AnimeID = rawTitle.AnimeID;
            title.Language = rawTitle.Language;
            title.Title = rawTitle.Title;
            title.TitleType = rawTitle.TitleType;
        }

        private static void Populate(this AniDB_Character character, Raw_AniDB_Character rawChar)
        {
            character.CharID = rawChar.CharID;
            character.CharDescription = rawChar.CharDescription;
            character.CharKanjiName = rawChar.CharKanjiName;
            character.CharName = rawChar.CharName;
            character.PicName = rawChar.PicName;
            character.CreatorListRaw = rawChar.CreatorListRaw;
        }

        public static void PopulateFromHTTP(this AniDB_Character character, Raw_AniDB_Character rawChar)
        {
            if (character.CharID == 0) // a new object
            {
                character.Populate(rawChar);
            }
            else
            {
                // only update the fields that come from HTTP API
                character.CharDescription = rawChar.CharDescription;
                character.CharName = rawChar.CharName;
                character.CreatorListRaw = rawChar.CreatorListRaw;
                character.PicName = rawChar.PicName;
            }
        }

        public static void PopulateFromUDP(this AniDB_Character character, Raw_AniDB_Character rawChar)
        {
            if (character.CharID == 0) // a new object
            {
                character.Populate(rawChar);
            }
            else
            {
                // only update the fields that com from UDP API
                character.CharKanjiName = rawChar.CharKanjiName;
                character.CharName = rawChar.CharName;
                //this.CreatorListRaw = rawChar.CreatorListRaw;
            }
        }

        public static Metro_AniDB_Character ToContractMetro(this AniDB_Character character, ISession session,
            AniDB_Anime_Character charRel)
        {
            Metro_AniDB_Character contract = new Metro_AniDB_Character
            {
                AniDB_CharacterID = character.AniDB_CharacterID,
                CharID = character.CharID,
                CharName = character.CharName,
                CharKanjiName = character.CharKanjiName,
                CharDescription = character.CharDescription,

                CharType = charRel.CharType,

                ImageType = (int)JMMImageType.AniDB_Character,
                ImageID = character.AniDB_CharacterID
            };
            AniDB_Seiyuu seiyuu = character.GetSeiyuu(session);
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageType = (int) JMMImageType.AniDB_Creator;
                contract.SeiyuuImageID = seiyuu.AniDB_SeiyuuID;
            }

            return contract;
        }

        public static Azure_AnimeCharacter ToContractAzure(this AniDB_Character character,
            AniDB_Anime_Character charRel)
        {
            Azure_AnimeCharacter contract = new Azure_AnimeCharacter
            {
                CharID = character.CharID,
                CharName = character.CharName,
                CharKanjiName = character.CharKanjiName,
                CharDescription = character.CharDescription,
                CharType = charRel.CharType,
                CharImageURL = string.Format(Constants.URLS.AniDB_Images, character.PicName)
            };
            AniDB_Seiyuu seiyuu = character.GetSeiyuu();
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageURL = string.Format(Constants.URLS.AniDB_Images, seiyuu.PicName);
            }

            return contract;
        }

        public static void Populate(this AniDB_Episode episode, Raw_AniDB_Episode epInfo)
        {
            episode.AirDate = epInfo.AirDate;
            episode.AnimeID = epInfo.AnimeID;
            episode.DateTimeUpdated = DateTime.Now;
            episode.EnglishName = epInfo.EnglishName;
            episode.EpisodeID = epInfo.EpisodeID;
            episode.EpisodeNumber = epInfo.EpisodeNumber;
            episode.EpisodeType = epInfo.EpisodeType;
            episode.LengthSeconds = epInfo.LengthSeconds;
            episode.Rating = epInfo.Rating.ToString();
            episode.RomajiName = epInfo.RomajiName;
            episode.Votes = epInfo.Votes.ToString();
        }

        public static void Populate(this AniDB_GroupStatus grpstatus, Raw_AniDB_GroupStatus raw)
        {
            grpstatus.AnimeID = raw.AnimeID;
            grpstatus.GroupID = raw.GroupID;
            grpstatus.GroupName = raw.GroupName;
            grpstatus.CompletionState = raw.CompletionState;
            grpstatus.LastEpisodeNumber = raw.LastEpisodeNumber;
            grpstatus.Rating = raw.Rating;
            grpstatus.Votes = raw.Votes;
            grpstatus.EpisodeRange = raw.EpisodeRange;
        }

        public static void Populate(this AniDB_MylistStats stats, Raw_AniDB_MyListStats raw)
        {
            stats.Animes = raw.Animes;
            stats.Episodes = raw.Episodes;
            stats.Files = raw.Files;
            stats.SizeOfFiles = raw.SizeOfFiles;
            stats.AddedAnimes = raw.AddedAnimes;
            stats.AddedEpisodes = raw.AddedEpisodes;
            stats.AddedFiles = raw.AddedFiles;
            stats.AddedGroups = raw.AddedGroups;
            stats.LeechPct = raw.LeechPct;
            stats.GloryPct = raw.GloryPct;
            stats.ViewedPct = raw.ViewedPct;
            stats.MylistPct = raw.MylistPct;
            stats.ViewedMylistPct = raw.ViewedMylistPct;
            stats.EpisodesViewed = raw.EpisodesViewed;
            stats.Votes = raw.Votes;
            stats.Reviews = raw.Reviews;
            stats.ViewiedLength = raw.ViewiedLength;
        }

        public static void Populate(this AniDB_Recommendation recommendation, Raw_AniDB_Recommendation rawRec)
        {
            recommendation.AnimeID = rawRec.AnimeID;
            recommendation.UserID = rawRec.UserID;
            recommendation.RecommendationText = rawRec.RecommendationText;

            recommendation.RecommendationType = (int) AniDBRecommendationType.Recommended;

            if (rawRec.RecommendationTypeText.Equals("recommended", StringComparison.InvariantCultureIgnoreCase))
                recommendation.RecommendationType = (int) AniDBRecommendationType.Recommended;

            if (rawRec.RecommendationTypeText.Equals("for fans", StringComparison.InvariantCultureIgnoreCase))
                recommendation.RecommendationType = (int) AniDBRecommendationType.ForFans;

            if (rawRec.RecommendationTypeText.Equals("must see", StringComparison.InvariantCultureIgnoreCase))
                recommendation.RecommendationType = (int) AniDBRecommendationType.MustSee;
        }

        public static void Populate(this AniDB_ReleaseGroup releasegroup, Raw_AniDB_Group raw)
        {
            releasegroup.GroupID = raw.GroupID;
            releasegroup.Rating = raw.Rating;
            releasegroup.Votes = raw.Votes;
            releasegroup.AnimeCount = raw.AnimeCount;
            releasegroup.FileCount = raw.FileCount;
            releasegroup.GroupName = raw.GroupName;
            releasegroup.GroupNameShort = raw.GroupNameShort;
            releasegroup.IRCChannel = raw.IRCChannel;
            releasegroup.IRCServer = raw.IRCServer;
            releasegroup.URL = raw.URL;
            releasegroup.Picname = raw.Picname;
        }

        public static void Populate(this AniDB_Review review, Raw_AniDB_Review rawReview)
        {
            review.ReviewID = rawReview.ReviewID;
            review.AuthorID = rawReview.AuthorID;
            review.RatingAnimation = rawReview.RatingAnimation;
            review.RatingSound = rawReview.RatingSound;
            review.RatingStory = rawReview.RatingStory;
            review.RatingCharacter = rawReview.RatingCharacter;
            review.RatingValue = rawReview.RatingValue;
            review.RatingEnjoyment = rawReview.RatingEnjoyment;
            review.ReviewText = rawReview.ReviewText;
        }

        public static void Populate(this AniDB_Tag tag, Raw_AniDB_Tag rawTag)
        {
            tag.TagID = rawTag.TagID;
            tag.GlobalSpoiler = rawTag.GlobalSpoiler;
            tag.LocalSpoiler = rawTag.LocalSpoiler;
            tag.Spoiler = 0;
            tag.TagCount = 0;
            tag.TagDescription = rawTag.TagDescription;
            tag.TagName = rawTag.TagName;
        }

        public static void PopulateManually(this CrossRef_File_Episode cross, SVR_VideoLocal vid, SVR_AnimeEpisode ep)
        {
            cross.Hash = vid.ED2KHash;
            cross.FileName = vid.FileName;
            cross.FileSize = vid.FileSize;
            cross.CrossRefSource = (int) CrossRefSource.User;
            cross.AnimeID = ep.GetAnimeSeries().AniDB_ID;
            cross.EpisodeID = ep.AniDB_EpisodeID;
            cross.Percentage = 100;
            cross.EpisodeOrder = 1;
        }

        public static void Populate(this SVR_AnimeGroup agroup, SVR_AnimeSeries series)
        {
            agroup.Populate(series, DateTime.Now);
        }

        public static void Populate(this SVR_AnimeGroup agroup, SVR_AnimeSeries series, DateTime now)
        {
            SVR_AniDB_Anime anime = series.GetAnime();

            agroup.Populate(anime, now);
        }

        public static void Populate(this SVR_AnimeGroup agroup, SVR_AniDB_Anime anime, DateTime now)
        {
            agroup.Description = anime.Description;
            agroup.GroupName = anime.PreferredTitle;
            agroup.SortName = anime.PreferredTitle;
            agroup.DateTimeUpdated = now;
            agroup.DateTimeCreated = now;
        }

        public static void Populate(this SVR_AnimeEpisode animeep, AniDB_Episode anidbEp)
        {
            animeep.AniDB_EpisodeID = anidbEp.EpisodeID;
            animeep.DateTimeUpdated = DateTime.Now;
            animeep.DateTimeCreated = DateTime.Now;
        }
    }
}