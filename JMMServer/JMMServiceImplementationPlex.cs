using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Web;
using System.Windows.Media.Animation;
using System.Xml.Serialization;
using AniDBAPI;
using Antlr.Runtime.Misc;
using BinaryNorthwest;
using FluentNHibernate.Mapping;
using JMMContracts;
using JMMContracts.PlexContracts;
using JMMFileHelper;
using JMMFileHelper.Subtitles;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Properties;
using JMMServer.Repositories;
using Microsoft.SqlServer.Management.Smo;
using NHibernate;
using NHibernate.Mapping;
using NHibernate.SqlCommand;
using NLog;
using Stream = JMMContracts.PlexContracts.Stream;

namespace JMMServer
{
    public class JMMServiceImplementationPlex : IJMMServerPlex
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private bool GetOptions()
        {
            string origin = WebOperationContext.Current.IncomingRequest.Headers.Get("origin");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Origin", "*");
            if (WebOperationContext.Current.IncomingRequest.Method == "OPTIONS")
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Methods",
                    "POST, GET, OPTIONS, DELETE, PUT, HEAD");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Max-Age", "1209600");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Access-Control-Allow-Headers",
                    "accept, x-plex-token, x-plex-client-identifier, x-plex-username, x-plex-product, x-plex-device, x-plex-platform, x-plex-platform-version, x-plex-version, x-plex-device-name");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Connection", "close");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");
                WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
                WebOperationContext.Current.OutgoingResponse.ContentType = "text/plain";
                return true;
            }

            return false;
        }

        private class Limits
        {
            public int Start { get; set; }
            public int Size { get; set; }

            public Limits(int maxvalue)
            {
                Start = 0;
                Size = maxvalue;
                if ((WebOperationContext.Current.IncomingRequest.UriTemplateMatch != null) &&
                    (WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters != null))
                {
                    if (
                        WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters.AllKeys.Contains(
                            "X-Plex-Container-Start"))
                        Start =
                            int.Parse(
                                WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters[
                                    "X-Plex-Container-Start"]);
                    if (
                        WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters.AllKeys.Contains(
                            "X-Plex-Container-Size"))
                    {
                        int max =
                            int.Parse(
                                WebOperationContext.Current.IncomingRequest.UriTemplateMatch.QueryParameters[
                                    "X-Plex-Container-Size"]);
                        if (max < Size)
                            Size = max;
                    }
                }
            }
        }


        public System.IO.Stream GetFilters(string UserId)
        {


            if (GetOptions())
                return new MemoryStream();
            MediaContainer m = new MediaContainer();
            List<Video> dirs = new List<Video>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    DateTime start = DateTime.Now;
                    GroupFilterRepository repGF = new GroupFilterRepository();
                    JMMUser user = GetUser(session, UserId);
                    if (user == null)
                        return new MemoryStream();
                    m.Title2 = "My Anime";
                    m.NoCache = "1";
                    m.AllowSync = "0";
                    m.ViewMode = "65592";
                    m.ViewGroup = "show";
                    m.Identifier = "com.plexapp.plugins.myanime";
                    m.MediaTagPrefix = "/system/bundle/media/flags/";
                    m.MediaTagVersion = "1375292524";

                    List<GroupFilter> allGfs = repGF.GetAll(session);
                    Dictionary<int, HashSet<int>> gstats = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID];
                    foreach (GroupFilter gg in allGfs.ToArray())
                    {
                        if ((!StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID)) ||
                            (!StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(gg.GroupFilterID)))
                        {
                            allGfs.Remove(gg);
                        }
                    }


                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    allGfs.Insert(0, new GroupFilter() {GroupFilterName = "All", GroupFilterID = -999});
                    foreach (GroupFilter gg in allGfs)
                    {

                        Random rnd = new Random(123456789);
                        Video pp = new Video();
                        pp.Key = "/video/jmm/proxy?url=" +
                                    ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                        MainWindow.PathAddressPlex + "/GetMetadata/" + user.JMMUserID + "/" +
                                        (int) JMMType.GroupFilter + "/" + gg.GroupFilterID);
                        pp.Title = gg.GroupFilterName;
                        HashSet<int> groups;
                        if (gg.GroupFilterID == -999)
                            groups =
                                new HashSet<int>(repGroups.GetAllTopLevelGroups(session).Select(a => a.AnimeGroupID));
                        else
                        {
                            groups = gstats[gg.GroupFilterID];
                        }
                        if (groups.Count != 0)
                        {
                            bool repeat;
                            int nn = 0;
                            pp.LeafCount = groups.Count.ToString();
                            pp.ViewedLeafCount = "0";
                            do
                            {

                                repeat = true;
                                int grp = groups.ElementAt(rnd.Next(groups.Count));
                                AnimeGroup ag = repGroups.GetByID(grp);
                                List<AnimeSeries> sers = ag.GetSeries(session);
                                if (sers.Count > 0)
                                {
                                    AnimeSeries ser = sers[rnd.Next(sers.Count)];
                                    AniDB_Anime anim = ser.GetAnime(session);
                                    if (anim != null)
                                    {

                                        ImageDetails poster = anim.GetDefaultPosterDetailsNoBlanks(session);
                                        ImageDetails fanart = anim.GetDefaultFanartDetailsNoBlanks(session);
                                        if (poster != null)
                                            pp.Thumb = GenPoster(poster);
                                        if (fanart != null)
                                            pp.Art = GenArt(fanart);
                                        if (poster != null)
                                            repeat = false;
                                    }
                                }
                                nn++;
                                if ((repeat) && (nn == 15))
                                    repeat = false;

                            } while (repeat);
                            dirs.Add(pp);
                        }
                    }
                    VideoLocalRepository repVids = new VideoLocalRepository();
                    List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
                    if (vids.Count > 0)
                    {
                        Video pp = new Video();
                        pp.Key = "/video/jmm/proxy?url=" +
                                 ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                     MainWindow.PathAddressPlex + "/GetMetadata/0/" + (int) JMMType.GroupUnsort + "/0");
                        pp.Title = "Unsort";
                        pp.Thumb = ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                            MainWindow.PathAddressPlex + "/GetSupportImage/plex_unsort.png");
                        pp.LeafCount = vids.Count.ToString();
                        pp.ViewedLeafCount = "0";
                        dirs.Add(pp);
                    }
                    dirs = dirs.OrderBy(a => a.Title).ToList();
                }
                m.Directories =  StoreLimits(m, dirs);
                /*
                m.ViewMode="65586";
                m.ViewGroup="video";
                m.ContentType="items";*/
                return GetStreamFromXmlObject(m);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }
        }

        public static string ShowString(string k)
        {
            if (k == null)
                return "NULL";
            if (k == "")
                return "EMPTY";
            return k;
        }

        public System.IO.Stream GetMetadata(string UserId, string TypeId, string Id)
        {
            if (GetOptions())
                return new MemoryStream();
            try
            {
                int type = -1;
                int.TryParse(TypeId, out type);
                if (type == -1)
                    return new MemoryStream();
                switch ((JMMType) type)
                {
                    case JMMType.Group:
                        return GetItemsFromGroup(UserId, Id);
                    case JMMType.GroupFilter:
                        return GetGroupsFromFilter(UserId, Id);
                    case JMMType.GroupUnsort:
                        return GetUnsort();
                    case JMMType.Serie:
                        return GetItemsFromSerie(UserId, Id);
                    case JMMType.File:
                        return InternalGetFile(Id);

                }
                return new MemoryStream();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }

        }

        private System.IO.Stream GetUnsort()
        {
            MediaContainer con = new MediaContainer();

            List<Video> dirs= new List<Video>();
            VideoLocalRepository repVids = new VideoLocalRepository();
            List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
            foreach (VideoLocal v in vids.OrderByDescending(a => a.DateTimeCreated))
            {
                Video m = new Video();
                FromVideoLocalEp(m, v, JMMType.File);
                if (!string.IsNullOrEmpty(m.Duration))
                    dirs.Add(m);
            }
            con.Videos = StoreLimits(con, dirs);
            con.Identifier = "com.plexapp.plugins.myanime";
            con.MediaTagPrefix = "/system/bundle/media/flags/";
            con.MediaTagVersion = "1375292524";

            return GetStreamFromXmlObject(con);
        }

        public System.IO.Stream GetFile(string Id)
        {
            if (GetOptions())
                return new MemoryStream();
            return InternalGetFile(Id);
        }

        private System.IO.Stream InternalGetFile(string Id)
        {

            int id = -1;
            if (!int.TryParse(Id, out id))
                return new MemoryStream();

            VideoLocalRepository repVids = new VideoLocalRepository();
            VideoLocal vi = repVids.GetByID(id);
            if (vi == null)
                return new MemoryStream();
            MediaContainer con = new MediaContainer();
            con.Videos = new List<Video>();
            Video v = new Video();
            AniDB_Anime ani=FromVideoLocalEp(v, vi, JMMType.File);
            if (!string.IsNullOrEmpty(v.Duration))
            {
                v.RatingKey = "VL_" + id.ToString();
                con.Videos.Add(v);
                if (ani != null)
                {
                    con.Title1 = ani.MainTitle;
                    con.Title2 = ani.MainTitle;
                }
            }

            con.Identifier = "com.plexapp.plugins.myanime";
            con.MediaTagPrefix = "/system/bundle/media/flags/";
            con.MediaTagVersion = "1375292524";
            con.AllowSync = "1";
            con.Size = "1";
            return GetStreamFromXmlObject(con);
        }
        public static string Base64EncodeUrl(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes).Replace("+", "-").Replace("/", "_").Replace("=", ",");
        }
        private AniDB_Anime FromVideoLocalEp(Video l, VideoLocal v, JMMType type)
        {
            AniDB_Anime ani=null;
            l.Type = "episode";
            l.Key = "/video/jmm/proxy?url=" +
                    ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                        MainWindow.PathAddressPlex + "/GetMetadata/0/" + (int) type + "/" + v.VideoLocalID);
            VideoInfoRepository repo=new VideoInfoRepository();
            AnimeSeriesRepository repSeries=new AnimeSeriesRepository();
            l.Title = Path.GetFileNameWithoutExtension(v.FilePath);
            List<AnimeEpisode> eps = v.GetAnimeEpisodes();
            if (eps.Count > 0)
            {
                AniDB_Episode epp = eps[0].AniDB_Episode;
                if (epp != null)
                {
                    l.Title = epp.EpisodeNumber + ". " + epp.EnglishName;
                    AnimeSeries series = repSeries.GetByAnimeID(epp.AnimeID);
                    if (series != null)
                    {
                        ani = series.GetAnime();
                        if (ani != null)
                        {
                            if (ani.AnimeTypeEnum == enAnimeType.Movie)
                                l.Type = "movie";
                        }
                    }
                }
            }
            l.AddedAt =
                ((Int32) (v.DateTimeCreated.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(
                    CultureInfo.InvariantCulture);
            l.UpdatedAt =
                ((Int32) (v.DateTimeUpdated.Subtract(new DateTime(1970, 1, 1))).TotalSeconds).ToString(
                    CultureInfo.InvariantCulture);
            l.OriginallyAvailableAt = v.DateTimeCreated.Year.ToString("0000") + "-" + v.DateTimeCreated.Month.ToString("00") + "-" +
                                      v.DateTimeCreated.Day.ToString("00");
            l.Year = v.DateTimeCreated.Year.ToString();
            VideoInfo info = v.VideoInfo;
            Media m = null;
            if (info!=null)
            {
                if (string.IsNullOrEmpty(info.FullInfo))
                {
                    MediaInfoResult mInfo=FileHashHelper.GetMediaInfo(v.FullServerPath, true);
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
                }
                if (!string.IsNullOrEmpty(info.FullInfo))
                {
                    XmlSerializer ser = new XmlSerializer(typeof (Media));
                    m = XmlDeserializeFromString<Media>(info.FullInfo);
                }
            }


            l.Medias = new List<Media>();
            if (m != null)
            {
                int pp = 1;
                m.Id= "VL_" + v.VideoLocalID.ToString();
                List<Stream> subs = SubtitleHelper.GetSubtitleStreams(v.FullServerPath);
                if (subs.Count > 0)
                {
                    foreach (Stream s in subs)
                    {
                        //s.Key = ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetStream/" + "file/" + Base64EncodeUrl(s.File));

                        s.Key = ServerUrl(int.Parse(ServerSettings.JMMServerFilePort), "file/" + Base64EncodeUrl(s.File));                        
                    }
                    m.Parts[0].Streams.AddRange(subs);
                }
                foreach (Part p in m.Parts)
                {
                    p.Id = "VL_" + v.VideoLocalID.ToString();
                    pp++;
                    p.File = v.FullServerPath;
                    string ff = Path.GetExtension(v.FullServerPath);
                    //p.Key = ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetStream/" + "videolocal/" + v.VideoLocalID + "/file" + ff);

                    p.Key = ServerUrl(int.Parse(ServerSettings.JMMServerFilePort), "videolocal/" + v.VideoLocalID + "/file" + ff);
                    p.Accessible = "1";
                    p.Exists = "1";
                    bool vid = false;
                    bool aud = false;
                    bool txt = false;
                    int xx = 1;
                    foreach (JMMContracts.PlexContracts.Stream ss in p.Streams.ToArray())
                    {
                        ss.Id = "VL_" + v.VideoLocalID + "_" + xx.ToString();
                        xx++;

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
            return ani;
        }

        public class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }
        }

        private static System.IO.Stream GetStreamFromXmlObject<T>(T obj)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            Utf8StringWriter textWriter = new Utf8StringWriter();
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");
            WebOperationContext.Current.OutgoingResponse.Headers.Add("Cache-Control", "no-cache");
            WebOperationContext.Current.OutgoingResponse.ContentType = "application/xml";
            xmlSerializer.Serialize(textWriter, obj,ns);
            return new MemoryStream(Encoding.UTF8.GetBytes(textWriter.ToString()));

        }

        private static T XmlDeserializeFromString<T>(string objectData)
        {
            return (T) XmlDeserializeFromString(objectData, typeof (T));
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

        public System.IO.Stream GetUsers()
        {
            PlexContract_Users gfs = new PlexContract_Users();
            try
            {
                gfs.Users=new List<PlexContract_User>();
                JMMUserRepository repUsers = new JMMUserRepository();
                foreach (JMMUser us in repUsers.GetAll())
                {
                    PlexContract_User p = new PlexContract_User();
                    p.id = us.JMMUserID.ToString();
                    p.name = us.Username;
                    gfs.Users.Add(p);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return GetStreamFromXmlObject(gfs);
        }

        public System.IO.Stream Search(string UserId, string limit, string query)
        {

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                query = System.Web.HttpUtility.UrlDecode(query);
                MediaContainer m=new MediaContainer();
                m.NoCache = "1";
                m.AllowSync = "0";
                m.ViewMode = "65592";
                m.ViewGroup = "show";
                m.Identifier = "com.plexapp.plugins.myanime";
                m.MediaTagPrefix = "/system/bundle/media/flags/";
                m.MediaTagVersion = "1375292524";
                m.Title2 = "Search Results for '"+query+"'...";
                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                int lim;
                if (!int.TryParse(limit, out lim))
                    lim = 20;
                JMMUser user = GetUser(session, UserId);
                if (user == null) return new MemoryStream();
                List<Video> ls=new List<Video>();
                int cnt = 0;
                List<AniDB_Anime> animes = repAnime.SearchByName(session, query );
                foreach (AniDB_Anime anidb_anime in animes)
                {

                    if (!user.AllowedAnime(anidb_anime)) continue;
                    AnimeSeries ser = repSeries.GetByAnimeID(session,anidb_anime.AnimeID);
                    if (ser != null)
                    {
                        Video v=FromSerie(ser.ToContract(ser.GetUserRecord(session, user.JMMUserID),true),user.JMMUserID);
                        switch (anidb_anime.AnimeTypeEnum)
                        {
                            case enAnimeType.Movie:
                                v.SourceTitle = "Anime Movies";
                                v.Type = "movie";
                                break;
                            case enAnimeType.OVA:
                                v.SourceTitle = "Anime Ovas";
                                v.Type = "show";
                                break;
                            case enAnimeType.Other:
                                v.SourceTitle = "Anime Others";
                                v.Type = "show";
                                break;
                            case enAnimeType.TVSeries:
                                v.SourceTitle = "Anime Series";
                                v.Type = "show";
                                break;
                            case enAnimeType.TVSpecial:
                                v.SourceTitle = "Anime Specials";
                                v.Type = "show";
                                break;
                            case enAnimeType.Web:
                                v.SourceTitle = "Anime Web Clips";
                                v.Type = "show";
                                break;

                        }
                        
                        ls.Add(v);
                        cnt++;
                        if (cnt == lim)
                            break;
                    }
                }
                m.Directories = StoreLimits(m, ls.OrderBy(a => a.Title).ToList());
                return GetStreamFromXmlObject(m);
            }
        }

        private JMMUser GetUser(ISession session, string UserId)
        {
            int userId = -1;
            if (!string.IsNullOrEmpty(UserId))
                int.TryParse(UserId, out userId);
            JMMUserRepository repUsers = new JMMUserRepository();
            return userId != 0
                ? repUsers.GetByID(session, userId)
                : repUsers.GetAll(session).FirstOrDefault(a => a.Username == "Default");
        }

        internal static Video FromGroup(Contract_AnimeGroup grp, Contract_AnimeSeries ser, int userid)
        {
            Video p = new Video();
            p.Key = "/video/jmm/proxy?url=" +
                    ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                        MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int) JMMType.Group + "/" +
                        grp.AnimeGroupID.ToString());
            p.Title = grp.GroupName;
            p.Summary = grp.Description;
            p.Type = "show";
            p.AirDate = grp.Stat_AirDate_Min.HasValue ? grp.Stat_AirDate_Min.Value : DateTime.MinValue;
            Contract_AniDBAnime anime = ser.AniDBAnime;
            if (anime != null)
            {

                Contract_AniDB_Anime_DefaultImage poster = anime.DefaultImagePoster;
                Contract_AniDB_Anime_DefaultImage fanart = anime.DefaultImageFanart;
                p.Thumb = poster != null ? GenPoster(poster) : ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetSupportImage/plex_404V.png");
                if (fanart != null)
                    p.Art = GenArt(fanart);
            }
            p.LeafCount = (grp.UnwatchedEpisodeCount + grp.WatchedEpisodeCount).ToString();
            p.ViewedLeafCount = grp.WatchedEpisodeCount.ToString();
            return p;
        }

        
        private static string GenPoster(ImageDetails im)
        {
            if (im == null)
                return null;
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetThumb/" + (int) im.ImageType + "/" + im.ImageID + "/1.0");
        }

        private static string GenArt(ImageDetails im)
        {
            if (im == null)
                return null;
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetImage/" + (int) im.ImageType + "/" + im.ImageID);
        }

        private static string GenPoster(Contract_AniDB_Anime_DefaultImage im)
        {
            if (im == null)
                return null;

            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetThumb/" + (int) im.ImageType + "/" + im.AnimeID + "/1.0");
        }

        private static string GenArt(Contract_AniDB_Anime_DefaultImage im)
        {
            if (im == null)
                return null;

            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetImage/" + (int) im.ImageType + "/" + im.AnimeID);
        }
        
        internal static Video FromSerie(Contract_AnimeSeries ser, int userid)
        {
            Video p = new Video();

            Contract_AniDBAnime anime = ser.AniDBAnime;

            p.Key = "/video/jmm/proxy?url=" +
                    ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                        MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int) JMMType.Serie + "/" +
                        ser.AnimeSeriesID);


            p.Title = anime.MainTitle;
            p.Summary = anime.Description;
            p.Type = "show";
            p.AirDate = DateTime.MinValue;

            if (anime != null)
            {
                if (!string.IsNullOrEmpty(anime.AllCategories))
                {
                    p.Genres = new List<Tag> {new Tag {Value = anime.AllCategories.Replace("|", ",")}};
                }
                if (!string.IsNullOrEmpty(anime.AllTags))
                {
                    p.Tags = new List<Tag> {new Tag {Value = anime.AllTags.Replace("|", ",")}};
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
                p.Rating = ((float)anime.Rating / 100F).ToString();
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
                p.Thumb = poster != null ? GenPoster(poster) : ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetSupportImage/plex_404V.png");
                if (fanart != null)
                    p.Art = GenArt(fanart);
            }

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

            
            return p;
        }
     

        public System.IO.Stream GetSupportImage(string name)
        {
            if (string.IsNullOrEmpty(name))
                return new MemoryStream();
            name = Path.GetFileNameWithoutExtension(name);
            System.Resources.ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[]) man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return new MemoryStream();
            WebOperationContext.Current.OutgoingResponse.ContentType = "image/png";
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public static string ServerUrl(int port, string path)
        {
            if ((WebOperationContext.Current == null) ||
                (WebOperationContext.Current.IncomingRequest.UriTemplateMatch == null))
            {
                return "{SCHEME}://{HOST}:" + port + "/" +path;
            }
            return WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme + "://" +
                   WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Host + ":" + port + "/" +
                   path;
        }

        public static string ReplaceSchemeHost(string str)
        {
            if (str == null)
                return null;
            return str.Replace("{SCHEME}", WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme).Replace("{HOST}", WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Host);
        }
        public static Video CloneVideo(Video o)
	    {
	        Video v=new Video();
	        v.AddedAt = o.AddedAt;
	        v.AirDate = o.AirDate;
	        v.Art = ReplaceSchemeHost(o.Art);
	        v.Duration = o.Duration;
	        v.EpNumber = o.EpNumber;
	        v.EpisodeCount = o.EpisodeCount;
	        v.Genres = o.Genres;
	        v.Group = o.Group;
	        v.Guid = o.Guid;
            v.Key = ReplaceSchemeHost(o.Key);
            v.LeafCount = o.LeafCount;
            v.Medias = o.Medias;
            v.OriginalTitle = o.OriginalTitle;
            v.OriginallyAvailableAt = o.OriginallyAvailableAt;
            v.Rating = o.Rating;
            v.RatingKey = o.RatingKey;
            v.Roles = o.Roles;
            v.Season = o.Season;
            v.SourceTitle = o.SourceTitle;
            v.Summary = o.Summary;
            v.Tags = o.Tags;
            v.Thumb = ReplaceSchemeHost(o.Thumb);
            v.Title = o.Title;
            v.Type = o.Type;
            v.UpdatedAt = o.UpdatedAt;
            v.Url = ReplaceSchemeHost(o.Url);
            v.ViewCount = o.ViewCount;
            v.ViewOffset = o.ViewOffset;
            v.ViewedLeafCount = o.ViewedLeafCount;
            v.Year = o.Year;
            return v;
	    }
        public System.IO.Stream GetItemsFromGroup(string UserId, string GroupId)
        {
            MediaContainer m = new MediaContainer();
            m.NoCache = "1";
            m.AllowSync = "1";
            m.ViewMode = "65592";
            m.ViewGroup = "show";
            m.Identifier = "com.plexapp.plugins.myanime";
            m.MediaTagPrefix = "/system/bundle/media/flags/";
            m.MediaTagVersion = "1375292524";



            int groupID = -1;
            int.TryParse(GroupId, out groupID);
            List<Video> retGroups = new List<Video>();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                if (groupID == -1)
                    return new MemoryStream();
                JMMUser user = GetUser(session, UserId);
                if (user == null) return new MemoryStream();
                AnimeGroupRepository repGroups = new AnimeGroupRepository();
                AnimeGroup grp = repGroups.GetByID(groupID);
                Random rnd = new Random(123456789);
                if (grp != null)
                {
                    Contract_AnimeGroup basegrp = grp.ToContract(grp.GetUserRecord(session, user.JMMUserID));
                        
                    List<AnimeSeries> sers2 = grp.GetSeries(session);
                    if (sers2.Count > 0)
                    {
                        AnimeSeries ser = sers2[rnd.Next(sers2.Count)];
                        AniDB_Anime anim = ser.GetAnime(session);
                        if (anim != null)
                        {

                            ImageDetails fanart = anim.GetDefaultFanartDetailsNoBlanks(session);
                            if (fanart != null)
                                m.Art = GenArt(fanart);
                        }
                    }


                    foreach (AnimeGroup grpChild in grp.GetChildGroups())
                    {
                        Video v = StatsCache.Instance.StatPlexGroupsCache[user.JMMUserID][grpChild.AnimeGroupID];
                        if (v != null)
                            retGroups.Add(CloneVideo(v));
                        /*

                        
                        Contract_AnimeGroup cgrp = grpChild.ToContract(grpChild.GetUserRecord(session, user.JMMUserID));
                        List<AnimeSeries> sers = grpChild.GetSeries();
                        if (StatsCache.Instance.StatGroupSeriesCount[grpChild.AnimeGroupID] == 1)
                        {


                            if ((sers != null) && (sers.Count > 0))
                            {
                                Video v = FromSerie(sers[0].ToContract(sers[0].GetUserRecord(session, user.JMMUserID), true), user.JMMUserID);
                                v.AirDate = sers[0].AirDate.HasValue ? sers[0].AirDate.Value : DateTime.MinValue;
                                v.Group = cgrp;
                                retGroups.Add(v);
                            }
                        }
                        else
                        {
                            if ((sers != null) && (sers.Count > 0))
                            {
                                Video v = FromGroup(cgrp, sers[0].ToContract(sers[0].GetUserRecord(session, user.JMMUserID), true), user.JMMUserID);
                                v.Group = cgrp;
                                v.AirDate = cgrp.Stat_AirDate_Min.HasValue ? cgrp.Stat_AirDate_Min.Value : DateTime.MinValue;
                                retGroups.Add(v);
                            }
                        }*/
                    }
                    foreach (AnimeSeries ser in grp.GetSeries())
                    {
                        Video v = FromSerie(ser.ToContract(ser.GetUserRecord(session, user.JMMUserID), true), user.JMMUserID);
                        v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                        v.Group = basegrp;
                        retGroups.Add(v);
                    }
                }

                m.Directories = StoreLimits(m,retGroups.OrderBy(a=>a.AirDate).ToList()).ToList();
                return GetStreamFromXmlObject(m);
            }
        }


        public static void EpisodeTypeTranslated(EpisodeType tp, enEpisodeType epType, AnimeTypes an, int cnt)
        {
            tp.Type = (int) epType;
            tp.Count = cnt;
            bool plural = cnt > 1;
            switch (epType)
            {
                case enEpisodeType.Credits:
                    tp.Name = plural ? "Credits" : "Credit";
                    tp.Image = "plex_credits.png";
                    return;
                case enEpisodeType.Episode:
                    switch (an)
                    {
                        case AnimeTypes.Movie:
                            tp.Name = plural ? "Movies" : "Movie";
                            tp.Image = "plex_movies.png";
                            return;
                        case AnimeTypes.OVA:
                            tp.Name = plural ? "Ovas" : "Ova";
                            tp.Image = "plex_ovas.png";
                            return;
                        case AnimeTypes.Other:
                            tp.Name = plural ? "Others" : "Other";
                            tp.Image = "plex_others.png";
                            return;
                        case AnimeTypes.TV_Series:
                            tp.Name = plural ? "Episodes" : "Episode";
                            tp.Image = "plex_episodes.png";
                            return;
                        case AnimeTypes.TV_Special:
                            tp.Name = plural ? "TV Episodes" : "TV Episode";
                            tp.Image = "plex_tvepisodes.png";
                            return;
                        case AnimeTypes.Web:
                            tp.Name = plural ? "Web Clips" : "Web Clip";
                            tp.Image = "plex_webclips.png";
                            return;
                    }
                    tp.Name = plural ? "Episodes" : "Episode";
                    tp.Image = "plex_episodes.png";
                    return;
                case enEpisodeType.Parody:
                    tp.Name = plural ? "Parodies" : "Parody";
                    tp.Image = "plex_parodies.png";
                    return;
                case enEpisodeType.Special:
                    tp.Name = plural ? "Specials" : "Special";
                    tp.Image = "plex_specials.png";
                    return;
                case enEpisodeType.Trailer:
                    tp.Name = plural ? "Trailers" : "Trailer";
                    tp.Image = "plex_trailers.png";
                    return;
                default:
                    tp.Name = "Misc";
                    tp.Image = "plex_misc.png";
                    return;
            }
        }

        public class EpisodeType
        {
            public string Name { get; set; }
            public int Type { get; set; }
            public string Image { get; set; }

            public int Count { get; set; }

        }

        public System.IO.Stream GetItemsFromSerie(string UserId, string SerieId)
        {
            MediaContainer m = new MediaContainer();
            m.NoCache = "1";
            m.AllowSync = "1";
            m.ViewMode = "65592";
            m.ViewGroup = "show";
            m.Identifier = "com.plexapp.plugins.myanime";
            m.MediaTagPrefix = "/system/bundle/media/flags/";
            m.MediaTagVersion = "1375292524";
            enEpisodeType? eptype = null;
            int serieID = -1;
            if (SerieId.Contains("_"))
            {
                int ept = 0;
                string[] ndata = SerieId.Split('_');
                if (!int.TryParse(ndata[0], out ept))
                    return new MemoryStream();
                eptype = (enEpisodeType) ept;
                if (!int.TryParse(ndata[1], out serieID))
                    return new MemoryStream();
            }
            else
            {
                if (!int.TryParse(SerieId, out serieID))
                    return new MemoryStream();
            }

            
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                if (serieID == -1)
                    return new MemoryStream();
                JMMUser user = GetUser(session, UserId);
                if (user == null) return new MemoryStream();
                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                AnimeSeries ser = repSeries.GetByID(session, serieID);
                if (ser == null)
                    return new MemoryStream();
                AniDB_Anime anime = ser.GetAnime();
                if (anime == null)
                    return new MemoryStream();

                ImageDetails fanart = anime.GetDefaultFanartDetailsNoBlanks(session);
                if (fanart != null)
                    m.Art = GenArt(fanart);
                m.Title2 = anime.MainTitle;
                List<AnimeEpisode> episodes =
                    ser.GetAnimeEpisodes(session).Where(a => a.GetVideoLocals(session).Count > 0).ToList();
                if (eptype.HasValue)
                {
                    episodes = episodes.Where(a => a.EpisodeTypeEnum == eptype.Value).ToList();
                }
                else
                {
                    List<enEpisodeType> types = episodes.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    if (types.Count > 1)
                    {
                        List<EpisodeType> eps = new List<EpisodeType>();
                        foreach (enEpisodeType ee in types)
                        {
                            EpisodeType k = new EpisodeType();
                            EpisodeTypeTranslated(k, ee, (AnimeTypes) anime.AnimeType,
                                episodes.Count(a => a.EpisodeTypeEnum == ee));
                            eps.Add(k);
                        }
                        List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("Name", SortType.eString));
                        eps = Sorting.MultiSort(eps, sortCriteria);
                        List<Video> dirs= new List<Video>();

                        foreach (EpisodeType ee in  eps)
                        {
                            Video v = new Video();
                            v.Title = ee.Name;
                            v.Type = "season";
                            v.LeafCount = ee.Count.ToString();
                            v.ViewedLeafCount = "0";
                            v.Key = "/video/jmm/proxy?url=" +
                                    ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                        MainWindow.PathAddressPlex + "/GetMetadata/" + user.JMMUserID + "/" +
                                        (int) JMMType.Serie + "/" + ee.Type + "_" + ser.AnimeSeriesID);
                            v.Thumb = ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                MainWindow.PathAddressPlex + "/GetSupportImage/" + ee.Image);
                            dirs.Add(v);
                        }
                        m.Directories = StoreLimits(m, dirs);
                        return GetStreamFromXmlObject(m);
                    }
                }
                List<Tag> genres = null;
                if (!string.IsNullOrEmpty(anime.AllCategories))
                {
                    genres =
                        anime.AllCategories.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => new Tag() {Value = a})
                            .ToList();
                }
                List<Tag> tags = null;
                if (!string.IsNullOrEmpty(anime.AllTags))
                {
                    tags =
                        anime.AllTags.Split(new char[] {'|'}, StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => new Tag() {Value = a})
                            .ToList();
                }
                List<Video> vids=new List<Video>();
                foreach (AnimeEpisode ep in episodes)
                {
                    Video v = new Video();
                    List<VideoLocal> locals = ep.GetVideoLocals(session);
                    if ((locals == null) || (locals.Count == 0))
                        continue;
                    AniDB_Episode aep = ep.AniDB_Episode;
                    if (aep == null)
                        continue;
                    VideoLocal current = locals[0];
                    FromVideoLocalEp(v, current, JMMType.File);
                    v.Title = aep.EpisodeNumber + ". " + aep.EnglishName;
                    v.EpNumber = aep.EpisodeNumber;
                    v.OriginalTitle = aep.RomajiName;
                    v.Type = "movie";
                    v.Genres = genres;
                    v.Tags = tags;
                    v.Rating = (float.Parse(aep.Rating)).ToString();
                    if (aep.AirDateAsDate.HasValue)
                    {
                        v.Year = aep.AirDateAsDate.Value.Year.ToString();
                        v.OriginallyAvailableAt = aep.AirDateAsDate.Value.Year.ToString("0000") + "-" + aep.AirDateAsDate.Value.Month.ToString("00") +
                                                  "-" + aep.AirDateAsDate.Value.Day.ToString("00");
                    }
                    AnimeEpisode_User epuser = ep.GetUserRecord(session, user.JMMUserID);
                    if (epuser != null)
                    {
                        v.ViewCount = epuser.WatchedCount.ToString();
                    }

                    MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
                    JMMServiceImplementationMetro.SetTvDBInfo(anime, aep, ref contract);
                    if (contract.ImageID != 0)
                        v.Thumb = ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                            MainWindow.PathAddressREST + "/GetThumb/" + (int) contract.ImageType + "/" +
                            contract.ImageID + "/1.33333");
                    else
                        v.Thumb = ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                            MainWindow.PathAddressPlex + "/GetSupportImage/plex_404.png");
                    v.Summary = contract.EpisodeOverview;
                    vids.Add(v);
                }

                List<SortPropOrFieldAndDirection> sortCriteria2 = new List<SortPropOrFieldAndDirection>();
                sortCriteria2.Add(new SortPropOrFieldAndDirection("EpNumber", SortType.eInteger));
                vids= Sorting.MultiSort(vids, sortCriteria2);
                m.Videos = StoreLimits(m, vids);
                return GetStreamFromXmlObject(m);
            }
        }

        private System.IO.Stream GetGroupsFromFilter(string UserId, string GroupFilterId)
        {
            MediaContainer m = new MediaContainer();
            m.NoCache = "1";
            m.AllowSync = "1";
            m.ViewMode = "65592";
            m.ViewGroup = "show";
            m.Identifier = "com.plexapp.plugins.myanime";
            m.MediaTagPrefix = "/system/bundle/media/flags/";
            m.MediaTagVersion = "1375292524";

            //List<Joint> retGroups = new List<Joint>();
            List<Video> retGroups=new List<Video>();
            try
            {
                int groupFilterID = -1;
                int.TryParse(GroupFilterId, out groupFilterID);
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    if (groupFilterID == -1)
                        return new MemoryStream();
                    DateTime start = DateTime.Now;
                    GroupFilterRepository repGF = new GroupFilterRepository();

                    JMMUser user = GetUser(session, UserId);
                    if (user == null) return new MemoryStream();

                    GroupFilter gf = null;

                    if (groupFilterID == -999)
                    {
                        // all groups
                        gf = new GroupFilter();
                        gf.GroupFilterName = "All";
                    }
                    else
                    {
                        gf = repGF.GetByID(session, groupFilterID);
                        if (gf == null) return new MemoryStream();
                    }
                    m.Title2 = gf.GroupFilterName;
                    //Contract_GroupFilterExtended contract = gf.ToContractExtended(user);

                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    List<AnimeGroup> allGrps = repGroups.GetAll(session);

                    AnimeGroup_UserRepository repUserRecords = new AnimeGroup_UserRepository();
                    List<AnimeGroup_User> userRecords = repUserRecords.GetByUserID(session, user.JMMUserID);
                    Dictionary<int, AnimeGroup_User> dictUserRecords = new Dictionary<int, AnimeGroup_User>();
                    foreach (AnimeGroup_User userRec in userRecords)
                        dictUserRecords[userRec.AnimeGroupID] = userRec;

                    TimeSpan ts = DateTime.Now - start;
                    string msg = string.Format("Got groups for filter DB: {0} - {1} in {2} ms", gf.GroupFilterName,
                        allGrps.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    start = DateTime.Now;
                    AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                    List<AnimeSeries> allSeries = repSeries.GetAll(session);

                    if ((StatsCache.Instance.StatUserGroupFilter.ContainsKey(user.JMMUserID)) &&
                        (StatsCache.Instance.StatUserGroupFilter[user.JMMUserID].ContainsKey(gf.GroupFilterID)))
                    {
                        HashSet<int> groups = StatsCache.Instance.StatUserGroupFilter[user.JMMUserID][gf.GroupFilterID];

                        foreach (AnimeGroup grp in allGrps)
                        {
                            if (groups.Contains(grp.AnimeGroupID))
                            {
                                Video v = StatsCache.Instance.StatPlexGroupsCache[user.JMMUserID][grp.AnimeGroupID];
                                if (v!=null)
                                retGroups.Add(CloneVideo(v));
/*
                                Contract_AnimeGroup cgrp = grp.ToContract(grp.GetUserRecord(session, user.JMMUserID));

                                if (StatsCache.Instance.StatGroupSeriesCount[grp.AnimeGroupID] == 1)
                                {
                                    AnimeSeries ser = JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                                    if (ser != null)
                                    {
                                        retGroups.Add(Joint.CreateFromSerie(ser.ToContract(ser.GetUserRecord(session, user.JMMUserID),true), cgrp, ser.AirDate, user.JMMUserID));
                                    }

                                }
                                else
                                {
                                    AnimeSeries ser = grp.DefaultAnimeSeriesID.HasValue ? allSeries.FirstOrDefault(a => a.AnimeSeriesID == grp.DefaultAnimeSeriesID.Value) : JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                                    if (ser!=null)
                                        retGroups.Add(Joint.CreateFromGroup(cgrp,ser.ToContract(ser.GetUserRecord(session, user.JMMUserID),true),user.JMMUserID));
                                }
 */
                            }
                        }
                    }
                    ts = DateTime.Now - start;
                    msg = string.Format("Got groups for filter EVAL: {0} - {1} in {2} ms", gf.GroupFilterName,
                        retGroups.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    if ((groupFilterID == -999) || (gf.SortCriteriaList.Count == 0))
                    {
                        //                        m.Directories = StoreLimits(m,retGroups.OrderBy(a => a.Group.SortName).Select(a => a.ToVideo()).ToList());

                        m.Directories = StoreLimits(m,retGroups.OrderBy(a => a.Group.SortName).ToList()).ToList();
                        return GetStreamFromXmlObject(m);
                    }
                    List<Contract_AnimeGroup> grps = retGroups.Select(a => a.Group).ToList();
                    List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                    foreach (GroupFilterSortingCriteria g in gf.SortCriteriaList)
                    {
                        sortCriteria.Add(GroupFilterHelper.GetSortDescription(g.SortType, g.SortDirection));
                    }
                    grps = Sorting.MultiSort(grps, sortCriteria);
                    /*
                    
                    List<Joint> joints2 = new List<Joint>();
                    foreach (Contract_AnimeGroup gr in grps)
                    {
                        foreach (Joint j in retGroups)
                        {
                            if (j.Group == gr)
                            {
                                joints2.Add(j);
                                retGroups.Remove(j);
                                break;
                            }
                        }
                    }
                    */
                    List<Video> joints2 = new List<Video>();
                    foreach (Contract_AnimeGroup gr in grps)
                    {
                        foreach (Video j in retGroups)
                        {
                            if (j.Group == gr)
                            {
                                joints2.Add(j);
                                retGroups.Remove(j);
                                break;
                            }
                        }
                    }
                    m.Directories = StoreLimits(m, joints2).ToList();
                    ts = DateTime.Now - start;
                    msg = string.Format("Got groups final: {0} - {1} in {2} ms", gf.GroupFilterName,
                        retGroups.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    return GetStreamFromXmlObject(m);

                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return new MemoryStream();
        }

        public static Video VideoFromAnimeGroup(ISession session, AnimeGroup grp, int userid, List<AnimeSeries> allSeries)
        {
            Contract_AnimeGroup cgrp = grp.ToContract(grp.GetUserRecord(session, userid));
            if (StatsCache.Instance.StatGroupSeriesCount[grp.AnimeGroupID] == 1)
            {
                AnimeSeries ser = JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                if (ser != null)
                {
                    Video v = FromSerie(ser.ToContract(ser.GetUserRecord(session, userid), true), userid);
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
                    v.AirDate=cgrp.Stat_AirDate_Min.HasValue ? cgrp.Stat_AirDate_Min.Value : DateTime.MinValue;
                    return v;
                }
            }
            return null;
        }

        public List<Video> StoreLimits(MediaContainer m,List<Video> list)
        {
            m.TotalSize =list.Count.ToString();
            Limits lm = new Limits(list.Count);
            m.Offset = lm.Start.ToString();
            m.Size = lm.Size.ToString();
            return list.Skip(lm.Start).Take(lm.Size).ToList();
        }

    }
}
