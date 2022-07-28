using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Metro;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.LZ4;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using TvDbSharper.Dto;

namespace Shoko.Server.Extensions
{
    public static class ModelProviders
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

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
                FileSize = anifile.FileSize,
                Username = Constants.AnonWebCacheUsername,
                AuthGUID = string.Empty
            };

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
                FileSize = vl.FileSize,
                Username = Constants.AnonWebCacheUsername,
                AuthGUID = string.Empty
            };

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
            //Cleanup any File subtitles from media information.
            Media m = new Media(v.VideoLocalID, v.Media);
            if (m.Parts != null && m.Parts.Count > 0)
            {
                foreach (Part p in m.Parts)
                {
                    if (p.Streams != null)
                    {
                        List<Stream> streams = p.Streams
                            .Where(a => a.StreamType == 3 && !string.IsNullOrEmpty(a.File))
                            .ToList();
                        if (streams.Count > 0)
                            streams.ForEach(a => p.Streams.Remove(a));
                    }
                }
            }
            //Cleanup the VideoLocal id
            byte[] data = CompressionHelper.SerializeObject(m, out int outsize);
            m.Id = 0;
            Azure_Media_Request r = new Azure_Media_Request
            {
                ED2K = v.ED2KHash,
                Version = SVR_VideoLocal.MEDIA_VERSION,
                Username = Constants.AnonWebCacheUsername,
                AuthGUID = string.Empty,
                MediaInfo = new byte[data.Length + 4]
            };
            r.MediaInfo[0] = (byte)(outsize >> 24);
            r.MediaInfo[1] = (byte)((outsize >> 16) & 0xFF);
            r.MediaInfo[2] = (byte)((outsize >> 8) & 0xFF);
            r.MediaInfo[3] = (byte)(outsize & 0xFF);
            Array.Copy(data, 0, r.MediaInfo, 4, data.Length);
            

            return r;
        }

        public static Azure_Media_Request ToMediaRequest(this Media m, string ed2k)
        {
            byte[] data = CompressionHelper.SerializeObject(m, out int outsize);
            Azure_Media_Request r = new Azure_Media_Request
            {
                ED2K = ed2k,
                MediaInfo = new byte[data.Length + 4],
                Version = SVR_VideoLocal.MEDIA_VERSION,
                Username = Constants.AnonWebCacheUsername,
                AuthGUID = string.Empty
            };
            r.MediaInfo[0] = (byte)(outsize >> 24);
            r.MediaInfo[1] = (byte)((outsize >> 16) & 0xFF);
            r.MediaInfo[2] = (byte)((outsize >> 8) & 0xFF);
            r.MediaInfo[3] = (byte)(outsize & 0xFF);
            Array.Copy(data, 0, r.MediaInfo, 4, data.Length);
            
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
                Username = Constants.AnonWebCacheUsername,
                AuthGUID = string.Empty
            };
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
                Username = Constants.AnonWebCacheUsername,
                AuthGUID = string.Empty
            };
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
                Username = Constants.AnonWebCacheUsername,
            };
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
            m.Rating = (int) Math.Round(result.Rating * 10D);
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

        public static void Populate(this TvDB_Episode episode, EpisodeRecord apiEpisode)
        {
            episode.Id = apiEpisode.Id;
            episode.SeriesID =apiEpisode.SeriesId;
            episode.SeasonID = 0;
            episode.SeasonNumber = apiEpisode.AiredSeason ?? 0;
            episode.EpisodeNumber = apiEpisode.AiredEpisodeNumber ?? 0;

            int flag = 0;
            if (apiEpisode.Filename != string.Empty)
                flag = 1;

            episode.EpImgFlag = flag;
            episode.AbsoluteNumber = apiEpisode.AbsoluteNumber ?? 0;
            episode.EpisodeName = apiEpisode.EpisodeName ?? string.Empty;
            episode.Overview = apiEpisode.Overview;
            episode.Filename = apiEpisode.Filename ?? string.Empty;
            episode.AirsAfterSeason = apiEpisode.AirsAfterSeason;
            episode.AirsBeforeEpisode = apiEpisode.AirsBeforeEpisode;
            episode.AirsBeforeSeason = apiEpisode.AirsBeforeSeason;
            if (apiEpisode.SiteRating != null) episode.Rating = (int) Math.Round(apiEpisode.SiteRating.Value);
            if (!string.IsNullOrEmpty(apiEpisode.FirstAired))
            {
                episode.AirDate = DateTime.ParseExact(apiEpisode.FirstAired, "yyyy-MM-dd", DateTimeFormatInfo.InvariantInfo);
            }
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

            return string.Empty;
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
                logger.Error(ex, "Error in TvDB_Episode.TryGetProperty: " + ex);
            }

            return string.Empty;
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
                logger.Error(ex, "Error in TvDB_Series.TryGetProperty: " + ex);
            }

            return string.Empty;
        }

        [Obsolete("Populate XmlNode is deprecated, please use Populate TvDbSharper.Series.Image instead.")]
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
                fanart.VignettePath = node["VignettePath"].InnerText;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImageFanart.Init: " + ex);
                return false;
            }
        }

        public static bool Populate(this TvDB_ImageFanart fanart, int seriesID, Image image)
        {
            try
            {
                fanart.SeriesID = seriesID;
                fanart.Id = image.Id;
                fanart.BannerPath = image.FileName;
                fanart.BannerType2 = image.Resolution;
                fanart.Colors = string.Empty;
                fanart.VignettePath = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImageFanart.Init: " + ex);
                return false;
            }
        }

        public static bool Populate(this TvDB_ImagePoster poster, int seriesID, Image image)
        {
            try
            {
                poster.SeriesID = seriesID;
                poster.SeasonNumber = null;
                poster.Id = image.Id;
                poster.BannerPath = image.FileName;
                poster.BannerType = image.KeyType;
                poster.BannerType2 = image.Resolution;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImagePoster.Populate: " + ex);
                return false;
            }
        }

        public static bool Populate(this TvDB_ImageWideBanner poster, int seriesID, Image image)
        {
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

                poster.Id = image.Id;
                poster.BannerPath = image.FileName;
                poster.BannerType = image.KeyType;
                poster.BannerType2 = image.Resolution;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error in TvDB_ImageWideBanner.Populate: " + ex);
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

        [Obsolete("PopulateFromSeriesInfo XmlDocument is deprecated, please use PopulateFromSeriesInfo TvDbSharper.Series instead.")]
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

        public static void PopulateFromSeriesInfo(this TvDB_Series series, Series apiSeries)
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
            if (apiSeries.SiteRating != null) series.Rating = (int) Math.Round(apiSeries.SiteRating.Value * 10);
        }

        [Obsolete("Populate XmlNode is deprecated, please use Populate TvDbSharper.SeriesSearchResult instead.")]
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

        public static void Populate(this TVDB_Series_Search_Response response, SeriesSearchResult series)
        {
            response.Id = string.Empty;
            response.SeriesID = series.Id;
            response.SeriesName = series.SeriesName;
            response.Overview = series.Overview;
            response.Banner = series.Banner;
            response.Language = string.Intern("en");
        }

        public static Metro_AniDB_Character ToContractMetro(this AniDB_Character character,
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

                ImageType = (int)ImageEntityType.AniDB_Character,
                ImageID = character.AniDB_CharacterID
            };
            AniDB_Seiyuu seiyuu = character.GetSeiyuu();
            if (seiyuu != null)
            {
                contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
                contract.SeiyuuName = seiyuu.SeiyuuName;
                contract.SeiyuuImageType = (int) ImageEntityType.AniDB_Creator;
                contract.SeiyuuImageID = seiyuu.AniDB_SeiyuuID;
            }

            return contract;
        }

        public static Azure_AnimeCharacter ToContractAzure(this AniDB_Character character,
            AniDB_Anime_Character charRel)
        {
            var handler = ShokoServer.ServiceContainer.GetRequiredService<IUDPConnectionHandler>();
            var contract = new Azure_AnimeCharacter
            {
                CharID = character.CharID,
                CharName = character.CharName,
                CharKanjiName = character.CharKanjiName,
                CharDescription = character.CharDescription,
                CharType = charRel.CharType,
                CharImageURL = string.Format(handler.ImageServerUrl, character.PicName),
            };
            var seiyuu = character.GetSeiyuu();
            if (seiyuu == null) return contract;
            contract.SeiyuuID = seiyuu.AniDB_SeiyuuID;
            contract.SeiyuuName = seiyuu.SeiyuuName;
            contract.SeiyuuImageURL = string.Format(handler.ImageServerUrl, seiyuu.PicName);

            return contract;
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

            agroup.Description = anime.Description;
            string name = series.GetSeriesName();
            agroup.GroupName = name;
            agroup.SortName = name;
            agroup.MainAniDBAnimeID = series.AniDB_ID;
            agroup.DateTimeUpdated = now;
            agroup.DateTimeCreated = now;
        }

        public static void Populate(this SVR_AnimeGroup agroup, SVR_AniDB_Anime anime, DateTime now)
        {
            agroup.Description = anime.Description;
            string name = anime.GetFormattedTitle();
            agroup.GroupName = name;
            agroup.SortName = name;
            agroup.MainAniDBAnimeID = anime.AnimeID;
            agroup.DateTimeUpdated = now;
            agroup.DateTimeCreated = now;
        }

        public static void Populate(this SVR_AnimeEpisode animeep, AniDB_Episode anidbEp)
        {
            animeep.AniDB_EpisodeID = anidbEp.EpisodeID;
            animeep.DateTimeUpdated = DateTime.Now;
            animeep.DateTimeCreated = DateTime.Now;
        }

        public static CrossRef_AniDB_TvDBV2 ToV2Model(this CrossRef_AniDB_TvDB xref)
        {
            return new CrossRef_AniDB_TvDBV2
            {
                AnimeID = xref.AniDBID,
                CrossRefSource = (int) xref.CrossRefSource,
                TvDBID = xref.TvDBID
            };
        }

        public static (int season, int episodeNumber) GetNextEpisode(this TvDB_Episode ep)
        {
            if (ep == null) return (0, 0);
            int epsInSeason = RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(ep.SeriesID, ep.SeasonNumber);
            if (ep.EpisodeNumber == epsInSeason)
            {
                int numberOfSeasons = RepoFactory.TvDB_Episode.getLastSeasonForSeries(ep.SeriesID);
                if (ep.SeasonNumber == numberOfSeasons) return (0, 0);
                return (ep.SeasonNumber + 1, 1);
            }

            return (ep.SeasonNumber, ep.EpisodeNumber + 1);
        }

        public static (int season, int episodeNumber) GetPreviousEpisode(this TvDB_Episode ep)
        {
            // check bounds and exit
            if (ep.SeasonNumber == 1 && ep.EpisodeNumber == 1) return (0, 0);
            // self explanatory
            if (ep.EpisodeNumber > 1) return (ep.SeasonNumber, ep.EpisodeNumber - 1);

            // episode number is 1
            // get the last episode of last season
            int epsInSeason = RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(ep.SeriesID, ep.SeasonNumber - 1);
            return (ep.SeasonNumber - 1, epsInSeason);
        }

        public static int GetAbsoluteEpisodeNumber(this TvDB_Episode ep)
        {
            if (ep.SeasonNumber == 1 || ep.SeasonNumber == 0) return ep.EpisodeNumber;
            int number = ep.EpisodeNumber;
            for (int season = 1; season < RepoFactory.TvDB_Episode.getLastSeasonForSeries(ep.SeriesID); season++)
                number += RepoFactory.TvDB_Episode.GetNumberOfEpisodesForSeason(ep.SeriesID, ep.SeasonNumber);

            return number;
        }
    }
}
