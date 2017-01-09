using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Force.DeepCloner;
using Shoko.Models.Azure;
using Shoko.Models.Client;
using Shoko.Models.Server;
using Shoko.Server.Entities;
using Shoko.Server.LZ4;
using CL_AniDB_Anime_DefaultImage = Shoko.Models.Client.CL_AniDB_Anime_DefaultImage;
using CL_AniDB_Anime_Relation = Shoko.Models.Client.CL_AniDB_Anime_Relation;
using CL_AniDB_Anime_Similar = Shoko.Models.Client.CL_AniDB_Anime_Similar;
using CL_AniDB_Character = Shoko.Models.Client.CL_AniDB_Character;
using CL_AniDB_GroupStatus = Shoko.Models.Client.CL_AniDB_GroupStatus;
using CL_AnimeEpisode_User = Shoko.Models.Client.CL_AnimeEpisode_User;
using CL_AnimeGroup_User = Shoko.Models.Client.CL_AnimeGroup_User;
using CL_AnimeSeries_User = Shoko.Models.Client.CL_AnimeSeries_User;
using CL_BookmarkedAnime = Shoko.Models.Client.CL_BookmarkedAnime;

namespace Shoko.Server
{
    public static class ClientCloneExtensions
    {
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
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }

        public static Azure_FileHash_Request ToHashRequest(this VideoLocal vl)
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

        public static Shoko.Models.PlexAndKodi.Media ToMedia(this Azure_Media m)
        {
            int size = m.MediaInfo[0] << 24 | m.MediaInfo[1] << 16 | m.MediaInfo[2] << 8 | m.MediaInfo[3];
            byte[] data = new byte[m.MediaInfo.Length - 4];
            Array.Copy(m.MediaInfo, 4, data, 0, data.Length);
            return CompressionHelper.DeserializeObject<Shoko.Models.PlexAndKodi.Media>(data, size);

        }

        public static Azure_Media_Request ToMediaRequest(this VideoLocal v)
        {
            Azure_Media_Request r=new Azure_Media_Request();
            r.ED2K = v.ED2KHash;
            //Cleanup any File subtitles from media information.
            Shoko.Models.PlexAndKodi.Media m = v.Media.DeepClone();
            if (m.Parts != null && m.Parts.Count > 0)
            {
                foreach (Shoko.Models.PlexAndKodi.Part p in m.Parts)
                {
                    if (p.Streams != null)
                    {
                        List<Shoko.Models.PlexAndKodi.Stream> streams = p.Streams.Where(a => a.StreamType == "3" && !string.IsNullOrEmpty(a.File)).ToList();
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
            r.Version = VideoLocal.MEDIA_VERSION;
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;

            return r;
        }
        public static Azure_Media_Request ToMediaRequest(this Shoko.Models.PlexAndKodi.Media m, string ed2k)
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
            r.Version = VideoLocal.MEDIA_VERSION;
            r.Username = ServerSettings.AniDB_Username;
            if (ServerSettings.WebCache_Anonymous)
                r.Username = Constants.AnonWebCacheUsername;
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
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

            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
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
            r.AuthGUID = string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey) ? "" : ServerSettings.WebCacheAuthKey;
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
    }
}
