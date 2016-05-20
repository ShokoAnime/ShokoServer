using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Web;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMContracts.PlexContracts;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;
using JMMServer.Plex;
using Directory = JMMContracts.PlexContracts.Directory;

// ReSharper disable FunctionComplexityOverflow
namespace JMMServer
{
    public class JMMServiceImplementationPlex : IJMMServerPlex
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();
        

        public System.IO.Stream GetSupportImage(string name)
        {
            if (string.IsNullOrEmpty(name))
                return new MemoryStream();
            name = Path.GetFileNameWithoutExtension(name);
            System.Resources.ResourceManager man = Resources.ResourceManager;
            byte[] dta = (byte[])man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return new MemoryStream();
            if (WebOperationContext.Current!=null)
                WebOperationContext.Current.OutgoingResponse.ContentType = "image/png";
            MemoryStream ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public System.IO.Stream GetFilters(string uid)
        {
            int t = 0;
            int.TryParse(uid, out t);
            JMMUser user = t > 0 ? PlexHelper.GetJMMUser(uid) : PlexHelper.GetUser(uid);
            if (user==null)
                return new MemoryStream();
            int userid = user.JMMUserID;
            Breadcrumbs info = new Breadcrumbs { Key = PlexHelper.ConstructFiltersUrl(userid), Title = "Anime" };
            PlexObject ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show,info,false));
            if (!ret.Init())
                return new MemoryStream();
            List<Video> dirs = new List<Video>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    GroupFilterRepository repGF = new GroupFilterRepository();
                    List<GroupFilter> allGfs = repGF.GetAll(session);

                    foreach (GroupFilter gg in allGfs.ToArray())
                    {
                        if (!gg.GroupsIds.ContainsKey(userid))
                        {
                            allGfs.Remove(gg);
                        }
                    }


                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    foreach (GroupFilter gg in allGfs)
                    {

                        Random rnd = new Random(123456789);
                        Directory pp = new Directory { Type="show" };
                        pp.Key =  PlexHelper.ConstructFilterIdUrl(userid, gg.GroupFilterID);
                        pp.Title = gg.GroupFilterName;
                        HashSet<int> groups;
                        groups = gg.GroupsIds[userid];
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
                                List<AnimeSeries> sers = ag?.GetSeries(session);
                                if (sers?.Count > 0)
                                {
                                    AnimeSeries ser = sers[rnd.Next(sers.Count)];
                                    AniDB_Anime anim = ser.GetAnime(session);
                                    if (anim != null)
                                    {

                                        ImageDetails poster = anim.GetDefaultPosterDetailsNoBlanks(session);
                                        ImageDetails fanart = anim.GetDefaultFanartDetailsNoBlanks(session);
                                        if (poster != null)
                                            pp.Thumb = poster.GenPoster();
                                        if (fanart != null)
                                            pp.Art = fanart.GenArt();
                                        if (poster != null)
                                            repeat = false;
                                    }
                                }
                                nn++;
                                if ((repeat) && (nn == 15))
                                    repeat = false;

                            } while (repeat);
                            dirs.Add(pp,info);
                        }
                    }
                    VideoLocalRepository repVids = new VideoLocalRepository();
                    List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
                    if (vids.Count > 0)
                    {
                        Directory pp = new Directory() { Type = "show" };
                        pp.Key = PlexHelper.ConstructUnsortUrl(userid);
                        pp.Title = "Unsort";
                        pp.Thumb = PlexHelper.ConstructSupportImageLink("plex_unsort.png");
                        pp.LeafCount = vids.Count.ToString();
                        pp.ViewedLeafCount = "0";
                        dirs.Add(pp, info);
                    }
                    var repPlaylist = new PlaylistRepository();
                    var playlists = repPlaylist.GetAll();
                    if (playlists.Count > 0)
                    {
                        Directory pp = new Directory() { Type="show" };
                        pp.Key = PlexHelper.ConstructPlaylistUrl(userid);
                        pp.Title = "Playlists";
                        pp.Thumb = PlexHelper.ConstructSupportImageLink("plex_playlists.png");
                        pp.LeafCount = playlists.Count.ToString();
                        pp.ViewedLeafCount = "0";
                        dirs.Add(pp, info);
                    }
                    dirs = dirs.OrderBy(a => a.Title).ToList();
                }
                ret.Childrens = dirs;
                ret.MediaContainer.Size = (int.Parse(ret.MediaContainer.Size) + 1).ToString(); //FIX to the added search item
                return ret.GetStream();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }
        }



        public System.IO.Stream GetMetadata(string UserId, string TypeId, string Id, string historyinfo)
        {
            try
            {
                Breadcrumbs his = Breadcrumbs.FromKey(historyinfo);
                int type;
                int.TryParse(TypeId, out type);
                JMMUser user = PlexHelper.GetJMMUser(UserId);

                switch ((JMMType) type)
                {
                    case JMMType.Group:
                        return GetItemsFromGroup(user.JMMUserID, Id, his);
                    case JMMType.GroupFilter:
                        return GetGroupsFromFilter(user.JMMUserID, Id, his);
                    case JMMType.GroupUnsort:
                        return GetUnsort(user.JMMUserID, his);
                    case JMMType.Serie:
                        return GetItemsFromSerie(user.JMMUserID, Id, his);
                    case JMMType.Episode:
                        return GetFromEpisode(user.JMMUserID, Id, his);
                    case JMMType.File:
                        return GetFromFile(user.JMMUserID, Id, his);
                    case JMMType.Playlist:
                        return GetItemsFromPlaylist(user.JMMUserID, Id, his);
                    case JMMType.FakeIosThumb:
                        return FakeParentForIOSThumbnail(user.JMMUserID, Id);
                }
                return new MemoryStream();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }

        }

        private System.IO.Stream GetItemsFromPlaylist(int userid, string id, Breadcrumbs info)
        {
            var PlaylistID = -1;
            int.TryParse(id, out PlaylistID);
            var playlistRepository = new PlaylistRepository();
            var repo = new AnimeEpisodeRepository();
            if (PlaylistID == 0)
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show,info,false));
                    if (!ret.Init())
                        return new MemoryStream();
                    var retPlaylists = new List<Video>();
                    var playlists = playlistRepository.GetAll();
                    foreach (var playlist in playlists)
                    {
                        var dir = new Directory();
                        dir.Key= PlexHelper.ConstructPlaylistIdUrl(userid, playlist.PlaylistID);
                        dir.Title = playlist.PlaylistName;
                        var episodeID = -1;
                        if (int.TryParse(playlist.PlaylistItems.Split('|')[0].Split(';')[1], out episodeID))
                        {
                            var anime = repo.GetByID(session, episodeID).GetAnimeSeries(session).GetAnime(session);
                            dir.Thumb = anime.GetDefaultPosterDetailsNoBlanks(session).GenPoster();
                            dir.Art = anime.GetDefaultFanartDetailsNoBlanks(session).GenArt();
                        }
                        else
                        {
                            dir.Thumb = PlexHelper.ConstructSupportImageLink("plex_404V.png");
                        }
                        dir.LeafCount = playlist.PlaylistItems.Split('|').Count().ToString();
                        dir.ViewedLeafCount = "0";
                        retPlaylists.Add(dir,info);
                    }
                    retPlaylists = retPlaylists.OrderBy(a => a.Title).ToList();
                    ret.Childrens = retPlaylists;
                    return ret.GetStream();
                }
            }
            if (PlaylistID > 0)
            {
                //iOS Hack, since it uses the previous thumb, as overlay image on the episodes
                /*
                bool iosHack = false;
                if (WebOperationContext.Current != null && WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
                {
                    string kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                    if (kh.Contains(" IOS"))
                        iosHack = true;
                }
                */
                var playlist = playlistRepository.GetByID(PlaylistID);
                var playlistItems = playlist.PlaylistItems.Split('|');
                var vids = new List<Video>();
                var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Episode, info, true));
                if (!ret.Init())
                    return new MemoryStream();
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    foreach (var item in playlistItems)
                    {
                        var episodeID = -1;
                        int.TryParse(item.Split(';')[1], out episodeID);
                        if (episodeID < 0) return new MemoryStream();
                        var ep = repo.GetByID(session, episodeID);
                        var v = new Video();
                        var locals = ep.GetVideoLocals(session);
                        if ((locals == null) || (locals.Count == 0))
                            continue;
                        try
                        {
                            PlexHelper.PopulateVideo(v, locals, userid);
                            if (!string.IsNullOrEmpty(v.Duration))
                            {
                                vids.Add(v, info);
                                /*
                                if (iosHack)
                                {
                                    v.Art = v.Thumb;
                                    v.Thumb = ret.MediaContainer.ParentThumb;
                                }*/
                            }
                        }
                        catch (Exception e)
                        {
                            //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                        }
                    }
                    ret.Childrens = vids;
                    return ret.GetStream();
                }
            }
            return new MemoryStream();
        }

        private System.IO.Stream GetUnsort(int userid, Breadcrumbs info)
        {
            PlexObject ret=new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Video,info, true));
            if (!ret.Init())
                return new MemoryStream();
            List<Video> dirs= new List<Video>();
            VideoLocalRepository repVids = new VideoLocalRepository();
            List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
            foreach (VideoLocal v in vids.OrderByDescending(a => a.DateTimeCreated))
            {
                Video m = new Video();
                try
                {
                    PlexHelper.PopulateVideo(m, new List<VideoLocal>() { v}, userid);
                    if (!string.IsNullOrEmpty(m.Duration))
                        dirs.Add(m,info);
                    m.Thumb = PlexHelper.ConstructSupportImageLink("plex_404.png");
                    m.ParentThumb = PlexHelper.ConstructSupportImageLink("plex_unsort.png");
                    m.ParentKey = null;
                    m.GrandparentKey = PlexHelper.PlexProxy(PlexHelper.ConstructFakeIosThumb(userid, m.ParentThumb));
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }

            }
            ret.Childrens = dirs;
            return ret.GetStream();
        }

        public System.IO.Stream GetFile(string Id)
        {
            JMMUser user = PlexHelper.GetJMMUser("0");
            return GetFromFile(user.JMMUserID, Id, new Breadcrumbs());
        }

        private System.IO.Stream GetFromFile(int userid, string Id, Breadcrumbs info)
        {
                int id;
                if (!int.TryParse(Id, out id))
                    return new MemoryStream(Encoding.UTF8.GetBytes(" "));
                VideoLocalRepository repVids = new VideoLocalRepository();
                PlexObject ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.File, info, true));
                VideoLocal vi = repVids.GetByID(id);
                if (vi == null)
                    return new MemoryStream(Encoding.UTF8.GetBytes("  "));
                List<Video> dirs = new List<Video>();
                Video v2 = new Video();
                PlexHelper.PopulateVideo(v2, new List<VideoLocal> {  vi}, userid);
                dirs.EppAdd(v2, info, true);
                v2.Thumb = PlexHelper.ConstructSupportImageLink("plex_404.png");
                v2.ParentThumb = PlexHelper.ConstructSupportImageLink("plex_unsort.png");
                v2.GrandparentKey = PlexHelper.PlexProxy(PlexHelper.ConstructFakeIosThumb(userid, v2.ParentThumb));
                v2.ParentKey = null;
                v2.Key = ret.MediaContainer.Key;
                ret.MediaContainer.Childrens = dirs;
                return ret.GetStream();

        }
        private System.IO.Stream GetFromEpisode(int userid, string Id, Breadcrumbs info)
        {
            int id;
            if (!int.TryParse(Id, out id))
                return new MemoryStream();
            PlexObject ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Episode, info, true));
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                List<Video> dirs = new List<Video>();
                AnimeEpisodeRepository epRepo = new AnimeEpisodeRepository();
                AnimeSeriesRepository serRepo = new AnimeSeriesRepository();
                AnimeEpisode ep = epRepo.GetByID(session, id);
                if (ep == null)
                    return new MemoryStream();
                Video v = new Video();
                List<VideoLocal> locals = ep.GetVideoLocals(session);
                if ((locals == null) || (locals.Count == 0))
                    return new MemoryStream();
                AniDB_Episode aep = ep.AniDB_Episode;
                if (aep == null)
                    return new MemoryStream();
                AnimeSeries ser = serRepo.GetByID(session, ep.AnimeSeriesID);
                if (ser == null)
                    return new MemoryStream();
                AniDB_Anime anime = ser.GetAnime(session);
                if (anime == null)
                    return new MemoryStream();
                Contract_AnimeSeries con = ser.GetUserContract(userid);
                if (con == null)
                    return new MemoryStream();
                try
                {
                    Video nv = new Video();
                    PlexHelper.FillSerie(nv, ser, anime, con, userid);
                    PlexHelper.PopulateVideo(v, locals, ep, ser.GetUserContract(userid), anime, nv, userid);
                    v.Type = "episode";
                    dirs.EppAdd(v, info,true);
                    v.GrandparentKey = PlexHelper.PlexProxy(PlexHelper.ConstructFakeIosThumb(userid, v.ParentThumb));
                    v.ParentKey = null;
                    v.Key = ret.MediaContainer.Key;
                    ret.MediaContainer.Childrens = dirs;
                    return ret.GetStream();
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }
            return new MemoryStream();
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


            return PlexHelper.GetStreamFromXmlObject(gfs);
        }

        public System.IO.Stream Search(string UserId, string limit, string query)
        {
            Breadcrumbs info = new Breadcrumbs { Key = PlexHelper.ConstructSearchUrl(UserId,limit,query), Title = "Search for '"+query+"'" };

            PlexObject ret =new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show,info,true));
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
            int lim;
            if (!int.TryParse(limit, out lim))
                lim = 20;
            JMMUser user = PlexHelper.GetUser(UserId);
            if (user == null) return new MemoryStream();
            List<Video> ls=new List<Video>();
            int cnt = 0;
            List<AniDB_Anime> animes = repAnime.SearchByName(query);
            foreach (AniDB_Anime anidb_anime in animes)
            {
                if (!user.AllowedAnime(anidb_anime)) continue;
                AnimeSeries ser = repSeries.GetByAnimeID(anidb_anime.AnimeID);
                if (ser != null)
                {
                    Video v = ser.GetPlexContract(user.JMMUserID)?.Clone();
                    if (v != null)
                    {
                        switch (anidb_anime.AnimeTypeEnum)
                        {
                            case enAnimeType.Movie:
                                v.SourceTitle = "Anime Movies";
                                break;
                            case enAnimeType.OVA:
                                v.SourceTitle = "Anime Ovas";
                                break;
                            case enAnimeType.Other:
                                v.SourceTitle = "Anime Others";
                                break;
                            case enAnimeType.TVSeries:
                                v.SourceTitle = "Anime Series";
                                break;
                            case enAnimeType.TVSpecial:
                                v.SourceTitle = "Anime Specials";
                                break;
                            case enAnimeType.Web:
                                v.SourceTitle = "Anime Web Clips";
                                break;

                        }

                        ls.Add(v, info);
                    }
                    cnt++;
                    if (cnt == lim)
                        break;
                }
            }
            ret.MediaContainer.Childrens= PlexHelper.ConvertToDirectoryIfNotUnique(ls);
           
            return ret.GetStream();
        }


       
        public System.IO.Stream GetItemsFromGroup(int userid, string GroupId, Breadcrumbs info)
        {
            PlexObject ret=new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show,info,false));
            if (!ret.Init())
                return new MemoryStream();
            int groupID;
            int.TryParse(GroupId, out groupID);
            List<Video> retGroups = new List<Video>();
            if (groupID == -1)
                return new MemoryStream();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AnimeGroupRepository repGroups = new AnimeGroupRepository();
                AnimeGroup grp = repGroups.GetByID(groupID);
                Contract_AnimeGroup basegrp = grp?.GetUserContract(userid);
                if (basegrp != null)
                {
                    foreach (AnimeGroup grpChild in grp.GetChildGroups())
                    {
                        var v = grpChild.GetPlexContract(userid);
                        if (v != null)
                        {
                            v.Type = "show";
                            retGroups.Add(v, info);
                        }
                    }
                    foreach (AnimeSeries ser in grp.GetSeries())
                    {
                        var v = ser.GetPlexContract(userid)?.Clone();
                        if (v != null)
                        {
                            v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                            v.Group = basegrp;
                            v.Type = "show";
                            retGroups.Add(v, info);
                        }
                    }
                }
                ret.Childrens = PlexHelper.ConvertToDirectoryIfNotUnique(retGroups.OrderBy(a => a.AirDate).ToList());
                return ret.GetStream();
            }
        }
        public void ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus)
        {
            try
            {
                int aep = 0;
                int usid = 0;
                bool wstatus = false;
                if (!int.TryParse(episodeid, out aep))
                    return;
                if (!int.TryParse(userid, out usid))
                    return;
                if (!bool.TryParse(watchedstatus, out wstatus))
                    return;

                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                AnimeEpisode ep = repEps.GetByID(aep);
                if (ep == null)
                    return;

                ep.ToggleWatchedStatus(wstatus, true, DateTime.Now, false, false, usid, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public void VoteAnime(string userid, string seriesid, string votevalue, string votetype)
        {
            int serid = 0;
            int usid = 0;
            int vt = 0;
            double vvalue = 0;
            if (!int.TryParse(seriesid, out serid))
                return;
            if (!int.TryParse(userid, out usid))
                return;
            if (!int.TryParse(votetype, out vt))
                return;
            if (!double.TryParse(votevalue, NumberStyles.Any, CultureInfo.InvariantCulture, out vvalue))
                return;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                
                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                AnimeSeries ser = repSeries.GetByID(session, serid);
                AniDB_Anime anime = ser.GetAnime();
                if (anime == null)
                    return;
                string msg = string.Format("Voting for anime: {0} - Value: {1}", anime.AnimeID, vvalue);
                logger.Info(msg);

                // lets save to the database and assume it will work
                AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
                List<AniDB_Vote> dbVotes = repVotes.GetByEntity(anime.AnimeID);
                AniDB_Vote thisVote = null;
                foreach (AniDB_Vote dbVote in dbVotes)
                {
                    // we can only have anime permanent or anime temp but not both
                    if (vt == (int) enAniDBVoteType.Anime || vt == (int) enAniDBVoteType.AnimeTemp)
                    {
                        if (dbVote.VoteType == (int) enAniDBVoteType.Anime ||
                            dbVote.VoteType == (int) enAniDBVoteType.AnimeTemp)
                        {
                            thisVote = dbVote;
                        }
                    }
                    else
                    {
                        thisVote = dbVote;
                    }
                }

                if (thisVote == null)
                {
                    thisVote = new AniDB_Vote();
                    thisVote.EntityID = anime.AnimeID;
                }
                thisVote.VoteType = vt;

                int iVoteValue = 0;
                if (vvalue > 0)
                    iVoteValue = (int) (vvalue*100);
                else
                    iVoteValue = (int) vvalue;

                msg = string.Format("Voting for anime Formatted: {0} - Value: {1}", anime.AnimeID, iVoteValue);
                logger.Info(msg);
                thisVote.VoteValue = iVoteValue;
                repVotes.Save(thisVote);
                CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(anime.AnimeID, vt, Convert.ToDecimal(vvalue));
                cmdVote.Save();
            }
        }

        public System.IO.Stream FakeParentForIOSThumbnail(int userid, string url)
        {
            PlexObject ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.None,null, true));
            if (!ret.Init())
                return new MemoryStream();
            string rurl = PlexHelper.Base64DecodeUrl(url);
            Directory v = new Directory() {Thumb = rurl, ParentThumb = rurl, GrandparentThumb = rurl};
            ret.MediaContainer.Thumb = ret.MediaContainer.ParentThumb = ret.MediaContainer.GrandparentThumb = rurl;
            List<Video> vids=new List<Video>();
            vids.Add(v);
            ret.Childrens = vids;
            return ret.GetStream();
        }

        public System.IO.Stream GetItemsFromSerie(int userid, string SerieId,Breadcrumbs info)
        {
            PlexObject ret = null;
            enEpisodeType? eptype = null;
            int serieID ;
            if (SerieId.Contains("_"))
            {
                int ept ;
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
                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                AnimeSeries ser = repSeries.GetByID(session, serieID);
                if (ser == null)
                    return new MemoryStream();
                AniDB_Anime anime = ser.GetAnime();
                if (anime == null)
                    return new MemoryStream();
                Contract_AnimeSeries cseries = ser.GetUserContract(userid);
                if (cseries==null)
                    return new MemoryStream();
                ImageDetails fanart = anime.GetDefaultFanartDetailsNoBlanks(session);

                //iOS Hack, since it uses the previous thumb, as overlay image on the episodes
                //bool iosHack = false;
                /*
                if (WebOperationContext.Current != null && WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
                {
                    string kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                    if (kh.Contains(" IOS"))
                        iosHack = true;
                }
                */
                List<AnimeEpisode> episodes = ser.GetAnimeEpisodes(session).Where(a => a.GetVideoLocals(session).Count > 0).ToList();
                if (eptype.HasValue)
                {
                    ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Episode, info, true));
                    if (!ret.Init())
                        return new MemoryStream();
                    ret.MediaContainer.LeafCount = (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount.ToString();
                    episodes = episodes.Where(a => a.EpisodeTypeEnum == eptype.Value).ToList();
                }
                else
                {
                    ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, true));
                    if (!ret.Init())
                        return new MemoryStream();

                    ret.MediaContainer.LeafCount = (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount.ToString();
                    List<enEpisodeType> types = episodes.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    if (types.Count > 1)
                    {
                        List<PlexEpisodeType> eps = new List<PlexEpisodeType>();
                        foreach (enEpisodeType ee in types)
                        {
                            PlexEpisodeType k2 = new PlexEpisodeType();
                            PlexEpisodeType.EpisodeTypeTranslated(k2, ee, (AnimeTypes)anime.AnimeType, episodes.Count(a => a.EpisodeTypeEnum == ee));
                            eps.Add(k2);
                        }
                        List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("Name", SortType.eString));
                        eps = Sorting.MultiSort(eps, sortCriteria);
                        List<Video> dirs= new List<Video>();
                        //bool converttoseason = true;

                        foreach (PlexEpisodeType ee in  eps)
                        {
                            Video v = new Directory();
                            if (fanart != null)
                                v.Art = fanart.GenArt();
                            v.Title = ee.Name;
                            v.LeafCount = ee.Count.ToString();
                            v.ChildCount = v.LeafCount;
                            v.ViewedLeafCount = "0";
                            v.Key = PlexHelper.ConstructSerieIdUrl(userid, ee.Type + "_" + ser.AnimeSeriesID);
                            v.Thumb = PlexHelper.ConstructSupportImageLink(ee.Image);
                            if ((ee.AnimeType==AnimeTypes.Movie) || (ee.AnimeType==AnimeTypes.OVA))
                            {
                                v = PlexHelper.MayReplaceVideo(v, ser, cseries, anime,  JMMType.File, userid, false);
                            }
                            dirs.Add(v,info,false,true);
                            /*
                            if (iosHack)
                            {
                                v.Thumb = ret.MediaContainer.ParentThumb;
                                v.ParentThumb = ret.MediaContainer.GrandparentThumb;
                                v.GrandparentThumb = ret.MediaContainer.GrandparentThumb;
                                v.ParentKey = v.GrandparentKey;
                            }*/
                        }
                        ret.Childrens = dirs;
                        return ret.GetStream();
                    }
                }
                List<Video> vids=new List<Video>();
                Video nv = new Video();
                PlexHelper.FillSerie(nv,ser,anime, cseries, userid);
                if (eptype.HasValue)
                {
                    info.ParentKey = info.GrandParentKey;
                }
                foreach (AnimeEpisode ep in episodes)
                {

                    Video v = new Video();                   
                    List<VideoLocal> locals = ep.GetVideoLocals(session);
                    if ((locals == null) || (locals.Count == 0))
                        continue;
                    AniDB_Episode aep = ep.AniDB_Episode;
                    if (aep == null)
                        continue;
                    try
                    {
                        PlexHelper.PopulateVideo(v,locals, ep, cseries, anime, nv, userid);
                        v.Type = "episode";
                        vids.Add(v, info);
                        v.GrandparentKey = PlexHelper.PlexProxy(PlexHelper.ConstructFakeIosThumb(userid,v.ParentThumb));
                        v.ParentKey = null;

                    }
                    catch (Exception e)
                    {
                        //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                    }
                }

                List<SortPropOrFieldAndDirection> sortCriteria2 = new List<SortPropOrFieldAndDirection>();
                sortCriteria2.Add(new SortPropOrFieldAndDirection("EpNumber", SortType.eInteger));
                vids= Sorting.MultiSort(vids, sortCriteria2);
                ret.Childrens = vids;
                
                return ret.GetStream();
            }
        }

        private System.IO.Stream GetGroupsFromFilter(int userid, string GroupFilterId, Breadcrumbs info)
        {
            PlexObject ret=new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show,info,false));
            if (!ret.Init())
                return new MemoryStream();
           
            //List<Joint> retGroups = new List<Joint>();
            List<Video> retGroups=new List<Video>();
            try
            {
                int groupFilterID ;
                int.TryParse(GroupFilterId, out groupFilterID);
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    if (groupFilterID == -1)
                        return new MemoryStream();
                    DateTime start = DateTime.Now;
                    GroupFilterRepository repGF = new GroupFilterRepository();

                    GroupFilter gf;

                    gf = repGF.GetByID(session, groupFilterID);
                    if (gf == null) return new MemoryStream();
                    //Contract_GroupFilterExtended contract = gf.ToContractExtended(user);

                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    if (gf.GroupsIds.ContainsKey(userid))
                    {
                        foreach (AnimeGroup grp in gf.GroupsIds[userid].Select(a=>repGroups.GetByID(a)).Where(a=>a!=null))
                        {
                            Video v = grp.GetPlexContract(userid);
                            if (v != null)
                            {
                                v = v.Clone();
                                v.Type = "show";
                                retGroups.Add(v, info);
                            }
                        }
                    }

                    if ((groupFilterID == -999) || (gf.SortCriteriaList.Count == 0))
                    {
                        ret.Childrens = PlexHelper.ConvertToDirectoryIfNotUnique(retGroups.OrderBy(a => a.Group.SortName).ToList());
                        return ret.GetStream();
                    }
                    List<Contract_AnimeGroup> grps = retGroups.Select(a => a.Group).ToList();
                    List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                    foreach (GroupFilterSortingCriteria g in gf.SortCriteriaList)
                    {
                        sortCriteria.Add(GroupFilterHelper.GetSortDescription(g.SortType, g.SortDirection));
                    }
                    grps = Sorting.MultiSort(grps, sortCriteria);
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
                    ret.Childrens = PlexHelper.ConvertToDirectoryIfNotUnique(joints2);
                    return ret.GetStream();

                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return new MemoryStream();
        }

    }




}
