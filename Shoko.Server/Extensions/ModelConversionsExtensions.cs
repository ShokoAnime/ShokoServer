using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Force.DeepCloner;
using NLog;
using Pri.LongPath;
using Shoko.Models;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.PlexAndKodi;
using Shoko.Models.Server;
using Shoko.Models.TvDB;
using Shoko.Server.Entities;
using Shoko.Server.ImageDownload;
using Shoko.Server.LZ4;
using Shoko.Server.Providers.MovieDB;
using Shoko.Server.Providers.TraktTV.Contracts;
using Shoko.Server.Repositories;
using CL_AniDB_Anime_DefaultImage = Shoko.Models.Client.CL_AniDB_Anime_DefaultImage;
using CL_AniDB_Anime_Relation = Shoko.Models.Client.CL_AniDB_Anime_Relation;
using CL_AniDB_Anime_Similar = Shoko.Models.Client.CL_AniDB_Anime_Similar;
using CL_AniDB_Character = Shoko.Models.Client.CL_AniDB_Character;
using CL_AniDB_GroupStatus = Shoko.Models.Client.CL_AniDB_GroupStatus;
using CL_AnimeEpisode_User = Shoko.Models.Client.CL_AnimeEpisode_User;
using CL_AnimeGroup_User = Shoko.Models.Client.CL_AnimeGroup_User;
using CL_AnimeSeries_User = Shoko.Models.Client.CL_AnimeSeries_User;
using CL_BookmarkedAnime = Shoko.Models.Client.CL_BookmarkedAnime;
using File = System.IO.File;

namespace Shoko.Server

{
    public static class ModelConversionsExtensions
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static CL_AniDB_Anime CloneToClient(this AniDB_Anime anime)
        {
            return new CL_AniDB_Anime
            {
                AniDB_AnimeID = anime.AniDB_AnimeID,
                AnimeID = anime.AnimeID,
                EpisodeCount = anime.EpisodeCount,
                AirDate = anime.AirDate,
                EndDate = anime.EndDate,
                URL = anime.URL,
                Picname = anime.Picname,
                BeginYear = anime.BeginYear,
                EndYear = anime.EndYear,
                AnimeType = anime.AnimeType,
                MainTitle = anime.MainTitle,
                AllTitles = anime.AllTitles,
                AllTags = anime.AllTags,
                EpisodeCountNormal = anime.EpisodeCountNormal,
                EpisodeCountSpecial = anime.EpisodeCountSpecial,
                Rating = anime.Rating,
                VoteCount = anime.VoteCount,
                TempRating = anime.TempRating,
                TempVoteCount = anime.TempVoteCount,
                AvgReviewRating = anime.AvgReviewRating,
                ReviewCount = anime.ReviewCount,
                DateTimeUpdated = anime.DateTimeUpdated,
                DateTimeDescUpdated = anime.DateTimeDescUpdated,
                ImageEnabled = anime.ImageEnabled,
                AwardList = anime.AwardList,
                Restricted = anime.Restricted,
                AnimePlanetID = anime.AnimePlanetID,
                ANNID = anime.ANNID,
                AllCinemaID = anime.AllCinemaID,
                AnimeNfo = anime.AnimeNfo,
                LatestEpisodeNumber = anime.LatestEpisodeNumber,
                DisableExternalLinksFlag = anime.DisableExternalLinksFlag
            };
        }

        public static CL_AniDB_Anime_DefaultImage CloneToClient(this AniDB_Anime_DefaultImage def)
        {
            return new CL_AniDB_Anime_DefaultImage
            {
                AniDB_Anime_DefaultImageID=def.AniDB_Anime_DefaultImageID,
                AnimeID=def.AnimeID,
                ImageParentID=def.ImageParentID,
                ImageParentType=def.ImageParentType,
                ImageType=def.ImageType
            };
        }

        public static CL_AniDB_Anime_Relation CloneToClient(this AniDB_Anime_Relation ar)
        {
            return new CL_AniDB_Anime_Relation
            {
                AniDB_Anime_RelationID = ar.AniDB_Anime_RelationID,
                AnimeID = ar.AnimeID,
                RelationType = ar.RelationType,
                RelatedAnimeID = ar.RelatedAnimeID
            };
        }

        public static CL_AniDB_Anime_Similar CloneToClient(this AniDB_Anime_Similar s)
        {
            return new CL_AniDB_Anime_Similar
            {
                AniDB_Anime_SimilarID = s.AniDB_Anime_SimilarID,
                AnimeID = s.AnimeID,
                SimilarAnimeID = s.SimilarAnimeID,
                Approval = s.Approval,
                Total = s.Total
            };
        }

        public static CL_AniDB_Character CloneToClient(this AniDB_Character c)
        {
            return new CL_AniDB_Character
            {
                AniDB_CharacterID =c.AniDB_CharacterID,
                CharID = c.CharID,
                PicName = c.PicName,
                CreatorListRaw = c.CreatorListRaw,
                CharName = c.CharName,
                CharKanjiName = c.CharKanjiName,
                CharDescription = c.CharDescription
            };
        }

        public static CL_AniDB_GroupStatus CloneToClient(this AniDB_GroupStatus g)
        {
            return new CL_AniDB_GroupStatus
            {
                AniDB_GroupStatusID = g.AniDB_GroupStatusID,
                AnimeID = g.AnimeID,
                GroupID = g.GroupID,
                GroupName = g.GroupName,
                CompletionState = g.CompletionState,
                LastEpisodeNumber = g.LastEpisodeNumber,
                Rating = g.Rating,
                Votes = g.Votes,
                EpisodeRange = g.EpisodeRange
            };
        }

        public static CL_AnimeEpisode_User CloneToClient(this AnimeEpisode_User e)
        {
            return new CL_AnimeEpisode_User
            {
                AnimeEpisode_UserID = e.AnimeEpisode_UserID,
                JMMUserID = e.JMMUserID,
                AnimeEpisodeID = e.AnimeEpisodeID,
                AnimeSeriesID = e.AnimeSeriesID,
                WatchedDate = e.WatchedDate,
                PlayedCount = e.PlayedCount,
                WatchedCount = e.WatchedCount,
                StoppedCount = e.StoppedCount
            };
        }

        public static CL_AnimeGroup_User CloneToClient(this AnimeGroup_User g)
        {
            return new CL_AnimeGroup_User
            {
                AnimeGroup_UserID = g.AnimeGroup_UserID,
                JMMUserID = g.JMMUserID,
                AnimeGroupID = g.AnimeGroupID,
                IsFave = g.IsFave,
                UnwatchedEpisodeCount = g.UnwatchedEpisodeCount,
                WatchedEpisodeCount = g.WatchedEpisodeCount,
                WatchedDate = g.WatchedDate,
                PlayedCount = g.PlayedCount,
                WatchedCount = g.WatchedCount,
                StoppedCount = g.StoppedCount
            };
        }

        public static CL_AnimeSeries_User CloneToClient(this AnimeSeries_User s)
        {
            return new CL_AnimeSeries_User
            {
                AnimeSeries_UserID=s.AnimeSeries_UserID,
                JMMUserID=s.JMMUserID,
                AnimeSeriesID=s.AnimeSeriesID,
                UnwatchedEpisodeCount=s.UnwatchedEpisodeCount,
                WatchedEpisodeCount=s.WatchedEpisodeCount,
                WatchedDate=s.WatchedDate,
                PlayedCount=s.PlayedCount,
                WatchedCount=s.WatchedCount,
                StoppedCount=s.StoppedCount
            };
        }

        public static CL_BookmarkedAnime CloneToClient(this BookmarkedAnime b)
        {
            return new CL_BookmarkedAnime
            {
                BookmarkedAnimeID=b.BookmarkedAnimeID,
                AnimeID=b.AnimeID,
                Priority=b.Priority,
                Notes=b.Notes,
                Downloading=b.Downloading
            };
        }

        public static Azure_CrossRef_AniDB_MAL_Request CloneToRequest(this CrossRef_AniDB_MAL c)
        {
            return new Azure_CrossRef_AniDB_MAL_Request
            {
                CrossRef_AniDB_MALID=c.CrossRef_AniDB_MALID,
                AnimeID=c.AnimeID,
                MALID =c.MALID,
                MALTitle =c.MALTitle,
                StartEpisodeType=c.StartEpisodeType,
                StartEpisodeNumber = c.StartEpisodeNumber,
                CrossRefSource = c.CrossRefSource
            };
        }

        public static Azure_CrossRef_AniDB_Other_Request CloneToRequest(this CrossRef_AniDB_Other c)
        {
            return new Azure_CrossRef_AniDB_Other_Request
            {
                CrossRef_AniDB_OtherID=c.CrossRef_AniDB_OtherID,
                AnimeID=c.AnimeID,
                CrossRefID =c.CrossRefID,
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
            r.AuthGUID = String.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

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
            r.AuthGUID = String.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }

        public static Media ToMedia(this Azure_Media m)
        {
            int size = m.MediaInfo[0] << 24 | m.MediaInfo[1] << 16 | m.MediaInfo[2] << 8 | m.MediaInfo[3];
            byte[] data = new byte[m.MediaInfo.Length - 4];
            Array.Copy(m.MediaInfo, 4, data, 0, data.Length);
            return CompressionHelper.DeserializeObject<Media>(data, size);

        }

        public static Azure_Media_Request ToMediaRequest(this SVR_VideoLocal v)
        {
            Azure_Media_Request r=new Azure_Media_Request();
            r.ED2K = v.ED2KHash;
            //Cleanup any File subtitles from media information.
            Media m = v.Media.DeepClone();
            if (m.Parts != null && m.Parts.Count > 0)
            {
                foreach (Part p in m.Parts)
                {
                    if (p.Streams != null)
                    {
                        List<Stream> streams = p.Streams.Where(a => a.StreamType == "3" && !String.IsNullOrEmpty(a.File)).ToList();
                        if (streams.Count > 0)
                            streams.ForEach(a => p.Streams.Remove(a));
                    }
                }
            }
            //Cleanup the VideoLocal id
            m.Id = null;
            int outsize;
            byte[] data = CompressionHelper.SerializeObject(m, out outsize);
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
            r.AuthGUID = String.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }
        public static Azure_Media_Request ToMediaRequest(this Media m, string ed2k)
        {
            Azure_Media_Request r = new Azure_Media_Request();
            int outsize;
            byte[] data = CompressionHelper.SerializeObject(m, out outsize);
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
            r.AuthGUID = String.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
            return r;
        }
        public static Azure_CrossRef_AniDB_Trakt_Request ToRequest(this CrossRef_AniDB_TraktV2 xref, string animeName)
        {
            Azure_CrossRef_AniDB_Trakt_Request r=new Azure_CrossRef_AniDB_Trakt_Request();
            r.AnimeID = xref.AnimeID;
            r.AnimeName = animeName;
            r.AniDBStartEpisodeType = xref.AniDBStartEpisodeType;
            r.AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber;
            r.TraktID = xref.TraktID;
            r.TraktSeasonNumber = xref.TraktSeasonNumber;
            r.TraktStartEpisodeNumber = xref.TraktStartEpisodeNumber;
            r.TraktTitle = xref.TraktTitle;
            r.CrossRefSource = xref.CrossRefSource;

            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;

            r.AuthGUID = String.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
            return r;
        }


        public static Azure_CrossRef_AniDB_TvDB_Request ToRequest(this SVR_CrossRef_AniDB_TvDBV2 xref, string animeName)
        {
            Azure_CrossRef_AniDB_TvDB_Request r =new Azure_CrossRef_AniDB_TvDB_Request();
            r.AnimeID = xref.AnimeID;
            r.AnimeName = animeName;
            r.AniDBStartEpisodeType = xref.AniDBStartEpisodeType;
            r.AniDBStartEpisodeNumber = xref.AniDBStartEpisodeNumber;
            r.TvDBID = xref.TvDBID;
            r.TvDBSeasonNumber = xref.TvDBSeasonNumber;
            r.TvDBStartEpisodeNumber = xref.TvDBStartEpisodeNumber;
            r.TvDBTitle = xref.TvDBTitle;
            r.CrossRefSource = xref.CrossRefSource;
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = String.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
            return r;
        }

        public static Azure_CrossRef_File_Episode_Request ToRequest(this SVR_CrossRef_File_Episode xref)
        {
            Azure_CrossRef_File_Episode_Request r=new Azure_CrossRef_File_Episode_Request();
            r.Hash = xref.Hash;
            r.AnimeID = xref.AnimeID;
            r.EpisodeID = xref.EpisodeID;
            r.Percentage = xref.Percentage;
            r.EpisodeOrder = xref.EpisodeOrder;

            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            return r;
        }

        public static CL_DuplicateFile CloneToClient(this DuplicateFile d)
        {
            return new CL_DuplicateFile
            {
                DuplicateFileID=d.DuplicateFileID,
                FilePathFile1=d.FilePathFile1,
                FilePathFile2=d.FilePathFile2,
                Hash =d.Hash,
                ImportFolderIDFile1=d.ImportFolderIDFile1,
                ImportFolderIDFile2=d.ImportFolderIDFile2,
                DateTimeUpdated=d.DateTimeUpdated
            };
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

        public static CL_IgnoreAnime ToClient(this IgnoreAnime i)
        {
            CL_IgnoreAnime c = new CL_IgnoreAnime
            {
                IgnoreAnimeID = i.IgnoreAnimeID,
                JMMUserID = i.JMMUserID,
                AnimeID = i.AnimeID,
                IgnoreType = i.IgnoreType
            };
            c.Anime = RepoFactory.AniDB_Anime.GetByAnimeID(i.AnimeID).CloneToClient();
            return c;
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

        //TODO MOVE Commons with all the ImagePath Resolves
        public static string GetFullImagePath(this MovieDB_Fanart fanart)
        {
            if (String.IsNullOrEmpty(fanart.URL)) return "";

            //strip out the base URL
            int pos = fanart.URL.IndexOf('/', 0);
            string fname = fanart.URL.Substring(pos + 1, fanart.URL.Length - pos - 1);
            fname = fname.Replace("/", @"\");
            return Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
        }
        public static string GetFullImagePath(this MovieDB_Poster movie)
        {
            if (String.IsNullOrEmpty(movie.URL)) return "";

            //strip out the base URL
            int pos = movie.URL.IndexOf('/', 0);
            string fname = movie.URL.Substring(pos + 1, movie.URL.Length - pos - 1);
            fname = fname.Replace("/", @"\");
            return System.IO.Path.Combine(ImageUtils.GetMovieDBImagePath(), fname);
        }

        public static string GetFullImagePath(this Trakt_Episode episode)
        {
            // typical EpisodeImage url
            // http://vicmackey.trakt.tv/images/episodes/3228-1-1.jpg

            // get the TraktID from the URL
            // http://trakt.tv/show/11eyes/season/1/episode/1 (11 eyes)

            if (String.IsNullOrEmpty(episode.EpisodeImage)) return "";
            if (String.IsNullOrEmpty(episode.URL)) return "";

            // on Trakt, if the episode doesn't have a proper screenshot, they will return the
            // fanart instead, we will ignore this
            int pos = episode.EpisodeImage.IndexOf(@"episodes/");
            if (pos <= 0) return "";

            int posID = episode.URL.IndexOf(@"show/");
            if (posID <= 0) return "";

            int posIDNext = episode.URL.IndexOf(@"/", posID + 6);
            if (posIDNext <= 0) return "";

            string traktID = episode.URL.Substring(posID + 5, posIDNext - posID - 5);
            traktID = traktID.Replace("/", @"\");

            string imageName = episode.EpisodeImage.Substring(pos + 9, episode.EpisodeImage.Length - pos - 9);
            imageName = imageName.Replace("/", @"\");

            string relativePath = System.IO.Path.Combine("episodes", traktID);
            relativePath = System.IO.Path.Combine(relativePath, imageName);

            return System.IO.Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
        }

        public static string GetFullImagePath(this Trakt_Friend friend)
        {
            // typical url
            // http://vicmackey.trakt.tv/images/avatars/837.jpg
            // http://gravatar.com/avatar/f894a4cbd5e8bcbb1a79010699af1183.jpg?s=140&r=pg&d=http%3A%2F%2Fvicmackey.trakt.tv%2Fimages%2Favatar-large.jpg

            if (String.IsNullOrEmpty(friend.Avatar)) return "";

            string path = ImageUtils.GetTraktImagePath_Avatars();
            return System.IO.Path.Combine(path, String.Format("{0}.jpg", friend.Username));
        }

        public static string GetFullImagePath(this Trakt_ImageFanart image)
        {
            // typical url
            // http://vicmackey.trakt.tv/images/fanart/3228.jpg

            if (String.IsNullOrEmpty(image.ImageURL)) return "";

            int pos = image.ImageURL.IndexOf(@"images/");
            if (pos <= 0) return "";

            string relativePath = image.ImageURL.Substring(pos + 7, image.ImageURL.Length - pos - 7);
            relativePath = relativePath.Replace("/", @"\");

            return System.IO.Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
        }

        public static string GetFullImagePath(this Trakt_ImagePoster poster)
        {
            // typical url
            // http://vicmackey.trakt.tv/images/seasons/3228-1.jpg
            // http://vicmackey.trakt.tv/images/posters/1130.jpg

            if (String.IsNullOrEmpty(poster.ImageURL)) return "";

            int pos = poster.ImageURL.IndexOf(@"images/");
            if (pos <= 0) return "";

            string relativePath = poster.ImageURL.Substring(pos + 7, poster.ImageURL.Length - pos - 7);
            relativePath = relativePath.Replace("/", @"\");

            return System.IO.Path.Combine(ImageUtils.GetTraktImagePath(), relativePath);
        }

        public static string GetFullImagePath(this TvDB_Episode episode)
        {
            if (String.IsNullOrEmpty(episode.Filename)) return "";

            string fname = episode.Filename;
            fname = episode.Filename.Replace("/", @"\");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImageFanart fanart)
        {
            if (String.IsNullOrEmpty(fanart.BannerPath)) return "";

            string fname = fanart.BannerPath;
            fname = fanart.BannerPath.Replace("/", @"\");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullThumbnailPath(this TvDB_ImageFanart fanart)
        {
            string fname = fanart.ThumbnailPath;
            fname = fanart.ThumbnailPath.Replace("/", @"\");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static void Valid(this TvDB_ImageFanart fanart)
        {
            if (!File.Exists(fanart.GetFullImagePath()) || !File.Exists(fanart.GetFullThumbnailPath()))
            {
                //clean leftovers
                if (File.Exists(fanart.GetFullImagePath()))
                {
                    File.Delete(fanart.GetFullImagePath());
                }
                if (File.Exists(fanart.GetFullThumbnailPath()))
                {
                    File.Delete(fanart.GetFullThumbnailPath());
                }
            }
        }

        public static string GetFullImagePath(this TvDB_ImagePoster poster)
        {
            if (String.IsNullOrEmpty(poster.BannerPath)) return "";

            string fname = poster.BannerPath;
            fname = poster.BannerPath.Replace("/", @"\");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static string GetFullImagePath(this TvDB_ImageWideBanner banner)
        {
            if (String.IsNullOrEmpty(banner.BannerPath)) return "";

            string fname = banner.BannerPath;
            fname = banner.BannerPath.Replace("/", @"\");
            return System.IO.Path.Combine(ImageUtils.GetTvDBImagePath(), fname);
        }

        public static void Populate(this Trakt_Friend friend, TraktV2User user)
        {
            friend.Username = user.username;
            friend.FullName = user.name;
            friend.LastAvatarUpdate = DateTime.Now;
        }

        public static List<Trakt_Episode> GetEpisodes(this Trakt_Season season) => RepoFactory.Trakt_Episode.GetByShowIDAndSeason(season.Trakt_ShowID, season.Season);

        public static List<Trakt_Season> GetSeasons(this Trakt_Show show)
        {
            return RepoFactory.Trakt_Season.GetByShowID(show.Trakt_ShowID);
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

        public static void Populate(this TvDB_Episode episode, XmlDocument doc)
        {
            // used when getting information from episode info
            // http://thetvdb.com/api/B178B8940CAF4A2C/episodes/306542/en.xml

            episode.Id = Int32.Parse(TryGetProperty(doc, "id"));
            episode.SeriesID = Int32.Parse(TryGetProperty(doc, "seriesid"));
            episode.SeasonID = Int32.Parse(TryGetProperty(doc, "seasonid"));
            episode.SeasonNumber = Int32.Parse(TryGetProperty(doc, "SeasonNumber"));
            episode.EpisodeNumber = Int32.Parse(TryGetProperty(doc, "EpisodeNumber"));

            int flag = 0;
            if (Int32.TryParse(TryGetProperty(doc, "EpImgFlag"), out flag))
                episode.EpImgFlag = flag;

            int abnum = 0;
            if (Int32.TryParse(TryGetProperty(doc, "absolute_number"), out abnum))
                episode.AbsoluteNumber = abnum;

            episode.EpisodeName = TryGetProperty(doc, "EpisodeName");
            episode.Overview = TryGetProperty(doc, "Overview");
            episode.Filename = TryGetProperty(doc, "filename");
            //this.FirstAired = TryGetProperty(doc, "FirstAired");

            int aas = 0;
            if (Int32.TryParse(TryGetProperty(doc, "airsafter_season"), out aas))
                episode.AirsAfterSeason = aas;
            else
                episode.AirsAfterSeason = null;

            int abe = 0;
            if (Int32.TryParse(TryGetProperty(doc, "airsbefore_episode"), out abe))
                episode.AirsBeforeEpisode = abe;
            else
                episode.AirsBeforeEpisode = null;

            int abs = 0;
            if (Int32.TryParse(TryGetProperty(doc, "airsbefore_season"), out abs))
                episode.AirsBeforeSeason = abs;
            else
                episode.AirsBeforeSeason = null;
        }

        public static void Populate(this TvDB_Episode episode, XmlNode node)
        {
            // used when getting information from full series info
            // http://thetvdb.com/api/B178B8940CAF4A2C/series/84187/all/en.xml

            episode.Id = Int32.Parse(TryGetProperty(node, "id"));
            episode.SeriesID = Int32.Parse(TryGetProperty(node, "seriesid"));
            episode.SeasonID = Int32.Parse(TryGetProperty(node, "seasonid"));
            episode.SeasonNumber = Int32.Parse(TryGetProperty(node, "SeasonNumber"));
            episode.EpisodeNumber = Int32.Parse(TryGetProperty(node, "EpisodeNumber"));

            int flag = 0;
            if (Int32.TryParse(TryGetProperty(node, "EpImgFlag"), out flag))
                episode.EpImgFlag = flag;

            int abnum = 0;
            if (Int32.TryParse(TryGetProperty(node, "absolute_number"), out abnum))
                episode.AbsoluteNumber = abnum;

            episode.EpisodeName = TryGetProperty(node, "EpisodeName");
            episode.Overview = TryGetProperty(node, "Overview");
            episode.Filename = TryGetProperty(node, "filename");
            //this.FirstAired = TryGetProperty(node, "FirstAired");

            int aas = 0;
            if (Int32.TryParse(TryGetProperty(node, "airsafter_season"), out aas))
                episode.AirsAfterSeason = aas;
            else
                episode.AirsAfterSeason = null;

            int abe = 0;
            if (Int32.TryParse(TryGetProperty(node, "airsbefore_episode"), out abe))
                episode.AirsBeforeEpisode = abe;
            else
                episode.AirsBeforeEpisode = null;

            int abs = 0;
            if (Int32.TryParse(TryGetProperty(node, "airsbefore_season"), out abs))
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
            catch (Exception ex)
            {
                //logger.Error( ex,"Error in TvDB_Episode.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        private static string TryGetProperty(XmlDocument doc, string propertyName)
        {
            try
            {
                string prop = doc["Data"]["Episode"][propertyName].InnerText.Trim();
                return prop;
            }
            catch (Exception ex)
            {
                //logger.Error( ex,"Error in TvDB_Episode.TryGetProperty: " + ex.ToString());
            }

            return "";
        }

        public static bool Populate(this TvDB_ImageFanart fanart, int seriesID, XmlNode node)
        {
            try
            {
                fanart.SeriesID = seriesID;
                fanart.Id = Int32.Parse(node["id"].InnerText);
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
                logger.Error( ex,"Error in TvDB_ImageFanart.Init: " + ex.ToString());
                return false;
            }
        }

        public static bool Populate(this TvDB_ImagePoster poster, int seriesID, XmlNode node, TvDBImageNodeType nodeType)
        {
            try
            {
                poster.SeriesID = seriesID;

                if (nodeType == TvDBImageNodeType.Series)
                    poster.SeasonNumber = null;
                else
                    poster.SeasonNumber = Int32.Parse(node["Season"].InnerText);


                poster.Id = Int32.Parse(node["id"].InnerText);
                poster.BannerPath = node["BannerPath"].InnerText;
                poster.BannerType = node["BannerType"].InnerText;
                poster.BannerType2 = node["BannerType2"].InnerText;
                poster.Language = node["Language"].InnerText;


                return true;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TvDB_ImagePoster.Populate: " + ex.ToString());
                return false;
            }
        }

        public static bool Populate(this TvDB_ImageWideBanner banner, int seriesID, XmlNode node, TvDBImageNodeType nodeType)
        {
            try
            {
                banner.SeriesID = seriesID;

                if (nodeType == TvDBImageNodeType.Series)
                    banner.SeasonNumber = null;
                else
                    banner.SeasonNumber = Int32.Parse(node["Season"].InnerText);

                banner.Id = Int32.Parse(node["id"].InnerText);
                banner.BannerPath = node["BannerPath"].InnerText;
                banner.BannerType = node["BannerType"].InnerText;
                banner.BannerType2 = node["BannerType2"].InnerText;
                banner.Language = node["Language"].InnerText;

                return true;
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Error in TvDB_ImageWideBanner.Populate: " + ex.ToString());
                return false;
            }
        }

        public static CL_Trakt_Season ToClient(this Trakt_Season season)
        {
            return new CL_Trakt_Season
            {
                Trakt_SeasonID = season.Trakt_SeasonID,
                Trakt_ShowID = season.Trakt_ShowID,
                Season = season.Season,
                URL = season.URL,
                Episodes = season.GetEpisodes()
            };

        }

        public static CL_Trakt_Show ToClient(this Trakt_Show show)
        {
            return new CL_Trakt_Show
            {
                Trakt_ShowID = show.Trakt_ShowID,
                TraktID = show.TraktID,
                Title = show.Title,
                Year = show.Year,
                URL = show.URL,
                Overview = show.Overview,
                TvDB_ID = show.TvDB_ID,
                Seasons = show.GetSeasons().Select(a=>a.ToClient()).ToList()
            };

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
            series.SeriesID = Int32.Parse(TryGetProperty(doc, "seriesid"));
            series.SeriesName = TryGetProperty(doc, "SeriesName");
            series.Overview = TryGetProperty(doc, "Overview");
            series.Banner = TryGetProperty(doc, "banner");
        }

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
            series.SeriesID = Int32.Parse(TryGetProperty(doc, "id"));
            series.SeriesName = TryGetProperty(doc, "SeriesName");
            series.Overview = TryGetProperty(doc, "Overview");
            series.Banner = TryGetProperty(doc, "banner");

            series.Status = TryGetProperty(doc, "Status");
            series.Fanart = TryGetProperty(doc, "fanart");
            series.Lastupdated = TryGetProperty(doc, "lastupdated");
            series.Poster = TryGetProperty(doc, "poster");
        }


        public static void Populate(this TVDB_Series_Search_Response response, XmlNode series)
        {
            response.Id = String.Empty;
            response.SeriesID = 0;
            response.Overview = String.Empty;
            response.SeriesName = String.Empty;
            response.Banner = String.Empty;
            if (series["seriesid"] != null) response.SeriesID = Int32.Parse(series["seriesid"].InnerText);
            if (series["SeriesName"] != null) response.SeriesName = series["SeriesName"].InnerText;
            if (series["id"] != null) response.Id = series["id"].InnerText;
            if (series["Overview"] != null) response.Overview = series["Overview"].InnerText;
            if (series["banner"] != null) response.Banner = series["banner"].InnerText;
            if (series["language"] != null) response.Language = series["language"].InnerText;
        }
    }
}
