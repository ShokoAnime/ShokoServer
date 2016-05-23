using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel.Web;
using System.Text;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMContracts.PlexAndKodi;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.ImageDownload;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;
using Directory = JMMContracts.PlexAndKodi.Directory;

// ReSharper disable FunctionComplexityOverflow
namespace JMMServer.PlexAndKodi
{
    public class CommonImplementation 
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

        public System.IO.Stream GetFilters(IProvider prov, string uid)
        {
            int t = 0;
            int.TryParse(uid, out t);
            JMMUser user = t > 0 ? Helper.GetJMMUser(uid) : Helper.GetUser(uid);
            if (user==null)
                return new MemoryStream();
            int userid = user.JMMUserID;
            
            BreadCrumbs info = prov.UseBreadCrumbs ? new BreadCrumbs { Key = prov.ConstructFiltersUrl(userid), Title = "Anime" } : null;
            PlexObject ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Show, "Anime",false,false, info));
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
                        pp.Key =  prov.ConstructFilterIdUrl(userid, gg.GroupFilterID);
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
                            dirs.Add(prov, pp,info);
                        }
                    }
                    VideoLocalRepository repVids = new VideoLocalRepository();
                    List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
                    if (vids.Count > 0)
                    {
                        Directory pp = new Directory() { Type = "show" };
                        pp.Key = prov.ConstructUnsortUrl(userid);
                        pp.Title = "Unsort";
                        pp.Thumb = Helper.ConstructSupportImageLink("plex_unsort.png");
                        pp.LeafCount = vids.Count.ToString();
                        pp.ViewedLeafCount = "0";
                        dirs.Add(prov, pp, info);
                    }
                    var repPlaylist = new PlaylistRepository();
                    var playlists = repPlaylist.GetAll();
                    if (playlists.Count > 0)
                    {
                        Directory pp = new Directory() { Type="show" };
                        pp.Key = prov.ConstructPlaylistUrl(userid);
                        pp.Title = "Playlists";
                        pp.Thumb = Helper.ConstructSupportImageLink("plex_playlists.png");
                        pp.LeafCount = playlists.Count.ToString();
                        pp.ViewedLeafCount = "0";
                        dirs.Add(prov, pp, info);
                    }
                    dirs = dirs.OrderBy(a => a.Title).ToList();
                }
                ret.Childrens = dirs;
                if (prov.AddExtraItemForSearchButtonInGroupFilters)
                    ret.MediaContainer.Size = (int.Parse(ret.MediaContainer.Size) + 1).ToString(); //FIX to the added search item
                return ret.GetStream(prov);
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }
        }

        public System.IO.Stream GetMetadata(IProvider prov, string UserId, string TypeId, string Id, string historyinfo)
        {
            try
            {

                BreadCrumbs his = prov.UseBreadCrumbs ? BreadCrumbs.FromKey(historyinfo) : null;
                int type;
                int.TryParse(TypeId, out type);
                JMMUser user = Helper.GetJMMUser(UserId);

                switch ((JMMType) type)
                {
                    case JMMType.Group:
                        return GetItemsFromGroup(prov, user.JMMUserID, Id, his);
                    case JMMType.GroupFilter:
                        return GetGroupsFromFilter(prov, user.JMMUserID, Id, his);
                    case JMMType.GroupUnsort:
                        return GetUnsort(prov, user.JMMUserID, his);
                    case JMMType.Serie:
                        return GetItemsFromSerie(prov, user.JMMUserID, Id, his);
                    case JMMType.Episode:
                        return GetFromEpisode(prov, user.JMMUserID, Id, his);
                    case JMMType.File:
                        return GetFromFile(prov, user.JMMUserID, Id, his);
                    case JMMType.Playlist:
                        return GetItemsFromPlaylist(prov, user.JMMUserID, Id, his);
                    case JMMType.FakeIosThumb:
                        return FakeParentForIOSThumbnail(prov, user.JMMUserID, Id);
                }
                return new MemoryStream();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }

        }

        private System.IO.Stream GetItemsFromPlaylist(IProvider prov, int userid, string id, BreadCrumbs info)
        {
            var PlaylistID = -1;
            int.TryParse(id, out PlaylistID);
            var playlistRepository = new PlaylistRepository();
            var repo = new AnimeEpisodeRepository();
            if (PlaylistID == 0)
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Show,"Playlists", true, true, info));
                    if (!ret.Init())
                        return new MemoryStream();
                    var retPlaylists = new List<Video>();
                    var playlists = playlistRepository.GetAll();
                    foreach (var playlist in playlists)
                    {
                        var dir = new Directory();
                        dir.Key= prov.ConstructPlaylistIdUrl(userid, playlist.PlaylistID);
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
                            dir.Thumb = Helper.ConstructSupportImageLink("plex_404V.png");
                        }
                        dir.LeafCount = playlist.PlaylistItems.Split('|').Count().ToString();
                        dir.ViewedLeafCount = "0";
                        retPlaylists.Add(prov, dir,info);
                    }
                    retPlaylists = retPlaylists.OrderBy(a => a.Title).ToList();
                    ret.Childrens = retPlaylists;
                    return ret.GetStream(prov);
                }
            }
            if (PlaylistID > 0)
            {
                var playlist = playlistRepository.GetByID(PlaylistID);
                var playlistItems = playlist.PlaylistItems.Split('|');
                var vids = new List<Video>();
                var ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Episode, playlist.PlaylistName,true,true,info));
                if (!ret.Init())
                    return new MemoryStream();
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    foreach (var item in playlistItems)
                    {
                        try
                        {
                            var episodeID = -1;
                            int.TryParse(item.Split(';')[1], out episodeID);
                            if (episodeID < 0) return new MemoryStream();
                            List<Video> dirs = new List<Video>();
                            AnimeSeriesRepository serRepo = new AnimeSeriesRepository();
                            AnimeEpisode e = repo.GetByID(session, episodeID);
                            if (e == null)
                                return new MemoryStream();
                            KeyValuePair<AnimeEpisode, Contract_AnimeEpisode> ep = new KeyValuePair<AnimeEpisode, Contract_AnimeEpisode>(e, e.GetUserContract(userid));
                            if (ep.Value != null && ep.Value.LocalFileCount == 0)
                                continue;
                            AnimeSeries ser = serRepo.GetByID(session, ep.Key.AnimeSeriesID);
                            if (ser == null)
                                return new MemoryStream();
                            Contract_AnimeSeries con = ser.GetUserContract(userid);
                            if (con == null)
                                return new MemoryStream();
                            Video v = Helper.VideoFromAnimeEpisode(prov, con.CrossRefAniDBTvDBV2, ep, userid);
                            if (v.Medias != null && v.Medias.Count > 0)
                            {
                                Helper.AddInformationFromMasterSeries(v, con, ser.GetPlexContract(userid));
                                v.Type = "episode";
                                vids.Add(prov, v, info);
                                if (prov.ConstructFakeIosParent)
                                    v.GrandparentKey = prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v.ParentThumb));
                                v.ParentKey = null;
                            }
                        }
                        catch (Exception e)
                        {
                            //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                        }
                    }
                    ret.Childrens = vids;
                    return ret.GetStream(prov);
                }
            }
            return new MemoryStream();
        }

        private System.IO.Stream GetUnsort(IProvider prov, int userid, BreadCrumbs info)
        {
            PlexObject ret=new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Video, "Unsort", true,true, info));
            if (!ret.Init())
                return new MemoryStream();
            List<Video> dirs= new List<Video>();
            VideoLocalRepository repVids = new VideoLocalRepository();
            List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
            foreach (VideoLocal v in vids.OrderByDescending(a => a.DateTimeCreated))
            {
                try
                {
                    Video m = Helper.VideoFromVideoLocal(prov, v, userid);
                    dirs.Add(prov, m,info);
                    m.Thumb = Helper.ConstructSupportImageLink("plex_404.png");
                    m.ParentThumb = Helper.ConstructSupportImageLink("plex_unsort.png");
                    m.ParentKey = null;
                    if (prov.ConstructFakeIosParent)
                        m.GrandparentKey = prov.Proxyfy(prov.ConstructFakeIosThumb(userid, m.ParentThumb));
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }

            }
            ret.Childrens = dirs;
            return ret.GetStream(prov);
        }


        private System.IO.Stream GetFromFile(IProvider prov, int userid, string Id, BreadCrumbs info)
        {
            int id;
            if (!int.TryParse(Id, out id))
                return new MemoryStream(Encoding.UTF8.GetBytes(" "));
            VideoLocalRepository repVids = new VideoLocalRepository();
            VideoLocal vi = repVids.GetByID(id);
            PlexObject ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.File,Path.GetFileNameWithoutExtension(vi.FilePath ?? ""),true,false,info));
            Video v2 = Helper.VideoFromVideoLocal(prov, vi, userid);
            List<Video> dirs = new List<Video>();
            dirs.EppAdd(prov, v2, info, true);
            v2.Thumb = Helper.ConstructSupportImageLink("plex_404.png");
            v2.ParentThumb = Helper.ConstructSupportImageLink("plex_unsort.png");
            if (prov.ConstructFakeIosParent)
               v2.GrandparentKey = prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v2.ParentThumb));
            v2.ParentKey = null;
            if (prov.UseBreadCrumbs)
               v2.Key = ret.MediaContainer.Key;
            ret.MediaContainer.Childrens = dirs;
            return ret.GetStream(prov);

        }
        private System.IO.Stream GetFromEpisode(IProvider prov, int userid, string Id, BreadCrumbs info)
        {
            int id;
            if (!int.TryParse(Id, out id))
                return new MemoryStream();
            PlexObject ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Episode, "Episode",true, true, info));
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                List<Video> dirs = new List<Video>();
                AnimeEpisodeRepository epRepo = new AnimeEpisodeRepository();
                AnimeSeriesRepository serRepo = new AnimeSeriesRepository();

                AnimeEpisode e = epRepo.GetByID(session, id);
                if (e == null)
                    return new MemoryStream();
                KeyValuePair<AnimeEpisode,Contract_AnimeEpisode> ep=new KeyValuePair<AnimeEpisode, Contract_AnimeEpisode>(e,e.GetUserContract(userid));
                if (ep.Value!=null && ep.Value.LocalFileCount==0)
                    return new MemoryStream();
                AniDB_Episode aep = ep.Key.AniDB_Episode;
                if (aep == null)
                    return new MemoryStream();
                AnimeSeries ser = serRepo.GetByID(session, ep.Key.AnimeSeriesID);
                if (ser == null)
                    return new MemoryStream();
                AniDB_Anime anime = ser.GetAnime(session);
                Contract_AnimeSeries con = ser.GetUserContract(userid);
                if (con == null)
                    return new MemoryStream();
                try
                {
                    Video v = Helper.VideoFromAnimeEpisode(prov, con.CrossRefAniDBTvDBV2, ep, userid);
                    Helper.AddInformationFromMasterSeries(v,con, ser.GetPlexContract(userid));
                    v.Type = "episode";
                    if (v.Medias != null && v.Medias.Count > 0)
                    {
                        dirs.EppAdd(prov, v, info, true);
                        if (prov.ConstructFakeIosParent)
                            v.GrandparentKey = prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v.ParentThumb));
                        v.ParentKey = null;
                    }
                    if (prov.UseBreadCrumbs)
                        v.Key = ret.MediaContainer.Key;
                    ret.MediaContainer.Childrens = dirs;
                    return ret.GetStream(prov);
                }
                catch (Exception ex)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }
            return new MemoryStream();
        }





        public System.IO.Stream GetUsers(IProvider prov)
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


            return prov.GetStreamFromXmlObject(gfs);
        }

        public System.IO.Stream Search(IProvider prov, string UserId, string limit, string query, bool searchTag)
        {
            BreadCrumbs info = prov.UseBreadCrumbs ? new BreadCrumbs { Key = prov.ConstructSearchUrl(UserId,limit,query,searchTag), Title = "Search for '"+query+"'" } : null;

            PlexObject ret =new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Show, "Search for '" + query + "'",true,true,info));
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
            int lim;
            if (!int.TryParse(limit, out lim))
                lim = 100;
            JMMUser user = Helper.GetUser(UserId);
            if (user == null) return new MemoryStream();
            List<Video> ls=new List<Video>();
            int cnt = 0;
            List<AniDB_Anime> animes = searchTag ? repAnime.SearchByTag(query) : repAnime.SearchByName(query);
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

                        ls.Add(prov, v, info);
                    }
                    cnt++;
                    if (cnt == lim)
                        break;
                }
            }
            ret.MediaContainer.Childrens= Helper.ConvertToDirectory(ls);           
            return ret.GetStream(prov);
        }


       
        public System.IO.Stream GetItemsFromGroup(IProvider prov, int userid, string GroupId, BreadCrumbs info)
        {
            int groupID;
            int.TryParse(GroupId, out groupID);
            if (groupID == -1)
                return new MemoryStream();

            List<Video> retGroups = new List<Video>();
            AnimeGroupRepository repGroups = new AnimeGroupRepository();
            AnimeGroup grp = repGroups.GetByID(groupID);
            PlexObject ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Show, grp.GroupName,false,true, info));
            if (!ret.Init())
                return new MemoryStream();
            Contract_AnimeGroup basegrp = grp?.GetUserContract(userid);
            if (basegrp != null)
            {
                foreach (AnimeGroup grpChild in grp.GetChildGroups())
                {
                    var v = grpChild.GetPlexContract(userid);
                    if (v != null)
                    {
                        v.Type = "show";
                        v.Key = prov.ConstructGroupIdUrl(userid, grp.AnimeGroupID);
                        retGroups.Add(prov, v, info);
                    }
                }
                foreach (AnimeSeries ser in grp.GetSeries())
                {
                    var v = ser.GetPlexContract(userid)?.Clone();
                    if (v != null)
                    {
                        v.AirDate = ser.AirDate ?? DateTime.MinValue;
                        v.Group = basegrp;
                        v.Type = "show";
                        v.Key = prov.ConstructSerieIdUrl(userid, ser.AnimeSeriesID.ToString());
                        retGroups.Add(prov, v, info);
                    }
                }
            }
            ret.Childrens = Helper.ConvertToDirectory(retGroups.OrderBy(a => a.AirDate).ToList());
            return ret.GetStream(prov);
        }
        public System.IO.Stream ToggleWatchedStatusOnEpisode(IProvider prov, string userid, string episodeid, string watchedstatus)
        {
            Response rsp = new Response();
            rsp.Code = 400;
            rsp.Message = "Bad Request";
            try
            {
                int aep = 0;
                int usid = 0;
                bool wstatus = false;
                if (!int.TryParse(episodeid, out aep))
                    return prov.GetStreamFromXmlObject(rsp);
                if (!int.TryParse(userid, out usid))
                    return prov.GetStreamFromXmlObject(rsp);
                if (!bool.TryParse(watchedstatus, out wstatus))
                    return prov.GetStreamFromXmlObject(rsp);

                AnimeEpisodeRepository repEps = new AnimeEpisodeRepository();
                AnimeEpisode ep = repEps.GetByID(aep);
                if (ep == null)
                {
                    rsp.Code = 404;
                    rsp.Message = "Episode Not Found";
                    return prov.GetStreamFromXmlObject(rsp);
                }
                ep.ToggleWatchedStatus(wstatus, true, DateTime.Now, false, false, usid, true);
                ep.GetAnimeSeries().UpdateStats(true, false, true);
                rsp.Code = 200;
                rsp.Message = null;
            }
            catch (Exception ex)
            {
                rsp.Code = 500;
                rsp.Message = "Internal Error : "+ex;
                logger.ErrorException(ex.ToString(), ex);
            }
            return prov.GetStreamFromXmlObject(rsp);
        }

        public System.IO.Stream VoteAnime(IProvider prov, string userid, string objectid, string votevalue, string votetype)
        {
            Response rsp=new Response();
            rsp.Code = 400;
            rsp.Message = "Bad Request";
            try
            {
                int objid = 0;
                int usid = 0;
                int vt = 0;
                double vvalue = 0;
                if (!int.TryParse(objectid, out objid))
                    return prov.GetStreamFromXmlObject(rsp);
                if (!int.TryParse(userid, out usid))
                    return prov.GetStreamFromXmlObject(rsp);
                if (!int.TryParse(votetype, out vt))
                    return prov.GetStreamFromXmlObject(rsp);
                if (!double.TryParse(votevalue, NumberStyles.Any, CultureInfo.InvariantCulture, out vvalue))
                    return prov.GetStreamFromXmlObject(rsp);
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    if (vt == (int)enAniDBVoteType.Episode)
                    {
                        AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
                        AnimeEpisode ep = repEpisodes.GetByID(session, objid);
                        if (ep == null)
                        {
                            rsp.Code = 404;
                            rsp.Message = "Episode Not Found";
                            return prov.GetStreamFromXmlObject(rsp);
                        }
                        AniDB_Anime anime = ep.GetAnimeSeries().GetAnime();
                        if (anime == null)
                        {
                            rsp.Code = 404;
                            rsp.Message = "Anime Not Found";
                            return prov.GetStreamFromXmlObject(rsp);
                        }
                        string msg = string.Format("Voting for anime episode: {0} - Value: {1}", ep.AnimeEpisodeID, vvalue);
                        logger.Info(msg);

                        // lets save to the database and assume it will work
                        AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
                        List<AniDB_Vote> dbVotes = repVotes.GetByEntity(ep.AnimeEpisodeID);
                        AniDB_Vote thisVote = null;
                        foreach (AniDB_Vote dbVote in dbVotes)
                        {
                            if (dbVote.VoteType == (int)enAniDBVoteType.Episode)
                            {
                                thisVote = dbVote;
                            }
                        }

                        if (thisVote == null)
                        {
                            thisVote = new AniDB_Vote();
                            thisVote.EntityID = ep.AnimeEpisodeID;
                        }
                        thisVote.VoteType = vt;

                        int iVoteValue = 0;
                        if (vvalue > 0)
                            iVoteValue = (int)(vvalue * 100);
                        else
                            iVoteValue = (int)vvalue;

                        msg = string.Format("Voting for anime episode Formatted: {0} - Value: {1}", ep.AnimeEpisodeID, iVoteValue);
                        logger.Info(msg);
                        thisVote.VoteValue = iVoteValue;
                        repVotes.Save(thisVote);
                        CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(anime.AnimeID, vt, Convert.ToDecimal(vvalue));
                        cmdVote.Save();
                    }

                    if (vt == (int)enAniDBVoteType.Anime)
                    {
                        AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                        AnimeSeries ser = repSeries.GetByID(session, objid);
                        AniDB_Anime anime = ser.GetAnime();
                        if (anime == null)
                        {
                            rsp.Code = 404;
                            rsp.Message = "Anime Not Found";
                            return prov.GetStreamFromXmlObject(rsp);
                        }
                        string msg = string.Format("Voting for anime: {0} - Value: {1}", anime.AnimeID, vvalue);
                        logger.Info(msg);

                        // lets save to the database and assume it will work
                        AniDB_VoteRepository repVotes = new AniDB_VoteRepository();
                        List<AniDB_Vote> dbVotes = repVotes.GetByEntity(anime.AnimeID);
                        AniDB_Vote thisVote = null;
                        foreach (AniDB_Vote dbVote in dbVotes)
                        {
                            // we can only have anime permanent or anime temp but not both
                            if (vt == (int)enAniDBVoteType.Anime || vt == (int)enAniDBVoteType.AnimeTemp)
                            {
                                if (dbVote.VoteType == (int)enAniDBVoteType.Anime ||
                                    dbVote.VoteType == (int)enAniDBVoteType.AnimeTemp)
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
                            iVoteValue = (int)(vvalue * 100);
                        else
                            iVoteValue = (int)vvalue;

                        msg = string.Format("Voting for anime Formatted: {0} - Value: {1}", anime.AnimeID, iVoteValue);
                        logger.Info(msg);
                        thisVote.VoteValue = iVoteValue;
                        repVotes.Save(thisVote);
                        CommandRequest_VoteAnime cmdVote = new CommandRequest_VoteAnime(anime.AnimeID, vt, Convert.ToDecimal(vvalue));
                        cmdVote.Save();
                    }
                    rsp.Code = 200;
                    rsp.Message = null;
                }
            }
            catch (Exception ex)
            {
                rsp.Code = 500;
                rsp.Message = "Internal Error : " + ex;
                logger.ErrorException(ex.ToString(), ex);
            }
            return prov.GetStreamFromXmlObject(rsp);
            
        }



        public System.IO.Stream TraktScrobble(IProvider prov, string animeId, string type, string progress, string status)
        {
            Response rsp = new Response();
            rsp.Code = 400;
            rsp.Message = "Bad Request";
            try
            {
                int typeTrakt;
                int statusTrakt;
                Providers.TraktTV.ScrobblePlayingStatus statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                float progressTrakt;

                int.TryParse(status, out statusTrakt);

                switch (statusTrakt)
                {
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Start:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Start;
                        break;
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Pause:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Pause;
                        break;
                    case (int)Providers.TraktTV.ScrobblePlayingStatus.Stop:
                        statusTraktV2 = Providers.TraktTV.ScrobblePlayingStatus.Stop;
                        break;
                }

                float.TryParse(progress, out progressTrakt);
                progressTrakt = progressTrakt / 10;
                int.TryParse(type, out typeTrakt);
                switch (typeTrakt)
                {
                    //1
                    case (int)Providers.TraktTV.ScrobblePlayingType.movie:
                        rsp.Code = Providers.TraktTV.TraktTVHelper.Scrobble(Providers.TraktTV.ScrobblePlayingType.movie, animeId, statusTraktV2, progressTrakt);
                        rsp.Message = "Movie Scrobbled";
                        break;
                    //2
                    case (int)Providers.TraktTV.ScrobblePlayingType.episode:
                        rsp.Code = Providers.TraktTV.TraktTVHelper.Scrobble(Providers.TraktTV.ScrobblePlayingType.episode, animeId, statusTraktV2, progressTrakt);
                        rsp.Message = "Episode Scrobbled";
                        break;
                    //error
                }

            }
            catch (Exception ex)
            {
                rsp.Code = 500;
                rsp.Message = "Internal Error : " + ex;
                logger.ErrorException(ex.ToString(), ex);
            }
            return prov.GetStreamFromXmlObject(rsp);
        }

        private System.IO.Stream FakeParentForIOSThumbnail(IProvider prov, int userid, string url)
        {
            PlexObject ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.None,null,false,true,null));
            if (!ret.Init())
                return new MemoryStream();
            string rurl = Helper.Base64DecodeUrl(url);
            Directory v = new Directory() {Thumb = rurl, ParentThumb = rurl, GrandparentThumb = rurl};
            ret.MediaContainer.Thumb = ret.MediaContainer.ParentThumb = ret.MediaContainer.GrandparentThumb = rurl;
            List<Video> vids=new List<Video>();
            vids.Add(v);
            ret.Childrens = vids;
            return ret.GetStream(prov);
        }

        public System.IO.Stream GetItemsFromSerie(IProvider prov, int userid, string SerieId, BreadCrumbs info)
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
                Contract_AnimeSeries cseries = ser.GetUserContract(userid);
                if (cseries==null)
                    return new MemoryStream();
                Video nv = ser.GetPlexContract(userid);

       
                Dictionary<AnimeEpisode,Contract_AnimeEpisode> episodes = ser.GetAnimeEpisodes(session).ToDictionary(a=>a,a=>a.GetUserContract(userid));
                episodes=episodes.Where(a=>a.Value==null || a.Value.LocalFileCount>0).ToDictionary(a=>a.Key,a=>a.Value);
                if (eptype.HasValue)
                {
                    ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Episode,ser.GetSeriesName(),true,true,info));
                    if (!ret.Init())
                        return new MemoryStream();
                    ret.MediaContainer.LeafCount = (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount.ToString();
                    episodes = episodes.Where(a => a.Key.EpisodeTypeEnum == eptype.Value).ToDictionary(a=>a.Key,a=>a.Value);
                }
                else
                {
                    ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Show, "Types", false, true,info));
                    if (!ret.Init())
                        return new MemoryStream();

                    ret.MediaContainer.LeafCount = (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount.ToString();
                    List<enEpisodeType> types = episodes.Keys.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    if (types.Count > 1)
                    {
                        List<PlexEpisodeType> eps = new List<PlexEpisodeType>();
                        foreach (enEpisodeType ee in types)
                        {
                            PlexEpisodeType k2 = new PlexEpisodeType();
                            PlexEpisodeType.EpisodeTypeTranslated(k2, ee, (AnimeTypes)cseries.AniDBAnime.AnimeType, episodes.Count(a => a.Key.EpisodeTypeEnum == ee));
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
                            v.Art = nv.Art;
                            v.Title = ee.Name;
                            v.LeafCount = ee.Count.ToString();
                            v.ChildCount = v.LeafCount;
                            v.ViewedLeafCount = "0";
                            v.Key = prov.ConstructSerieIdUrl(userid, ee.Type + "_" + ser.AnimeSeriesID);
                            v.Thumb = Helper.ConstructSupportImageLink(ee.Image);
                            if ((ee.AnimeType==AnimeTypes.Movie) || (ee.AnimeType==AnimeTypes.OVA))
                            {
                                v = Helper.MayReplaceVideo(v, ser, cseries, userid, false,nv);
                            }
                            dirs.Add(prov,v,info,false,true);
                        }
                        ret.Childrens = dirs;
                        return ret.GetStream(prov);
                    }
                }
                List<Video> vids=new List<Video>();
                if (eptype.HasValue)
                {
                    info.ParentKey = info.GrandParentKey;
                }
                foreach (KeyValuePair<AnimeEpisode, Contract_AnimeEpisode> ep in episodes)
                {

                    try
                    {
                        Video v = Helper.VideoFromAnimeEpisode(prov, cseries.CrossRefAniDBTvDBV2, ep, userid);
                        if (v.Medias != null && v.Medias.Count > 0)
                        {
                            Helper.AddInformationFromMasterSeries(v,cseries, nv);
                            v.Type = "episode";
                            vids.Add(prov, v, info);
                            if (prov.ConstructFakeIosParent)
                                v.GrandparentKey = prov.Proxyfy(prov.ConstructFakeIosThumb(userid, v.ParentThumb));
                            v.ParentKey = null;
                        }
                    }
                    catch (Exception e)
                    {
                        //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                    }
                }

                List<SortPropOrFieldAndDirection> sortCriteria2 = new List<SortPropOrFieldAndDirection>();
                sortCriteria2.Add(new SortPropOrFieldAndDirection("EpisodeNumber", SortType.eInteger));
                vids= Sorting.MultiSort(vids, sortCriteria2);
                ret.Childrens = vids;                
                return ret.GetStream(prov);
            }
        }

        private System.IO.Stream GetGroupsFromFilter(IProvider prov, int userid, string GroupFilterId, BreadCrumbs info)
        {
            //List<Joint> retGroups = new List<Joint>();
            try
            {
                int groupFilterID ;
                int.TryParse(GroupFilterId, out groupFilterID);
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    List<Video> retGroups = new List<Video>();
                    if (groupFilterID == -1)
                        return new MemoryStream();
                    DateTime start = DateTime.Now;
                    GroupFilterRepository repGF = new GroupFilterRepository();

                    GroupFilter gf;

                    gf = repGF.GetByID(session, groupFilterID);
                    if (gf == null) return new MemoryStream();

                    PlexObject ret = new PlexObject(prov.NewMediaContainer(MediaContainerTypes.Show,gf.GroupFilterName,false,true, info));
                    if (!ret.Init())
                        return new MemoryStream();



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
                                v.Key = prov.ConstructGroupIdUrl(userid, grp.AnimeGroupID);
                                v.Type = "show";
                                retGroups.Add(prov, v, info);
                            }
                        }
                    }

                    if ((groupFilterID == -999) || (gf.SortCriteriaList.Count == 0))
                    {
                        ret.Childrens = Helper.ConvertToDirectory(retGroups.OrderBy(a => a.Group.SortName).ToList());
                        return ret.GetStream(prov);
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
                    ret.Childrens = Helper.ConvertToDirectory(joints2);
                    return ret.GetStream(prov);
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
