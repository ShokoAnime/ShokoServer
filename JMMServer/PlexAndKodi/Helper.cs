﻿using System;
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
using JMMContracts.PlexAndKodi;
using JMMFileHelper;
using JMMFileHelper.Subtitles;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Repositories;
using NHibernate;
using Directory = JMMContracts.PlexAndKodi.Directory;
using Stream = JMMContracts.PlexAndKodi.Stream;

namespace JMMServer.PlexAndKodi
{
    public static class Helper
    {

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
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetSupportImage/" + name);
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

        public static string ConstructCharacterImage(int id)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetImage/2/"+id);
        }
        public static string ConstructSeiyuuImage(int id)
        {
            return ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetImage/3/"+id);
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



        public static JMMUser GetUser(string userid)
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
                        if (m.Trim().ToLower() == userid.ToLower())
                            return n;
                    }
                }
            }
            return allusers.FirstOrDefault(a => a.IsAdmin == 1) ?? allusers.FirstOrDefault(a => a.Username == "Default") ?? allusers.First();
        }

        public static JMMUser GetJMMUser(string userid)
        {
            JMMUserRepository repUsers = new JMMUserRepository();
            List<JMMUser> allusers = repUsers.GetAll();
            int id = 0;
            int.TryParse(userid, out id);
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

        public static void AddLinksToAnimeEpisodeVideo(IProvider prov, Video v, int userid)
        {
            if (v.AnimeType==JMMContracts.PlexAndKodi.AnimeTypes.AnimeEpisode)
                v.Key = prov.ContructVideoUrl(userid, v.Id, JMMType.Episode);
            else if (v.Medias!=null && v.Medias.Count>0)
                v.Key= prov.ContructVideoUrl(userid, int.Parse(v.Medias[0].Id), JMMType.File);
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

        public static Video VideoFromVideoLocal(IProvider prov, VideoLocal v, int userid)
        {
            Video l=new Video();
            l.AnimeType=JMMContracts.PlexAndKodi.AnimeTypes.AnimeFile;
            l.Id = v.VideoLocalID;
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
            AddLinksToAnimeEpisodeVideo(prov, l,userid);
            return l;
        }


        public static Video VideoFromAnimeEpisode(IProvider prov, List<Contract_CrossRef_AniDB_TvDBV2> cross,  KeyValuePair<AnimeEpisode,Contract_AnimeEpisode> e, int userid)
        {
            Video v = (Video) e.Key.PlexContract?.Clone<Video>();
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
                v = (Video) e.Key.PlexContract?.Clone<Video>();
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
            AddLinksToAnimeEpisodeVideo(prov, v,userid);
            return v;
        }

        public static Video GenerateVideoFromAnimeEpisode(AnimeEpisode ep)
        {
            Video l=new Video();
            List<VideoLocal> vids = ep.GetVideoLocals();
            l.Type = "episode";
            l.Summary = "Episode Overview Not Available"; //TODO Intenationalization
            l.Id = ep.AnimeEpisodeID;
            l.AnimeType=JMMContracts.PlexAndKodi.AnimeTypes.AnimeEpisode;
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
                List<Stream> subs = SubtitleHelper.GetSubtitleStreams(v.FullServerPath);
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
                    foreach (Stream ss in p.Streams.ToArray())
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

        public static void AddInformationFromMasterSeries(Video v, Contract_AnimeSeries cserie, Video nv)
        {
            bool ret = false;
            v.Art = nv.Art;
            v.ParentThumb = v.GrandparentThumb = nv.Thumb;
            if (cserie.AniDBAnime.Restricted > 0)
                v.ContentRating = "R";
            if (cserie.AniDBAnime.AnimeType == (int)enAnimeType.Movie)
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
            else if (cserie.AniDBAnime.AnimeType == (int)enAnimeType.OVA)
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
       
             
        public static List<Video> ConvertToDirectory(List<Video> n)
        {
            List<Video> ks=new List<Video>();
            foreach (Video n1 in n)
            {
                Video m;
                if (n1 is Directory)
                    m = n1;
                else
                    m = n1.Clone<Directory>();
                m.ParentThumb = m.GrandparentThumb = null;
                ks.Add(m);
            }
            return ks;         
        }

        public static Video MayReplaceVideo(Video v1, AnimeSeries ser, Contract_AnimeSeries cserie, int userid, bool all=true, Video serie=null)
        {
            int epcount = all ? ser.GetAnimeEpisodesCountWithVideoLocal() :  ser.GetAnimeEpisodesNormalCountWithVideoLocal();
            if ((epcount == 1) && (cserie.AniDBAnime.AnimeType==(int)enAnimeType.OVA || cserie.AniDBAnime.AnimeType == (int)enAnimeType.Movie))
            {
                try
                {
                    List<AnimeEpisode> episodes = ser.GetAnimeEpisodes();
                    Video v2 = episodes[0].PlexContract;
                    if (v2.IsMovie)
                    {
                        AddInformationFromMasterSeries(v2, cserie, serie ?? v1);
                        v2.Thumb = (serie ?? v1).Thumb;
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


        private static Video FromGroup(Contract_AnimeGroup grp, Contract_AnimeSeries ser, int userid, int subgrpcnt)
        {
            Directory p = new Directory();
            p.Id = grp.AnimeGroupID;
            p.AnimeType = JMMContracts.PlexAndKodi.AnimeTypes.AnimeGroup;
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
                v =MayReplaceVideo(v, ser, cserie, userid);
            }
            return v;
        }

        private static string SummaryFromAnimeContract(Contract_AnimeSeries c)
        {
            string s = c.AniDBAnime.Description;
            if (string.IsNullOrEmpty(s) && c.MovieDB_Movie != null)
                s = c.MovieDB_Movie.Overview;
            if (string.IsNullOrEmpty(s) && c.TvDB_Series != null && c.TvDB_Series.Count > 0)
                s = c.TvDB_Series[0].Overview;
            return s;
        }

         
        private static void FillSerie(Video p ,AnimeSeries aser, Dictionary<AnimeEpisode, Contract_AnimeEpisode> eps, AniDB_Anime anidb, Contract_AnimeSeries ser, int userid)
        {
            using (ISession session = JMMService.SessionFactory.OpenSession())
            {
                Contract_AniDBAnime anime = ser.AniDBAnime;
                p.Id = ser.AnimeSeriesID;
                p.AnimeType = JMMContracts.PlexAndKodi.AnimeTypes.AnimeSerie;                
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
                p.Roles=new List<RoleTag>();

                //TODO Character implementation is limited in JMM, One Character, could have more than one Seiyuu
                if (anidb.Contract?.AniDBAnime?.Characters != null)
                {
                    foreach (Contract_AniDB_Character c in anidb.Contract.AniDBAnime.Characters)
                    {
                        string ch = c?.CharName;
                        Contract_AniDB_Seiyuu seiyuu = c?.Seiyuu;
                        if (!string.IsNullOrEmpty(ch))
                        {
                            RoleTag t = new RoleTag();
                            t.Value = seiyuu?.SeiyuuName;
                            if (seiyuu != null)
                                t.TagPicture = Helper.ConstructSeiyuuImage(seiyuu.AniDB_SeiyuuID);
                            t.Role = ch;
                            t.RoleDescription = c?.CharDescription;
                            t.RolePicture = Helper.ConstructCharacterImage(c.CharID);
                            p.Roles.Add(t);
                        }
                    }
                }
                p.Titles=new List<AnimeTitle>();
                foreach (AniDB_Anime_Title title in anidb.GetTitles())
                {
                    p.Titles.Add(new AnimeTitle {Language = title.Language, Title = title.Title, Type = title.TitleType});
                }
            }
        }
    }

}
