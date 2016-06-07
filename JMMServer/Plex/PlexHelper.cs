using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Web;
using System.Text;
using System.Xml.Serialization;
using AniDBAPI;
using JMMContracts;
using JMMContracts.PlexContracts;
using JMMFileHelper;
using JMMFileHelper.Subtitles;
using JMMServer.Entities;
using JMMServer.Repositories;
using NHibernate;
using Directory = JMMContracts.PlexContracts.Directory;
using Stream = System.IO.Stream;

// ReSharper disable FunctionComplexityOverflow

namespace JMMServer.Plex
{
    public static class PlexHelper
    {
        public const string MediaTagVersion = "1454470850";
        public static Dictionary<string, double> _relations = new Dictionary<string, double>();


        public static MediaContainer NewMediaContainer(MediaContainerTypes type, HistoryInfo info = null,
            bool allowsync = true, bool nocache = true)
        {
            var m = new MediaContainer();
            m.AllowSync = allowsync ? "1" : "0";
            m.NoCache = nocache ? "1" : "0";
            m.MediaTagVersion = MediaTagVersion;
            m.Identifier = "com.plexapp.plugins.myanime";
            m.MediaTagPrefix = "/system/bundle/media/flags/";
            m.LibrarySectionTitle = "Anime";
            if (info != null)
                m.FillInfo(info);
            m.GrandparentTitle = m.ParentTitle ?? "";
            m.Title1 = m.Title2 = m.Title;
            m.ParentTitle = "";
            m.Title = null;
            switch (type)
            {
                case MediaContainerTypes.Show:
                    m.ViewGroup = "show";
                    m.ViewMode = "65592";

                    break;
                case MediaContainerTypes.Episode:
                    m.ViewGroup = "episode";
                    m.ViewMode = "65592";
                    break;
                case MediaContainerTypes.Video:
                    m.ViewMode = "65586";
                    m.ViewGroup = "video";
                    break;
                case MediaContainerTypes.Season:
                    m.ViewMode = "65593";
                    m.ViewGroup = "season";
                    break;
                case MediaContainerTypes.Movie:
                case MediaContainerTypes.File:
                    break;
            }
            return m;
        }

        public static string ConstructUnsortUrl(int userid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.GroupUnsort + "/0/");
        }

        public static string ConstructGroupIdUrl(int userid, int gid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Group + "/" + gid);
        }

        public static string ConstructSerieIdUrl(int userid, string sid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Serie + "/" + sid);
        }

        public static string ContructVideoLocalIdUrl(int userid, int vid, JMMType type)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)type + "/" + vid);
        }

        public static string ConstructFilterIdUrl(int userid, int gfid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.GroupFilter + "/" + gfid);
        }

        public static string ConstructFiltersUrl(int userid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetFilters/" + userid);
        }

        public static string ConstructSearchUrl(string userid, string limit, string query)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/Search/" + WebUtility.UrlEncode(userid) + "/" + limit + "/" +
                WebUtility.UrlEncode(query));
        }

        public static string ConstructPlaylistUrl(int userid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Playlist + "/0");
        }

        public static string ConstructPlaylistIdUrl(int userid, int pid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Playlist + "/" + pid);
        }

        public static string ConstructVideoLocalStream(int userid, int vid, string extension)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerFilePort),
                "videolocal/" + userid + "/" + vid + "/file" + extension, PlexObject.IsExternalRequest);
        }

        public static string ConstructFileStream(int userid, string file)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerFilePort), "file/" + userid + "/" + Base64EncodeUrl(file),
                PlexObject.IsExternalRequest);
        }

        public static string ConstructImageLink(int type, int id)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetImage/" + type + "/" + id);
        }

        public static string ConstructSupportImageLink(string name)
        {
            var relation = GetRelation();
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetSupportImage/" + name + "/" + relation);
        }

        public static string ConstructSupportImageLinkTV(string name)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressPlex + "/GetSupportImage/" + name);
        }

        public static string ConstructThumbLink(int type, int id)
        {
            var relation = GetRelation();
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                MainWindow.PathAddressREST + "/GetThumb/" + type + "/" + id + "/" + relation);
        }

        private static double GetRelation()
        {
            if (_relations.Count == 0)
            {
                var aspects = ServerSettings.PlexThumbnailAspects.Split(',');
                for (var x = 0; x < aspects.Length; x += 2)
                {
                    var key = aspects[x].Trim().ToUpper();
                    var val = 0.66667D;
                    double.TryParse(aspects[x + 1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out val);
                    _relations.Add(key, val);
                }
                if (!_relations.ContainsKey("DEFAULT"))
                {
                    _relations.Add("DEFAULT", 0.666667D);
                }
            }
            if (WebOperationContext.Current != null &&
                WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
            {
                var kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                foreach (var n in _relations.Keys.Where(a => a != "DEFAULT"))
                {
                    if (kh.Contains(n))
                        return _relations[n];
                }
            }
            return _relations["DEFAULT"];
        }

        public static string Base64EncodeUrl(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes).Replace("+", "-").Replace("/", "_").Replace("=", ",");
        }

        public static string ToHex(string ka)
        {
            var ba = Encoding.UTF8.GetBytes(ka);
            var hex = new StringBuilder(ba.Length * 2);
            foreach (var b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }

        public static string FromHex(string hex)
        {
            var raw = new byte[hex.Length / 2];
            for (var i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return Encoding.UTF8.GetString(raw);
        }

        public static string PlexProxy(string url)
        {
            return "/video/jmm/proxy/" + ToHex(url);
        }

        public static Stream GetStreamFromXmlObject<T>(T obj)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            var textWriter = new Utf8StringWriter();
            var ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            if (WebOperationContext.Current != null)
            {
                WebOperationContext.Current.OutgoingResponse.Headers.Add("X-Plex-Protocol", "1.0");
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
            var repUsers = new JMMUserRepository();
            var allusers = repUsers.GetAll();
            foreach (var n in allusers)
            {
                if (!string.IsNullOrEmpty(n.PlexUsers))
                {
                    var users = n.PlexUsers.Split(',');
                    foreach (var m in users)
                    {
                        if (m.Trim().ToLower() == UserId.ToLower())
                            return n;
                    }
                }
            }
            return allusers.FirstOrDefault(a => a.IsAdmin == 1) ??
                   allusers.FirstOrDefault(a => a.Username == "Default") ?? allusers.First();
        }

        public static JMMUser GetJMMUser(string UserId)
        {
            var repUsers = new JMMUserRepository();
            var allusers = repUsers.GetAll();
            var id = 0;
            int.TryParse(UserId, out id);
            return allusers.FirstOrDefault(a => a.JMMUserID == id) ??
                   allusers.FirstOrDefault(a => a.IsAdmin == 1) ??
                   allusers.FirstOrDefault(a => a.Username == "Default") ?? allusers.First();
        }

        public static string ServerUrl(int port, string path, bool externalip = false)
        {
            if ((WebOperationContext.Current == null) ||
                (WebOperationContext.Current.IncomingRequest.UriTemplateMatch == null))
            {
                return "{SCHEME}://{HOST}:" + port + "/" + path;
            }
            var host = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Host;
            if (externalip)
            {
                var ip = FileServer.FileServer.GetExternalAddress();
                if (ip != null)
                    host = ip.ToString();
            }
            return WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme + "://" +
                   host + ":" + port + "/" +
                   path;
        }

        public static string ReplaceSchemeHost(string str, bool externalip = false)
        {
            if (str == null)
                return null;
            if (WebOperationContext.Current == null)
                return null;
            var host = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Host;
            if (externalip)
            {
                var ip = FileServer.FileServer.GetExternalAddress();
                if (ip != null)
                    host = ip.ToString();
            }
            return
                str.Replace("{SCHEME}", WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme)
                    .Replace("{HOST}", host);
        }


        private static void PopulateVideoEpisodeFromVideoLocal(Video l, VideoLocal v, JMMType type, int userid)
        {
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available"; //TODO Intenationalization
            l.Title = Path.GetFileNameWithoutExtension(v.FilePath);
            l.Key = ContructVideoLocalIdUrl(userid, v.VideoLocalID, type);
            l.AddedAt = v.DateTimeCreated.ToUnixTime();
            l.UpdatedAt = v.DateTimeUpdated.ToUnixTime();
            l.OriginallyAvailableAt = v.DateTimeCreated.ToPlexDate();
            l.Year = v.DateTimeCreated.Year.ToString();
            var info = v.VideoInfo;

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
                    var repo = new VideoInfoRepository();
                    var mInfo = FileHashHelper.GetMediaInfo(v.FullServerPath, true);
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
                var subs = SubtitleHelper.GetSubtitleStreams(v.FullServerPath);
                if (subs.Count > 0)
                {
                    foreach (var s in subs)
                    {
                        s.Key = ConstructFileStream(userid, s.File);
                    }
                    m.Parts[0].Streams.AddRange(subs);
                }
                foreach (var p in m.Parts)
                {
                    p.Id = null;
                    var ff = Path.GetExtension(v.FullServerPath);
                    p.Key = ConstructVideoLocalStream(userid, v.VideoLocalID, ff);
                    p.Accessible = "1";
                    p.Exists = "1";
                    var vid = false;
                    var aud = false;
                    var txt = false;
                    foreach (var ss in p.Streams.ToArray())
                    {
                        if ((ss.StreamType == "1") && !vid)
                        {
                            vid = true;
                        }
                        if ((ss.StreamType == "2") && !aud)
                        {
                            aud = true;
                            ss.Selected = "1";
                        }
                        if ((ss.StreamType == "3") && !txt)
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
            var aep = ep.AniDB_Episode;
            if (aep != null)
            {
                v.EpNumber = aep.EpisodeNumber;
                v.Index = aep.EpisodeNumber.ToString();
                v.Title = aep.EnglishName;
                v.OriginalTitle = aep.RomajiName;
                v.Rating = float.Parse(aep.Rating, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
                if (aep.AirDateAsDate.HasValue)
                {
                    v.Year = aep.AirDateAsDate.Value.Year.ToString();
                    v.OriginallyAvailableAt = aep.AirDateAsDate.Value.ToPlexDate();
                }
                var epuser = ep.GetUserRecord(userid);
                if (epuser != null)
                {
                    v.ViewCount = epuser.WatchedCount.ToString();
                    if (epuser.WatchedDate.HasValue)
                        v.LastViewedAt = epuser.WatchedDate.Value.ToUnixTime();
                }
                var contract = new MetroContract_Anime_Episode();
                JMMServiceImplementationMetro.SetTvDBInfo(aep.AnimeID, aep, ref contract);
                v.Thumb = contract.GenPoster();
                v.Summary = contract.EpisodeOverview;
            }
        }


        private static bool PopulateVideoEpisodeFromAnime(Video v, AnimeEpisode ep, AnimeSeries ser,
            Contract_AnimeSeries cserie, AniDB_Anime ani, Video nv)
        {
            var ret = false;
            //            v.ParentTitle = "Season 1";
            //            v.GrandparentTitle = ser.GetSeriesName();
            v.ParentIndex = "1";
            v.Art = ani.GetDefaultFanartDetailsNoBlanks().GenArt();
            if (ep.EpisodeTypeEnum != enEpisodeType.Episode)
            {
                //v.ParentTitle = ep.ToString();
                v.ParentIndex = null;
            }
            else if (cserie.CrossRefAniDBTvDBV2 != null && cserie.CrossRefAniDBTvDBV2.Count > 0)
            {
                var c2 =
                    cserie.CrossRefAniDBTvDBV2.FirstOrDefault(
                        a =>
                            a.AniDBStartEpisodeType == ep.AniDB_Episode.EpisodeType &&
                            a.AniDBStartEpisodeNumber <= ep.AniDB_Episode.EpisodeNumber);
                if (c2 != null)
                {
                    //  v.ParentTitle = "Season " + c2.TvDBSeasonNumber;
                    if (c2.TvDBSeasonNumber > 0)
                        v.ParentIndex = c2.TvDBSeasonNumber.ToString();
                }
            }
            //v.Title1 = v.GrandparentTitle;
            //v.Title2 = v.ParentTitle;
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
                // else
                //   v.ParentTitle = nv.Title;
            }
            if (string.IsNullOrEmpty(v.Art))
                v.Art = nv.Art;
            if (v.Tags == null)
                v.Tags = nv.Tags;
            if (v.Genres == null)
                v.Genres = nv.Genres;
            if (v.Roles == null)
                v.Roles = nv.Roles;
            if (string.IsNullOrEmpty(v.Rating))
                v.Rating = nv.Rating;
            v.Index = ep.AniDB_Episode.EpisodeNumber.ToString();
            return ret;
        }

        public static bool PopulateVideo(Video l, VideoLocal v, JMMType type, int userid)
        {
            PopulateVideoEpisodeFromVideoLocal(l, v, type, userid);
            var eps = v.GetAnimeEpisodes();
            if (eps.Count > 0)
            {
                PopulateVideoEpisodeFromAnimeEpisode(l, eps[0], userid);
                var series = eps[0].GetAnimeSeries();
                if (series != null)
                {
                    var ani = series.GetAnime();
                    var cseries = series.ToContract(series.GetUserRecord(userid), true);
                    var nv = new Video();
                    FillSerie(nv, series, ani, cseries, userid);
                    return PopulateVideoEpisodeFromAnime(l, eps[0], series, cseries, ani, nv);
                }
            }
            return false;
        }

        public static bool PopulateVideo(Video l, VideoLocal v, AnimeEpisode ep, AnimeSeries series,
            Contract_AnimeSeries cseries, AniDB_Anime ani, Video nv, JMMType type, int userid)
        {
            PopulateVideoEpisodeFromVideoLocal(l, v, type, userid);
            if (ep != null)
            {
                PopulateVideoEpisodeFromAnimeEpisode(l, ep, userid);
                if (series != null)
                {
                    return PopulateVideoEpisodeFromAnime(l, ep, series, cseries, ani, nv);
                }
            }
            return false;
        }

        public static IEnumerable<T> Randomize<T>(this IEnumerable<T> source, int seed)
        {
            var rnd = new Random(seed);
            return source.OrderBy(item => rnd.Next());
        }

        public static string GetRandomFanartFromSeries(List<AnimeSeries> series, ISession session)
        {
            foreach (var ser in series.Randomize(123456789))
            {
                AniDB_Anime anim = ser.GetAnime(session);
                if (anim != null)
                {
                    var fanart = anim.GetDefaultFanartDetailsNoBlanks(session);
                    if (fanart != null)
                        return fanart.GenArt();
                }
            }
            return null;
        }

        public static string GetRandomFanartFromVideoList(List<Video> videos)
        {
            foreach (var v in videos.Randomize(123456789))
            {
                if (v.Art != null)
                    return v.Art;
            }
            return null;
        }

        public static Video VideoFromAnimeGroup(ISession session, AnimeGroup grp, int userid,
            List<AnimeSeries> allSeries)
        {
            var cgrp = grp.ToContract(grp.GetUserRecord(session, userid));
            if (StatsCache.Instance.StatGroupSeriesCount[grp.AnimeGroupID] == 1)
            {
                var ser = JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                if (ser != null)
                {
                    var cserie = ser.ToContract(ser.GetUserRecord(session, userid), true);
                    var v = FromSerieWithPossibleReplacement(cserie, ser, ser.GetAnime(session), userid);
                    v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                    v.Group = cgrp;
                    return v;
                }
            }
            else
            {
                var ser = grp.DefaultAnimeSeriesID.HasValue
                    ? allSeries.FirstOrDefault(a => a.AnimeSeriesID == grp.DefaultAnimeSeriesID.Value)
                    : JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                if (ser != null)
                {
                    var v = FromGroup(cgrp, ser.ToContract(ser.GetUserRecord(session, userid), true), userid);
                    v.Group = cgrp;
                    v.AirDate = cgrp.Stat_AirDate_Min.HasValue ? cgrp.Stat_AirDate_Min.Value : DateTime.MinValue;
                    return v;
                }
            }
            return null;
        }

        public static List<Video> ConvertToDirectoryIfNotUnique(List<Video> n)
        {
            if (n.Select(a => a.Type).Distinct().Count() > 1)
            {
                var ks = new List<Video>();
                foreach (var k in n)
                {
                    if (k is Video && !(k is Directory))
                    {
                        var m = new Directory();
                        k.CopyTo(m);
                        if ((m.Type == "movie") || (m.Type == "episode"))
                            m.Type = "show";
                        m.ParentThumb = m.GrandparentThumb = null;
                        if (m.Art == null)
                            m.Art = m.ParentArt;
                        ks.Add(m);
                    }
                    else
                    {
                        ks.Add(k);
                    }
                }
                n = ks;
            }

            return n;
        }

        public static Video MayReplaceVideo(Video v1, AnimeSeries ser, Contract_AnimeSeries cserie, AniDB_Anime anime,
            JMMType type, int userid, bool all = true)
        {
            var epcount = all
                ? ser.GetAnimeEpisodesCountWithVideoLocal()
                : ser.GetAnimeEpisodesNormalCountWithVideoLocal();
            if ((epcount == 1) && (anime.AnimeTypeEnum == enAnimeType.OVA || anime.AnimeTypeEnum == enAnimeType.Movie))
            {
                var episodes = ser.GetAnimeEpisodes();
                var l = episodes[0].GetVideoLocals();
                if (l.Count > 0)
                {
                    var v2 = new Video();
                    try
                    {
                        if (PopulateVideo(v2, l[0], episodes[0], ser, cserie, anime, v1, JMMType.File, userid))
                        {
                            v2.Thumb = anime.GetDefaultPosterDetailsNoBlanks().GenPoster();
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


        internal static Video FromGroup(Contract_AnimeGroup grp, Contract_AnimeSeries ser, int userid)
        {
            var p = new Directory();
            p.Key = ConstructGroupIdUrl(userid, grp.AnimeGroupID);
            p.Title = grp.GroupName;
            p.Summary = grp.Description;
            p.Type = "show";
            p.AirDate = grp.Stat_AirDate_Min.HasValue ? grp.Stat_AirDate_Min.Value : DateTime.MinValue;
            p.Thumb = ser.AniDBAnime?.DefaultImagePoster.GenPoster();
            p.Art = ser.AniDBAnime?.DefaultImageFanart.GenArt();
            p.LeafCount = (grp.UnwatchedEpisodeCount + grp.WatchedEpisodeCount).ToString();
            p.ViewedLeafCount = grp.WatchedEpisodeCount.ToString();
            return p;
        }

        public static Video FromSerieWithPossibleReplacement(Contract_AnimeSeries cserie, AnimeSeries ser,
            AniDB_Anime anidb, int userid)
        {
            Video v = new Directory();
            FillSerie(v, ser, anidb, cserie, userid);
            if (ser.GetAnimeNumberOfEpisodeTypes() > 1)
                v.Type = "show";
            else if ((cserie.AniDBAnime.AnimeType == (int)enAnimeType.Movie) ||
                     (cserie.AniDBAnime.AnimeType == (int)enAnimeType.OVA))
            {
                v = MayReplaceVideo(v, ser, cserie, ser.GetAnime(), JMMType.File, userid);
            }
            return v;
        }

        public static string SummaryFromAnimeContract(Contract_AnimeSeries c)
        {
            var s = c.AniDBAnime.Description;
            if (string.IsNullOrEmpty(s) && c.MovieDB_Movie != null)
                s = c.MovieDB_Movie.Overview;
            if (string.IsNullOrEmpty(s) && c.TvDB_Series != null && c.TvDB_Series.Count > 0)
                s = c.TvDB_Series[0].Overview;
            return s;
        }


        public static void FillSerie(Video p, AnimeSeries aser, AniDB_Anime anidb, Contract_AnimeSeries ser, int userid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var anime = ser.AniDBAnime;
                p.Key = ConstructSerieIdUrl(userid, ser.AnimeSeriesID.ToString());
                if (ser.AniDBAnime.Restricted > 0)
                    p.ContentRating = "R";
                p.Title = aser.GetSeriesName(session);
                p.Summary = SummaryFromAnimeContract(ser);
                p.Type = "show";
                p.AirDate = DateTime.MinValue;
                var textInfo = new CultureInfo("en-US", false).TextInfo;
                if (!string.IsNullOrEmpty(anime.AllTags))
                {
                    p.Genres = new List<Tag>();
                    anime.AllTags.Split('|')
                        .ToList()
                        .ForEach(a => p.Genres.Add(new Tag { Value = textInfo.ToTitleCase(a.Trim()) }));
                }
                //p.OriginalTitle
                if (anime.AirDate.HasValue)
                {
                    p.AirDate = anime.AirDate.Value;
                    p.OriginallyAvailableAt = anime.AirDate.Value.ToPlexDate();
                    p.Year = anime.AirDate.Value.Year.ToString();
                }
                p.LeafCount = anime.EpisodeCount.ToString();
                //p.ChildCount = p.LeafCount;
                p.ViewedLeafCount = ser.WatchedEpisodeCount.ToString();
                p.Rating = (anime.Rating / 100F).ToString(CultureInfo.InvariantCulture);
                var ls = ser.CrossRefAniDBTvDBV2;
                if (ls != null && ls.Count > 0)
                {
                    foreach (var c in ls)
                    {
                        if (c.TvDBSeasonNumber != 0)
                        {
                            p.Season = c.TvDBSeasonNumber.ToString();
                            p.Index = p.Season;
                        }
                    }
                }
                p.Thumb = anime.DefaultImagePoster.GenPoster();
                p.Art = anime.DefaultImageFanart.GenArt();
                var chars = anidb.GetAnimeCharacters(session);

                p.Roles = new List<Tag>();
                if (chars != null)
                {
                    foreach (var c in chars)
                    {
                        var cha = c.GetCharacter(session);
                        var seiyuu = cha?.GetSeiyuu(session);
                        if (seiyuu != null)
                        {
                            p.Roles.Add(new Tag
                            {
                                Value = seiyuu.SeiyuuName,
                                Role = cha.CharName
                            });
                        }
                    }
                }
            }
        }
    }
}