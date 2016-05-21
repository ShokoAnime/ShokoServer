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
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using NHibernate;
using Directory = JMMContracts.PlexContracts.Directory;
using Stream = JMMContracts.PlexContracts.Stream;

// ReSharper disable FunctionComplexityOverflow
namespace JMMServer.Plex
{
    public static class PlexHelper
    {
    
        public const string MediaTagVersion = "1461344894";


        public static MediaContainer NewMediaContainer(MediaContainerTypes type, Breadcrumbs info=null, bool allowsync=true, bool nocache = true)
        {
            MediaContainer m = new MediaContainer();
            m.AllowSync = allowsync ? "1" : "0";
            m.NoCache = nocache ? "1" : "0";
            m.MediaTagVersion = MediaTagVersion;
            m.Identifier = "com.plexapp.plugins.myanime";
            m.MediaTagPrefix = "/system/bundle/media/flags/";
            m.LibrarySectionTitle = "Anime";
            if (type!=MediaContainerTypes.None)
               info?.FillInfo(m,false, false);
            //m.GrandparentTitle = m.ParentTitle ?? "";
            //m.Title1 = m.Title2 = m.Title;
            //m.ParentTitle = "";
            //m.Title = null;
            m.Thumb = null;
            
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
                    m.ViewMode = "131132";
                    m.ViewGroup = "season";
                    break;
                case MediaContainerTypes.Movie:
                    m.ViewGroup = "movie";
                    m.ViewMode = "65592";
                    break;
                case MediaContainerTypes.File:
                    break;
            }
            return m;
        }

        public static string ConstructUnsortUrl(int userid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.GroupUnsort + "/0/");
        }
        public static string ConstructGroupIdUrl(int userid, int gid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Group + "/"+gid);
        }
        public static string ConstructSerieIdUrl(int userid, string sid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Serie + "/" + sid);
        }
               
        public static string ContructVideoUrl(int userid, int vid, JMMType type)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)type + "/" + vid);
        }
        public static string ConstructFilterIdUrl(int userid, int gfid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.GroupFilter + "/" + gfid);
        }

        public static string ConstructFakeIosThumb(int userid, string url)
        {
            string r=Base64EncodeUrl(url);

            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.FakeIosThumb+"/"+r+"/0");

        }
        public static string ConstructFiltersUrl(int userid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetFilters/" + userid);
        }
        public static string ConstructSearchUrl(string userid,string limit, string query)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/Search/" + WebUtility.UrlEncode(userid) +"/"+limit+"/"+WebUtility.UrlEncode(query));
        }
        public static string ConstructPlaylistUrl(int userid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/" + userid + "/" + (int)JMMType.Playlist + "/0");
        }

        public static string ConstructPlaylistIdUrl(int userid, int pid)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetMetadata/"+userid+"/" + (int)JMMType.Playlist + "/" +pid);
        }

        public static string ConstructVideoLocalStream(int userid, int vid, string extension)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerFilePort), "videolocal/" + userid + "/" + vid+ "/file" + extension, PlexObject.IsExternalRequest);
        }

        public static string ConstructFileStream(int userid, string file)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerFilePort), "file/" + userid + "/" + Base64EncodeUrl(file), PlexObject.IsExternalRequest);
        }

        public static string ConstructImageLink(int type, int id)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetImage/" + type + "/" + id);
        }
        public static string ConstructSupportImageLink(string name)
        {
            double relation = GetRelation();
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetSupportImage/"+name+ "/" + relation);
        }
        public static string ConstructSupportImageLinkTV(string name)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressPlex + "/GetSupportImage/" + name);
        }
        public static string ConstructThumbLink(int type, int id)
        {
            double relation = GetRelation();
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetThumb/" + type + "/" + id + "/"+relation);
        }
        public static string ConstructTVThumbLink(int type, int id)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetThumb/" + type + "/" + id + "/1.3333");
        }
        public static Dictionary<string,double> _relations=new Dictionary<string, double>();

        private static double GetRelation()
        {
            if (_relations.Count == 0)
            {
                
                string[] aspects = ServerSettings.PlexThumbnailAspects.Split(',');
                for (int x = 0; x < aspects.Length; x += 2)
                {
                    string key = aspects[x].Trim().ToUpper();
                    double val = 0.66667D;
                    double.TryParse(aspects[x + 1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out val);
                    _relations.Add(key, val);
                }
                if (!_relations.ContainsKey("DEFAULT"))
                {
                    _relations.Add("DEFAULT", 0.666667D);
                }
            }
            if (WebOperationContext.Current != null && WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
            {
                string kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                foreach (string n in _relations.Keys.Where(a=>a!="DEFAULT"))
                {
                    if (n!=null && kh.Contains(n))
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

        public static string Base64DecodeUrl(string url)
        {
            byte[] data = Convert.FromBase64String(url.Replace("-","+").Replace("_","/").Replace(",","="));
            return Encoding.UTF8.GetString(data);
        }
        public static string ToHex(string ka)
        {
            byte[] ba = Encoding.UTF8.GetBytes(ka);
            StringBuilder hex = new StringBuilder(ba.Length * 2);
            foreach (byte b in ba)
                hex.AppendFormat("{0:x2}", b);
            return hex.ToString();
        }
        public static string FromHex(string hex)
        {
            byte[] raw = new byte[hex.Length / 2];
            for (int i = 0; i < raw.Length; i++)
            {
                raw[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return Encoding.UTF8.GetString(raw);
        }
        public static string PlexProxy(string url)
        {
            return "/video/jmm/proxy/" + ToHex(url);
        }
        public static System.IO.Stream GetStreamFromXmlObject<T>(T obj)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(T));
            Utf8StringWriter textWriter = new Utf8StringWriter();
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
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

            JMMUserRepository repUsers = new JMMUserRepository();
            List <JMMUser> allusers=repUsers.GetAll();
            foreach (JMMUser n in allusers)
            {
                if (!string.IsNullOrEmpty(n.PlexUsers))
                {
                    string[] users = n.PlexUsers.Split(',');
                    foreach (string m in users)
                    {
                        if (m.Trim().ToLower() == UserId.ToLower())
                            return n;
                    }
                }
            }
            return allusers.FirstOrDefault(a => a.IsAdmin == 1) ?? allusers.FirstOrDefault(a => a.Username == "Default") ?? allusers.First();
        }

        public static JMMUser GetJMMUser(string UserId)
        {
            JMMUserRepository repUsers = new JMMUserRepository();
            List<JMMUser> allusers = repUsers.GetAll();
            int id = 0;
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
            return str.Replace("{SCHEME}", WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.Scheme).Replace("{HOST}", host);
        }

        public static bool RefreshIfMediaEmpty(VideoLocal vl, Video v)
        {
            if (v.Medias == null || v.Medias.Count == 0)
            {
                VideoLocalRepository lrepo=new VideoLocalRepository();
                lrepo.Save(vl,true);
                return true;
            }
            return false;
        }

        public static void AddLinksToAnimeEpisodeVideo(Video v, int userid)
        {
            if (v.Id!=0)
                v.Key = ContructVideoUrl(userid, v.Id, JMMType.Episode);
            else if (v.Medias!=null && v.Medias.Count>0)
                v.Key= ContructVideoUrl(userid, int.Parse(v.Medias[0].Id), JMMType.File);
            if (v.Medias != null)
            {
                foreach (Media m in v.Medias)
                {
                    if (m?.Parts != null)
                    {
                        foreach (Part p in m.Parts)
                        {
                            string ff = Path.GetExtension(p.Extension);
                            p.Key = ConstructVideoLocalStream(userid, int.Parse(m.Id), ff);
                            if (p.Streams != null)
                            {
                                foreach (Stream s in p.Streams.Where(a => a.File != null && a.StreamType == "3"))
                                {
                                    s.Key = ConstructFileStream(userid, s.File);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static Video VideoFromVideoLocal(VideoLocal v, int userid)
        {
            Video l=new Video();
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available"; //TODO Intenationalization
            l.Title = Path.GetFileNameWithoutExtension(v.FilePath);
            l.AddedAt = v.DateTimeCreated.ToUnixTime();
            l.UpdatedAt = v.DateTimeUpdated.ToUnixTime();
            l.OriginallyAvailableAt = v.DateTimeCreated.ToPlexDate();
            l.Year = v.DateTimeCreated.Year.ToString();
            l.Medias = new List<Media>();
            Media m = v.Media;
            if (m == null)
            {
                VideoLocalRepository lrepo = new VideoLocalRepository();
                lrepo.Save(v, true);
                m = v.Media;
            }
            if (m != null)
            {
                l.Medias.Add(m);
                l.Duration = m.Duration;
            }
            AddLinksToAnimeEpisodeVideo(l,userid);
            return l;
        }

        public static Video VideoFromAnimeEpisode(List<Contract_CrossRef_AniDB_TvDBV2> cross,  KeyValuePair<AnimeEpisode,Contract_AnimeEpisode> e, int userid)
        {
            Video v = (Video)e.Key.PlexContract?.DeepCopy();
            if (v?.Thumb != null)
                v.Thumb = ReplaceSchemeHost(v.Thumb);
            if (v!=null && (v.Medias == null || v.Medias.Count == 0))
            {
                List<VideoLocal> locals = e.Key.GetVideoLocals();
                if (locals.Count > 0)
                {
                    VideoLocalRepository lrepo = new VideoLocalRepository();
                    AnimeEpisodeRepository erepo=new AnimeEpisodeRepository();
                    foreach (VideoLocal n in locals)
                    {
                        lrepo.Save(n,false);
                    }
                    erepo.Save(e.Key);
                }
                v = (Video)e.Key.PlexContract?.DeepCopy();
            }
            if (e.Value != null)
            {
                v.ViewCount = e.Value.WatchedCount.ToString();
                if (e.Value.WatchedDate.HasValue)
                    v.LastViewedAt = e.Value.WatchedDate.Value.ToUnixTime();
            }
            v.ParentIndex = "1";
            if (e.Key.EpisodeTypeEnum != enEpisodeType.Episode)
            {
                v.ParentIndex = null;
            }
            if (cross != null && cross.Count > 0)
            {
                Contract_CrossRef_AniDB_TvDBV2 c2 =
                    cross.FirstOrDefault(
                        a =>
                            a.AniDBStartEpisodeType == v.EpisodeType &&
                            a.AniDBStartEpisodeNumber <= v.EpisodeNumber);
                if (c2?.TvDBSeasonNumber > 0)
                    v.ParentIndex = c2.TvDBSeasonNumber.ToString();
            }
            AddLinksToAnimeEpisodeVideo(v,userid);
            return v;
        }

        public static Video GenerateVideoFromAnimeEpisode(AnimeEpisode ep)
        {
            Video l=new Video();
            List<VideoLocal> vids = ep.GetVideoLocals();
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available"; //TODO Intenationalization
            if (vids.Count > 0)
            {
                l.Title = Path.GetFileNameWithoutExtension(vids[0].FilePath);
                l.AddedAt = vids[0].DateTimeCreated.ToUnixTime();
                l.UpdatedAt = vids[0].DateTimeUpdated.ToUnixTime();
                l.OriginallyAvailableAt = vids[0].DateTimeCreated.ToPlexDate();
                l.Year = vids[0].DateTimeCreated.Year.ToString();
                l.Medias = new List<Media>();
                foreach (VideoLocal v in vids)
                {
                    Media m = v.Media;
                    if (m != null)
                    {
                        l.Medias.Add(m);
                        l.Duration = m.Duration;
                    }
                }
            }
            AniDB_Episode aep = ep?.AniDB_Episode;
            if (aep != null)
            {
                l.EpisodeNumber = aep.EpisodeNumber;
                l.Index = aep.EpisodeNumber.ToString();
                l.Title = aep.EnglishName;
                l.OriginalTitle = aep.RomajiName;
                l.EpisodeType = aep.EpisodeType;
                l.Rating = (float.Parse(aep.Rating, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                if (aep.AirDateAsDate.HasValue)
                {
                    l.Year = aep.AirDateAsDate.Value.Year.ToString();
                    l.OriginallyAvailableAt = aep.AirDateAsDate.Value.ToPlexDate();
                }

                //FIX THIS
                MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
                JMMServiceImplementationMetro.SetTvDBInfo(aep.AnimeID, aep, ref contract);
                l.Thumb = contract.GenPoster();
                l.Summary = contract.EpisodeOverview;
            }
            l.Id = ep.AnimeEpisodeID;
            return l;
        }

        public static Media GenerateMediaFromVideoLocal(VideoLocal v)
        {
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
                    try
                    {
                        VideoInfoRepository repo = new VideoInfoRepository();
                        MediaInfoResult mInfo = FileHashHelper.GetMediaInfo(v.FullServerPath, true);
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
                    catch (Exception)
                    {
                        //FILE DO NOT EXIST
                    }
                }

            }
            if (m != null)
            {

                m.Id = v.VideoLocalID.ToString();
                List<JMMContracts.PlexContracts.Stream> subs = SubtitleHelper.GetSubtitleStreams(v.FullServerPath);
                if (subs.Count > 0)
                {
                    m.Parts[0].Streams.AddRange(subs);
                }
                foreach (Part p in m.Parts)
                {
                    p.Id = null;
                    p.Extension = Path.GetExtension(v.FullServerPath);
                    p.Accessible = "1";
                    p.Exists = "1";
                    bool vid = false;
                    bool aud = false;
                    bool txt = false;
                    foreach (JMMContracts.PlexContracts.Stream ss in p.Streams.ToArray())
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
            }
            return m;
        }

        /*
        private static void PopulateVideoEpisodeFromVideoLocals(Video l, List<VideoLocal> vids, AnimeEpisode ep, int userid)
        {
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available"; //TODO Intenationalization
            l.Title = Path.GetFileNameWithoutExtension(vids[0].FilePath);
            l.Key = ContructVideoUrl(userid, vids[0].VideoLocalID, JMMType.File);
            l.AddedAt = vids[0].DateTimeCreated.ToUnixTime();
            l.UpdatedAt = vids[0].DateTimeUpdated.ToUnixTime();
            l.OriginallyAvailableAt = vids[0].DateTimeCreated.ToPlexDate();
            l.Year = vids[0].DateTimeCreated.Year.ToString();
            l.Medias = new List<Media>();
            foreach (VideoLocal v in vids)
            {
                Media m = MediaFromUser(v, userid);
                if (m!=null)
                { 
                l.Medias.Add(m);
                l.Duration = m.Duration;
                }
            }
            AniDB_Episode aep = ep?.AniDB_Episode;
            if (aep != null)
            {
                l.Key = ContructVideoUrl(userid, ep.AnimeEpisodeID, JMMType.Episode);
                l.EpisodeNumber = aep.EpisodeNumber;
                l.Index = aep.EpisodeNumber.ToString();
                l.Title = aep.EnglishName;
                l.OriginalTitle = aep.RomajiName;
                l.Rating = (float.Parse(aep.Rating, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                if (aep.AirDateAsDate.HasValue)
                {
                    l.Year = aep.AirDateAsDate.Value.Year.ToString();
                    l.OriginallyAvailableAt = aep.AirDateAsDate.Value.ToPlexDate();
                }
                AnimeEpisode_User epuser = ep.GetUserRecord(userid);
                if (epuser != null)
                {
                    l.ViewCount = epuser.WatchedCount.ToString();
                    if (epuser.WatchedDate.HasValue)
                        l.LastViewedAt = epuser.WatchedDate.Value.ToUnixTime();
                }
                //FIX THIS
                MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
                JMMServiceImplementationMetro.SetTvDBInfo(aep.AnimeID, aep, ref contract);
                l.Thumb = contract.GenPoster();
                l.Summary = contract.EpisodeOverview;
            }
        }
        */
        /*
        private static void PopulateVideoEpisodeFromAnimeEpisode(Video v, AnimeEpisode ep, int userid)
        {
            AniDB_Episode aep = ep.AniDB_Episode;
            if (aep != null)
            {
                v.EpNumber = aep.EpisodeNumber;
                v.Index = aep.EpisodeNumber.ToString();
                v.Title = aep.EnglishName;
                v.OriginalTitle = aep.RomajiName;
                v.Rating = (float.Parse(aep.Rating, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
                if (aep.AirDateAsDate.HasValue)
                {
                    v.Year = aep.AirDateAsDate.Value.Year.ToString();
                    v.OriginallyAvailableAt = aep.AirDateAsDate.Value.ToPlexDate();
                }
                AnimeEpisode_User epuser = ep.GetUserRecord(userid);
                if (epuser != null)
                {
                    v.ViewCount = epuser.WatchedCount.ToString();
                    if (epuser.WatchedDate.HasValue)
                        v.LastViewedAt = epuser.WatchedDate.Value.ToUnixTime();
                }
                MetroContract_Anime_Episode contract = new MetroContract_Anime_Episode();
                JMMServiceImplementationMetro.SetTvDBInfo(aep.AnimeID, aep, ref contract);
                v.Thumb = contract.GenPoster();
                v.Summary = contract.EpisodeOverview;
            }

        }

    */
        public static void AddInformationFromMasterSeries(Video v, Contract_AnimeSeries cserie, AniDB_Anime ani, Video nv)
        {
            bool ret = false;
            if (ani != null)
            {
                v.Art = ani.GetDefaultFanartDetailsNoBlanks().GenArt();
                v.ParentThumb = v.GrandparentThumb = ani.GetDefaultPosterDetailsNoBlanks().GenArt();
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
            if (v.Thumb == null)
                v.Thumb = v.ParentThumb;
            v.IsMovie = ret;

        }
        /*
        public static bool PopulateVideo(Video l, List<VideoLocal> vids, int userid)
        {
            List<AnimeEpisode> eps = vids[0].GetAnimeEpisodes();
            AnimeEpisode ep = eps.FirstOrDefault();

            PopulateVideoEpisodeFromVideoLocals(l,vids,ep, userid);
            if (ep != null)
            {
                AnimeSeries series = eps[0].GetAnimeSeries();
                if (series != null)
                {
                    AniDB_Anime ani = series.GetAnime();
                    Contract_AnimeSeries cseries = series.GetUserContract(userid);
                    if (cseries != null)
                    {
                        Video nv = new Video();
                        FillSerie(nv, series, ani, cseries, userid);
                        return PopulateVideoEpisodeFromAnime(l, ep, cseries, ani, nv);
                    }
                }

            }
            else
            {
                l.Thumb = l.ParentThumb = l.GrandparentThumb = null;
                l.Art = l.ParentArt = l.GrandparentArt = null;
            }
            return false;
        }
        public static bool PopulateVideo(Video l, List<VideoLocal> vids, AnimeEpisode ep,  Contract_AnimeSeries cseries, AniDB_Anime ani, Video nv, int userid)
        {

            PopulateVideoEpisodeFromVideoLocals(l, vids, ep, userid);
            if (ep!=null)
            {
                if (cseries != null)
                {
                    return PopulateVideoEpisodeFromAnime(l,ep,cseries, ani, nv);
                }
            }
            return false;
        }
        */
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
        public static Video GenerateFromAnimeGroup(ISession session, AnimeGroup grp, int userid, List<AnimeSeries> allSeries)
        {
            Contract_AnimeGroup cgrp = grp.GetUserContract(userid);
            int subgrpcnt = grp.GetAllChildGroups().Count;

            if ((cgrp.Stat_SeriesCount == 1) && (subgrpcnt==0))
            {
                AnimeSeries ser = JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                if (ser != null)
                {
                    Contract_AnimeSeries cserie = ser.GetUserContract(userid);
                    if (cserie != null)
                    {
                        Video v = GenerateFromSeries(cserie, ser, ser.GetAnime(session), userid);
                        v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                        v.Group = cgrp;
                        return v;
                    }
                }
            }
            else
            {
                AnimeSeries ser = grp.DefaultAnimeSeriesID.HasValue ? allSeries.FirstOrDefault(a => a.AnimeSeriesID == grp.DefaultAnimeSeriesID.Value) : JMMServiceImplementation.GetSeriesForGroup(grp.AnimeGroupID, allSeries);
                Contract_AnimeSeries cserie = ser?.GetUserContract(userid);
                Video v = FromGroup(cgrp, cserie, userid,subgrpcnt);
                v.Group = cgrp;
                v.AirDate = cgrp.Stat_AirDate_Min.HasValue ? cgrp.Stat_AirDate_Min.Value : DateTime.MinValue;
                return v;
            }
            return null;
        }
       
             
        public static List<Video> ConvertToDirectoryIfNotUnique(List<Video> n)
        {
            List<Video> ks=new List<Video>();
            foreach (Video n1 in n)
            {
                Directory m = new Directory();
                n1.CopyTo(m);
                m.ParentThumb = m.GrandparentThumb = null;
                ks.Add(m);
            }
            return ks;
            /*
            if (n.Select(a => a.Type).Distinct().Count() > 1)
            {
                List<Video> ks = new List<Video>();
                foreach (Video k in n)
                {
                    if ((k is Video) && (!(k is Directory)))
                    {

                        Directory m = new Directory();
                        k.CopyTo(m);
                        if ((m.Type == "movie") || (m.Type=="episode"))
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

            return n;*/
        }

        public static Video MayReplaceVideo(Video v1, AnimeSeries ser, Contract_AnimeSeries cserie, AniDB_Anime anime, JMMType type, int userid, bool all=true)
        {
            int epcount = all ? ser.GetAnimeEpisodesCountWithVideoLocal() :  ser.GetAnimeEpisodesNormalCountWithVideoLocal();
            if ((epcount == 1) && (anime.AnimeTypeEnum==enAnimeType.OVA || anime.AnimeTypeEnum==enAnimeType.Movie))
            {
                try
                {
                    List<AnimeEpisode> episodes = ser.GetAnimeEpisodes();
                    Video v2 = episodes[0].PlexContract;
                    if (v2.IsMovie)
                    {
                        AddInformationFromMasterSeries(v2, cserie, anime, v1);
                        v2.Thumb = anime.GetDefaultPosterDetailsNoBlanks().GenPoster();
                        return v2;
                    }
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }
            return v1;
        }


        internal static Video FromGroup(Contract_AnimeGroup grp, Contract_AnimeSeries ser, int userid, int subgrpcnt)
        {
            Directory p = new Directory();
            p.Key = ConstructGroupIdUrl(userid, grp.AnimeGroupID); 
            p.Title = grp.GroupName;
            p.Summary = grp.Description;
            p.Type = "show";
            p.AirDate = grp.Stat_AirDate_Min.HasValue ? grp.Stat_AirDate_Min.Value : DateTime.MinValue;
            if (ser != null)
            {
                p.Thumb = ser.AniDBAnime?.DefaultImagePoster.GenPoster();
                p.Art = ser.AniDBAnime?.DefaultImageFanart.GenArt();
            }
            p.LeafCount = (grp.UnwatchedEpisodeCount + grp.WatchedEpisodeCount).ToString();
            p.ViewedLeafCount = grp.WatchedEpisodeCount.ToString();
            p.ChildCount = (grp.Stat_SeriesCount + subgrpcnt).ToString();
            if ((grp.UnwatchedEpisodeCount == 0) && (grp.WatchedDate.HasValue))
                p.LastViewedAt = grp.WatchedDate.Value.ToUnixTime();
            return p;
        }

        public static Video GenerateFromSeries(Contract_AnimeSeries cserie, AnimeSeries ser, AniDB_Anime anidb, int userid)
        {
            Video v = new Directory();
            Dictionary<AnimeEpisode, Contract_AnimeEpisode> episodes = ser.GetAnimeEpisodes().ToDictionary(a => a, a => a.GetUserContract(userid));
            episodes = episodes.Where(a => a.Value == null || a.Value.LocalFileCount > 0).ToDictionary(a => a.Key, a => a.Value);
            FillSerie(v,ser,episodes, anidb,cserie,userid);
            if (ser.GetAnimeNumberOfEpisodeTypes() > 1)
                v.Type = "show";
            else if ((cserie.AniDBAnime.AnimeType == (int)enAnimeType.Movie) || (cserie.AniDBAnime.AnimeType == (int)enAnimeType.OVA))
            {

                v =MayReplaceVideo(v, ser, cserie, ser.GetAnime(), JMMType.File, userid);
            }
            return v;
        }

        public static string SummaryFromAnimeContract(Contract_AnimeSeries c)
        {
            string s = c.AniDBAnime.Description;
            if (string.IsNullOrEmpty(s) && c.MovieDB_Movie != null)
                s = c.MovieDB_Movie.Overview;
            if (string.IsNullOrEmpty(s) && c.TvDB_Series != null && c.TvDB_Series.Count > 0)
                s = c.TvDB_Series[0].Overview;
            return s;
        }

         
        public static void FillSerie(Video p ,AnimeSeries aser, Dictionary<AnimeEpisode, Contract_AnimeEpisode> eps, AniDB_Anime anidb, Contract_AnimeSeries ser, int userid)
        {
            using (ISession session = JMMService.SessionFactory.OpenSession())
            {
                Contract_AniDBAnime anime = ser.AniDBAnime;
                p.Key = ConstructSerieIdUrl(userid, ser.AnimeSeriesID.ToString());
                if (ser.AniDBAnime.Restricted > 0)
                    p.ContentRating = "R";
                p.Title = aser.GetSeriesName(session);
                p.Summary = SummaryFromAnimeContract(ser);
                p.Type = "show";
                p.AirDate = DateTime.MinValue;
                TextInfo textInfo = new CultureInfo("en-US", false).TextInfo;
                if (!string.IsNullOrEmpty(anime.AllTags))
                {
                    p.Genres = new List<Tag>();
                    anime.AllTags.Split('|').ToList().ForEach(a => p.Genres.Add(new Tag {Value = textInfo.ToTitleCase(a.Trim())}));
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
                p.Rating = (anime.Rating/100F).ToString(CultureInfo.InvariantCulture);
                List<Contract_CrossRef_AniDB_TvDBV2> ls = ser.CrossRefAniDBTvDBV2;
                if (ls!=null && ls.Count > 0)
                {
                    foreach (Contract_CrossRef_AniDB_TvDBV2 c in ls)
                    {
                        if (c.TvDBSeasonNumber != 0)
                        {
                            p.Season = c.TvDBSeasonNumber.ToString();
                            p.Index = p.Season;
                        }
                    }
                }
                p.Thumb = p.ParentThumb = anime.DefaultImagePoster.GenPoster();
                p.Art = anime.DefaultImageFanart.GenArt();
                if (eps != null)
                {
                    List<enEpisodeType> types = eps.Keys.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    p.ChildCount = types.Count > 1 ? types.Count.ToString() : eps.Keys.Count.ToString();
                }
                p.Roles=new List<Tag>();
                if (anidb.Contract?.AniDBAnime?.Characters != null)
                {
                    foreach (Contract_AniDB_Character c in anidb.Contract.AniDBAnime.Characters)
                    {
                        string ch = c?.CharName;
                        string se = c?.Seiyuu?.SeiyuuName;
                        if (!string.IsNullOrEmpty(ch))
                        {
                            Tag t = new Tag {Value = se, Role = ch};
                            p.Roles.Add(t);
                        }
                    }
                }
            }
        }
    }

}
