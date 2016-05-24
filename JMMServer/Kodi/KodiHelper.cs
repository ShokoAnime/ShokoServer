using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Serialization;
using JMMContracts;
using JMMContracts.KodiContracts;
using JMMFileHelper;
using JMMFileHelper.Subtitles;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using NHibernate;
using Directory = JMMContracts.KodiContracts.Directory;
using System.Text.RegularExpressions;

// ReSharper disable FunctionComplexityOverflow
namespace JMMServer.Kodi
{
    public static class KodiHelper
    {
    
        public const string MediaTagVersion = "1420942002";


        public static MediaContainer NewMediaContainer(string title, bool allowsync, bool nocache = true)
        {
            MediaContainer m = new MediaContainer();
            m.Title1 = m.Title2 = title;
            m.AllowSync = allowsync ? "1" : "0";
            m.NoCache = nocache ? "1" : "0";
            m.ViewMode = "65592";
            m.ViewGroup = "show";
            m.MediaTagVersion = MediaTagVersion;
            m.Identifier = "plugin.video.nakamori";
            return m;
        }

        public static string Base64EncodeUrl(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes).Replace("+", "-").Replace("/", "_").Replace("=", ",");
        }
        /* public static string ToHex(string ka)
        {
            byte[] ba = Encoding.UTF8.GetBytes(ka);
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        } */
        /* public static string FromHex(string hex)
        {
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return Encoding.UTF8.GetString(raw);
        } */
        /* public static string PlexProxy(string url)
        {
            // return "/video/jmm/proxy/" + ToHex(url);
            return url;
        } */
        public static System.IO.Stream GetStreamFromXmlObject<T>(T obj)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            Utf8StringWriter textWriter = new Utf8StringWriter();
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Nakamori-Protocol", "1.0");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                WebOperationContext.Current.OutgoingResponse.ContentType = "application/xml";
            }
            xmlSerializer.Serialize(textWriter, obj, ns);
            return new MemoryStream(Encoding.UTF8.GetBytes(textWriter.ToString()));

        }

        private static T XmlDeserializeFromString<T>(string objectData)
        {
            return (T)XmlDeserializeFromString(objectData, typeof(T));
        }

        private static object XmlDeserializeFromString(string objectData, Type type)
        {
            var serializer = new XmlSerializer(type);
            object result;

            using (TextReader reader = new StringReader(objectData))
            {
                result = serializer.Deserialize(reader);
            }

            return result;
        }

        public static JMMUser GetUser(string UserId)
        {
            int userId = -1;
            if (!string.IsNullOrEmpty(UserId))
                int.TryParse(UserId, out userId);
            JMMUserRepository repUsers = new JMMUserRepository();
            return userId != 0
                ? repUsers.GetByID(userId)
                : repUsers.GetAll().FirstOrDefault(a => a.Username == "Default");
        }

        public static string ServerUrl(int port, string path, bool externalip=false)
        {
            if ((WebOperationContext.Current == null) ||
                (WebOperationContext.Current.IncomingRequest.UriTemplateMatch == null))
            {
                return "{SCHEME}://{HOST}:" + port + "/" + path;
            }
            string host = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Host;
            if (externalip)
            {
                IPAddress ip = FileServer.FileServer.GetExternalAddress();
                if (ip != null)
                    host = ip.ToString();
            }
            return WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme + "://" +
                   host + ":" + port + "/" +
                   path;
        }

        public static string ReplaceSchemeHost(string str, bool externalip=false)
        {
            if (str == null)
                return null;
            if (WebOperationContext.Current == null)
                return null;
            string host = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Host;
            if (externalip)
            {
                IPAddress ip = FileServer.FileServer.GetExternalAddress();
                if (ip != null)
                    host = ip.ToString();
            }

           /* if (str.StartsWith("/video/jmm/proxy/"))
            {
                string k = str.Substring(17);
                k = FromHex(k);
                k = k.Replace("{SCHEME}", WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme).Replace("{HOST}", host);
                //return "/video/jmm/proxy/" + ToHex(k);
                return "/video/jmm/proxy/" +k;
            } */
            return str.Replace("{SCHEME}", WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme).Replace("{HOST}", host);
        }

        private static void PopulateVideoEpisodeFromVideoLocal(Video l, VideoLocal v, JMMType type)
        {
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available";
            l.Title = Path.GetFileNameWithoutExtension(v.FilePath);
            l.Key = l.PrimaryExtraKey = "" + (int)type + "/" + v.VideoLocalID;
            l.AddedAt = v.DateTimeCreated.Year.ToString("0000") + "-" + v.DateTimeCreated.Month.ToString("00") + "-" + v.DateTimeCreated.Day.ToString("00") + " " + v.DateTimeCreated.Hour.ToString("00") + ":" + v.DateTimeCreated.Minute.ToString("00") + ":" + v.DateTimeCreated.Millisecond.ToString("00");
            l.UpdatedAt = v.DateTimeUpdated.Year.ToString("0000") + "-" + v.DateTimeUpdated.Month.ToString("00") + "-" + v.DateTimeUpdated.Day.ToString("00") + " " + v.DateTimeUpdated.Hour.ToString("00") + ":" + v.DateTimeUpdated.Minute.ToString("00") + ":" + v.DateTimeUpdated.Millisecond.ToString("00");
            l.OriginallyAvailableAt = v.DateTimeCreated.Year.ToString("0000") + "-" + v.DateTimeCreated.Month.ToString("00") + "-" + v.DateTimeCreated.Day.ToString("00");
            l.Year = v.DateTimeCreated.Year.ToString();
     
            VideoInfo info = v.VideoInfo;
            
            Media m = null;
            if (info != null)
            {
                if (!string.IsNullOrEmpty(info.FullInfo))
                {
                    try
                    {
                        m = XmlDeserializeFromString<Media>(info.FullInfo);
                    }
                    catch (Exception)
                    {
                        info.FullInfo = null;
                    }
                }
                if (string.IsNullOrEmpty(info.FullInfo))
                {
                    VideoInfoRepository repo = new VideoInfoRepository();
                    MediaInfoResult mInfo = FileHashHelper.GetMediaInfo(v.FullServerPath, true, true);
                    info.AudioBitrate = string.IsNullOrEmpty(mInfo.AudioBitrate) ? "" : mInfo.AudioBitrate;
                    info.AudioCodec = string.IsNullOrEmpty(mInfo.AudioCodec) ? "" : mInfo.AudioCodec;
                    info.Duration = mInfo.Duration;
                    info.VideoBitrate = string.IsNullOrEmpty(mInfo.VideoBitrate) ? "" : mInfo.VideoBitrate;
                    info.VideoBitDepth = string.IsNullOrEmpty(mInfo.VideoBitDepth) ? "" : mInfo.VideoBitDepth;
                    info.VideoCodec = string.IsNullOrEmpty(mInfo.VideoCodec) ? "" : mInfo.VideoCodec;
                    info.VideoFrameRate = string.IsNullOrEmpty(mInfo.VideoFrameRate) ? "" : mInfo.VideoFrameRate;
                    info.VideoResolution = string.IsNullOrEmpty(mInfo.VideoResolution) ? "" : mInfo.VideoResolution;
                    info.FullInfo = string.IsNullOrEmpty(mInfo.FullInfo) ? "" : mInfo.FullInfo;
                    repo.Save(info);
                    m = XmlDeserializeFromString<Media>(info.FullInfo);
                }

            }
            l.Medias = new List<Media>();
            if (m != null)
            {

                m.Id = null;
                List<JMMContracts.KodiContracts.Stream> subs = SubtitleHelper.GetSubtitleStreamsKodi(v.FullServerPath);
                if (subs.Count > 0)
                {
                    foreach (JMMContracts.KodiContracts.Stream s in subs)
                    {
                        s.Key = ServerUrl(int.Parse(ServerSettings.JMMServerFilePort), "file/0/"+Base64EncodeUrl(s.File),KodiObject.IsExternalRequest);
                    }
                    m.Parts[0].Streams.AddRange(subs);
                }
                foreach (Part p in m.Parts)
                {
                    p.Id = null;

                    p.File = v.FullServerPath;
                    string ff = Path.GetExtension(v.FullServerPath);
                    p.Key = "" + v.VideoLocalID + "/file" + ff;
                    p.Accessible = "1";
                    p.Exists = "1";
                    bool vid = false;
                    bool aud = false;
                    bool txt = false;
                    foreach (JMMContracts.KodiContracts.Stream ss in p.Streams.ToArray())
                    {
                        if ((ss.StreamType == "1") && (!vid))
                        {
                            vid = true;
                        }
                        if ((ss.StreamType == "2") && (!aud))
                        {
                            aud = true;
                            ss.Selected = "1";
                        }
                        if ((ss.StreamType == "3") && (!txt))
                        {
                            txt = true;
                            ss.Selected = "1";
                        }
                    }
                }

                l.Medias.Add(m);
                l.Duration = m.Duration;
            }
        }

        private static void PopulateVideoEpisodeFromAnimeEpisode(Video v, AnimeEpisode ep, int userid)
        {
            AniDB_Episode aep = ep.AniDB_Episode;
            if (aep != null)
            {
                v.JMMEpisodeId = ep.AnimeEpisodeID;
                v.EpNumber = aep.EpisodeNumber;
                v.Index = aep.EpisodeNumber.ToString();
                v.Title = aep.EnglishName;
                v.OriginalTitle = aep.RomajiName;
                v.Rating = (Convert.ToDouble(aep.Rating)).ToString(CultureInfo.InvariantCulture);
                v.Votes = aep.Votes;
                if (aep.AirDateAsDate.HasValue)
                {
                    v.Year = aep.AirDateAsDate.Value.Year.ToString();
                    v.OriginallyAvailableAt = aep.AirDateAsDate.Value.Year.ToString("0000") + "-" +
                                                aep.AirDateAsDate.Value.Month.ToString("00") +
                                                "-" + aep.AirDateAsDate.Value.Day.ToString("00");
                }
                AnimeEpisode_User epuser = ep.GetUserRecord(userid);
                if (epuser != null)
                    v.ViewCount = epuser.WatchedCount.ToString();
                MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
                JMMServiceImplementationMetro.SetTvDBInfo(aep.AnimeID, aep, ref contract);
                if (contract.ImageID != 0)
                    v.Thumb = "" + contract.ImageType + "/" + contract.ImageID;
                else
                    v.Thumb = ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressKodi + "/GetSupportImage/plex_404.png");
                v.Summary = contract.EpisodeOverview;

                //total local files
                try
                {
                    v.totalLocal = ep.GetAnimeSeries().GetAnimeEpisodesCountWithVideoLocal();
                }
                catch { }
                //end total local files

                //community support

                //CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                //List<CrossRef_AniDB_TraktV2> Trakt = repCrossRef.GetByAnimeID(aep.AnimeID);
                //if (Trakt != null)
                //{
                //    if (Trakt.Count > 0)
                //    {
                //        v.Trakt = Trakt[0].TraktID;
                //    }
                //}

                //CrossRef_AniDB_TvDBV2Repository repCrossRefV2 = new CrossRef_AniDB_TvDBV2Repository();
                //List<CrossRef_AniDB_TvDBV2> TvDB = repCrossRefV2.GetByAnimeID(aep.AnimeID);
                //if (TvDB != null)
                //{
                //    if (TvDB.Count > 0)
                //    {
                //        v.TvDB = TvDB[0].TvDBID.ToString();
                //    }
                //}

                //community support END
            }

        }


        private static bool PopulateVideoEpisodeFromAnime(Video v, AniDB_Anime ani, Video nv)
        {
            bool ret = false;

            if (ani != null)
            {
                if (ani.Restricted > 0)
                    v.ContentRating = "R";
                if (ani.AnimeTypeEnum == enAnimeType.Movie)
                {
                    v.Type = "movie";
                    if (v.Title.StartsWith("Complete Movie"))
                    {
                        v.Title = nv.Title;
                        v.Summary = nv.Summary;
                        v.Index = null;
                        ret = true;
                    }
                    else if (v.Title.StartsWith("Part "))
                    {
                        v.Title = nv.Title + " - " + v.Title;
                        v.Summary = nv.Summary;
                    }
                    v.Thumb = nv.Thumb;
                }
                else if (ani.AnimeTypeEnum == enAnimeType.OVA)
                {
                    if (v.Title == "OVA")
                    {
                        v.Title = nv.Title;
                        v.Type = "movie";
                        v.Thumb = nv.Thumb;
                        v.Summary = nv.Summary;
                        v.Index = null;
                        ret = true;
                    }
                }
                else
                    v.ParentTitle = nv.Title;
            }
            if (string.IsNullOrEmpty(v.Art))
                v.Art = nv.Art;
            if (v.Tags == null)
                v.Tags = nv.Tags;
            if (v.Genres == null)
                v.Genres = nv.Genres;
            if (v.Season == null)
                v.Season = nv.Season;
            v.ParentThumb = nv.Thumb;
            v.ParentRatingKey = v.ParentKey = nv.Key;
            if (string.IsNullOrEmpty(v.Rating))
            {
                v.Rating = nv.Rating;
                v.Votes = nv.Votes;
            }
            return ret;
        }
        public static bool PopulateVideo(Video l, VideoLocal v, JMMType type, int userid)
        {

            PopulateVideoEpisodeFromVideoLocal(l, v, type);
            List<AnimeEpisode> eps = v.GetAnimeEpisodes();
            if (eps.Count > 0)
            {
                PopulateVideoEpisodeFromAnimeEpisode(l,eps[0],userid);
                AnimeSeries series = eps[0].GetAnimeSeries();
                if (series != null)
                {
                    Contract_AnimeSeries cseries = series.ToContract(series.GetUserRecord(userid), true);
                    Video nv = FromSerie(cseries, userid);
                    AniDB_Anime ani = series.GetAnime();
                    return PopulateVideoEpisodeFromAnime(l,ani,nv);
                }
                    
            }
            return false;
        }
        public static bool PopulateVideo(Video l, VideoLocal v, AnimeEpisode ep,  AnimeSeries series , AniDB_Anime ani, Video nv, JMMType type, int userid)
        {

            PopulateVideoEpisodeFromVideoLocal(l, v, type);
            if (ep!=null)
            {
                PopulateVideoEpisodeFromAnimeEpisode(l, ep, userid);
                if (series != null)
                {
                    return PopulateVideoEpisodeFromAnime(l,ani, nv);
                }
            }
            return false;
        }
        public static IEnumerable<T> Randomize<T>(this IEnumerable<T> source, int seed)
        {
            Random rnd = new Random(seed);
            return source.OrderBy(item => rnd.Next());
        }

        public static string GetRandomFanartFromSeries(List<AnimeSeries> series, ISession session)
        {
            foreach (AnimeSeries ser in series.Randomize(123456789))
            {
                AniDB_Anime anim = ser.GetAnime(session);
                if (anim != null)
                {
                    ImageDetails fanart = anim.GetDefaultFanartDetailsNoBlanks(session);
                    if (fanart != null)
                        return fanart.GenArt();
                }
            }
            return null;
        }
        public static string GetRandomFanartFromVideoList(List<Video> videos)
        {
            foreach (Video v in videos.Randomize(123456789))
            {
                if (v.Art != null)
                    return v.Art;
            }
            return null;
        }
        public static Video VideoFromAnimeGroup(ISession session, AnimeGroup grp, int userid, List<AnimeSeries> allSeries)
        {
            Contract_AnimeGroup cgrp = grp.ToContract(grp.GetUserRecord(session, userid));
            if (StatsCache.Instance.StatGroupSeriesCount[grp.AnimeGroupID] == 1)
            {
                AnimeSeries ser = JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                if (ser != null)
                {
                    Contract_AnimeSeries cserie = ser.ToContract(ser.GetUserRecord(session, userid), true);
                    Video v = FromSerieWithPossibleReplacement(cserie, ser, userid);
                    v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                    v.Group = cgrp;
                    return v;
                }
            }
            else
            {
                AnimeSeries ser = grp.DefaultAnimeSeriesID.HasValue ? allSeries.FirstOrDefault(a => a.AnimeSeriesID == grp.DefaultAnimeSeriesID.Value) : JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                if (ser != null)
                {
                    Video v = FromGroup(cgrp, ser.ToContract(ser.GetUserRecord(session, userid), true), userid);
                    v.Group = cgrp;
                    v.AirDate = cgrp.Stat_AirDate_Min.HasValue ? cgrp.Stat_AirDate_Min.Value : DateTime.MinValue;
                    return v;
                }
            }
            return null;
        }
        public static Video MayReplaceVideo(Directory v1, AnimeSeries ser, AniDB_Anime anime, JMMType type, int userid, bool all=true)
        {
            int epcount = all ? ser.GetAnimeEpisodesCountWithVideoLocal() :  ser.GetAnimeEpisodesNormalCountWithVideoLocal();
            if (epcount == 1)
            {

                List<AnimeEpisode> episodes = ser.GetAnimeEpisodes();
                List<VideoLocal> l = episodes[0].GetVideoLocals();
                if (l.Count > 0)
                {
                    Video v2 = new Video();
                    try
                    {
                        if (PopulateVideo(v2, l[0], episodes[0], ser, anime, v1, JMMType.File, userid))
                        {
                            return v2;
                        }
                    }
                    catch (Exception e)
                    {
                        //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                    }
                }
            }
            return v1;
        }


        internal static Directory FromGroup(Contract_AnimeGroup grp, Contract_AnimeSeries ser, int userid)
        {
            Directory p = new Directory();
            p.Key = (int)JMMType.Group + "/" + grp.AnimeGroupID.ToString();
            p.Title = grp.GroupName;
            p.Summary = grp.Description;

            p.Summary = Regex.Replace(p.Summary, "http://anidb.net/[a-z]{1,3}[0-9]{1,7}[ ]", "");
            p.Summary = Regex.Replace(p.Summary, "(\\[|\\])", "");

            p.Type = "show";
            p.AirDate = grp.Stat_AirDate_Min.HasValue ? grp.Stat_AirDate_Min.Value : DateTime.MinValue;
            Contract_AniDBAnime anime = ser.AniDBAnime;
            if (anime != null)
            {

                Contract_AniDB_Anime_DefaultImage poster = anime.DefaultImagePoster;
                Contract_AniDB_Anime_DefaultImage fanart = anime.DefaultImageFanart;
                p.Thumb = poster != null ? poster.GenPoster() : ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressKodi + "/GetSupportImage/plex_404V.png");
                if (fanart != null)
                    p.Art = fanart.GenArt();
                if (!string.IsNullOrEmpty(anime.AllTags))
                {
                    p.Tags = new List<Tag> { new Tag { Value = anime.AllTags.Replace("|", ",") } };
                }
                p.Rating = (anime.Rating / 100F).ToString(CultureInfo.InvariantCulture);
                p.Year = "" + anime.BeginYear;
            }
            p.LeafCount = (grp.UnwatchedEpisodeCount + grp.WatchedEpisodeCount).ToString();
            p.ViewedLeafCount = grp.WatchedEpisodeCount.ToString();
            return p;
        }

        public static Video FromSerieWithPossibleReplacement(Contract_AnimeSeries cserie, AnimeSeries ser, int userid)
        {
            Video v = KodiHelper.FromSerie(cserie, userid);
            //if (ser.GetAnimeNumberOfEpisodeTypes() > 1)
                v.Type = "show";
            //else if ((cserie.AniDBAnime.AnimeType == (int)enAnimeType.Movie) || (cserie.AniDBAnime.AnimeType == (int)enAnimeType.OVA))
            //{
            //    v = MayReplaceVideo((Directory)v, ser, ser.GetAnime(), JMMType.File, userid);
            //}
            return v;
        }

        public static Directory FromSerie(Contract_AnimeSeries ser, int userid)
        {
            Directory p = new Directory();

            Contract_AniDBAnime anime = ser.AniDBAnime;

            p.Key = "" + (int)JMMType.Serie + "/" + ser.AnimeSeriesID;

            if (ser.AniDBAnime.Restricted>0)
                p.ContentRating = "R";
            p.Title = anime.MainTitle;
            p.Summary = anime.Description;
            if (string.IsNullOrEmpty(p.Summary) && ser.MovieDB_Movie!=null)
            {
                p.Summary = ser.MovieDB_Movie.Overview;
            }
            if (string.IsNullOrEmpty(p.Summary) && ser.TvDB_Series != null && ser.TvDB_Series.Count > 0)
            {
                p.Summary = ser.TvDB_Series[0].Overview;
            }

            p.Summary = Regex.Replace(p.Summary, "http://anidb.net/[a-z]{1,3}[0-9]{1,7}[ ]", "");
            p.Summary = Regex.Replace(p.Summary, "(\\[|\\])", "");

            p.Type = "season";
            p.AirDate = DateTime.MinValue;

            if (!string.IsNullOrEmpty(anime.AllCategories))
            {
                p.Genres = new List<Tag> { new Tag { Value = anime.AllCategories.Replace("|", ",") } };
            }
            if (!string.IsNullOrEmpty(anime.AllTags))
            {
                p.Tags = new List<Tag> { new Tag { Value = anime.AllTags.Replace("|", ",") } };
            }
            p.OriginalTitle = anime.AllTitles;
            if (anime.AirDate.HasValue)
            {
                p.AirDate = anime.AirDate.Value;
                p.OriginallyAvailableAt = anime.AirDate.Value.Year.ToString("0000") + "-" + anime.AirDate.Value.Month.ToString("00") + "-" +
                                            anime.AirDate.Value.Day.ToString("00");
                p.Year = anime.AirDate.Value.Year.ToString();
            }
            p.LeafCount = anime.EpisodeCount.ToString();
            p.ViewedLeafCount = ser.WatchedEpisodeCount.ToString();
            p.Rating = (anime.Rating / 100F).ToString(CultureInfo.InvariantCulture);
            p.Votes = anime.VoteCount.ToString();
            List<Contract_CrossRef_AniDB_TvDBV2> ls = ser.CrossRefAniDBTvDBV2;
            if (ls.Count > 0)
            {
                foreach (Contract_CrossRef_AniDB_TvDBV2 c in ls)
                {
                    if (c.TvDBSeasonNumber != 0)
                    {
                        p.Season = c.TvDBSeasonNumber.ToString();
                    }
                }
            }
            Contract_AniDB_Anime_DefaultImage poster = anime.DefaultImagePoster;
            Contract_AniDB_Anime_DefaultImage fanart = anime.DefaultImageFanart;

            p.Thumb = poster != null ? poster.GenPoster() : ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressKodi + "/GetSupportImage/plex_404V.png");
            if (fanart != null)
                p.Art = fanart.GenArt();


            /*
                List<AniDB_Anime_Character> chars = anime.GetAnimeCharacters(session);
            
                List<string> sey=new List<string>();

                if (chars != null)
                {
                    foreach (AniDB_Anime_Character c in chars)
                    {
                        AniDB_Character cha = c.GetCharacter(session);
                        if (cha != null)
                        {
                            AniDB_Seiyuu seiyuu = cha.GetSeiyuu(session);
                            if (seiyuu!=null)
                                sey.Add(seiyuu.SeiyuuName);
                        }
                    }
                }
                if (sey.Count > 0)
                    p.Roles = sey.Select(a => new Tag() {Value = a}).ToList();
                */

            //community support

            //CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
            //List<CrossRef_AniDB_TraktV2> Trakt = repCrossRef.GetByAnimeID(anime.AnimeID);
            //if (Trakt != null)
            //{
            //    if (Trakt.Count > 0)
            //    {
            //        p.Trakt = Trakt[0].TraktID;
            //    }
            //}

            //CrossRef_AniDB_TvDBV2Repository repCrossRefV2 = new CrossRef_AniDB_TvDBV2Repository();
            //List<CrossRef_AniDB_TvDBV2> TvDB = repCrossRefV2.GetByAnimeID(anime.AnimeID);
            //if (TvDB != null)
            //{
            //    if (TvDB.Count > 0)
            //    {
            //        p.TvDB = TvDB[0].TvDBID.ToString();
            //    }
            //}

            //community support END


            return p;
        }




    }

}
