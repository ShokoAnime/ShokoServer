using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel.Web;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMContracts.PlexContracts;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.Plex;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;
using Directory = JMMContracts.PlexContracts.Directory;
using Stream = System.IO.Stream;

// ReSharper disable FunctionComplexityOverflow

namespace JMMServer
{
    public class JMMServiceImplementationPlex : IJMMServerPlex
    {
        public static Logger logger = LogManager.GetCurrentClassLogger();


        public Stream GetSupportImage(string name)
        {
            if (string.IsNullOrEmpty(name))
                return new MemoryStream();
            name = Path.GetFileNameWithoutExtension(name);
            var man = Resources.ResourceManager;
            var dta = (byte[])man.GetObject(name);
            if ((dta == null) || (dta.Length == 0))
                return new MemoryStream();
            if (WebOperationContext.Current != null)
                WebOperationContext.Current.OutgoingResponse.ContentType = "image/png";
            var ms = new MemoryStream(dta);
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public Stream GetFilters(string uid)
        {
            var t = 0;
            int.TryParse(uid, out t);
            var user = t > 0 ? PlexHelper.GetJMMUser(uid) : PlexHelper.GetUser(uid);
            if (user == null)
                return new MemoryStream();
            var userid = user.JMMUserID;
            var info = new HistoryInfo { Key = PlexHelper.ConstructFiltersUrl(userid), Title = "Anime" };
            var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, false));
            if (!ret.Init())
                return new MemoryStream();
            var dirs = new List<Video>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var repGF = new GroupFilterRepository();
                    var allGfs = repGF.GetAll(session);
                    var gstats = StatsCache.Instance.StatUserGroupFilter[userid];
                    foreach (var gg in allGfs.ToArray())
                    {
                        if (!StatsCache.Instance.StatUserGroupFilter.ContainsKey(userid) ||
                            !StatsCache.Instance.StatUserGroupFilter[userid].ContainsKey(gg.GroupFilterID))
                        {
                            allGfs.Remove(gg);
                        }
                    }


                    var repGroups = new AnimeGroupRepository();
                    allGfs.Insert(0, new GroupFilter { GroupFilterName = "All", GroupFilterID = -999 });
                    foreach (var gg in allGfs)
                    {
                        var rnd = new Random(123456789);
                        var pp = new Directory { Type = "show" };
                        pp.Key = PlexHelper.ConstructFilterIdUrl(userid, gg.GroupFilterID);
                        pp.Title = gg.GroupFilterName;
                        HashSet<int> groups;
                        groups = gg.GroupFilterID == -999
                            ? new HashSet<int>(repGroups.GetAllTopLevelGroups(session).Select(a => a.AnimeGroupID))
                            : gstats[gg.GroupFilterID];
                        if (groups.Count != 0)
                        {
                            bool repeat;
                            var nn = 0;
                            pp.LeafCount = groups.Count.ToString();
                            pp.ViewedLeafCount = "0";
                            do
                            {
                                repeat = true;
                                var grp = groups.ElementAt(rnd.Next(groups.Count));
                                var ag = repGroups.GetByID(grp);
                                var sers = ag.GetSeries(session);
                                if (sers.Count > 0)
                                {
                                    var ser = sers[rnd.Next(sers.Count)];
                                    var anim = ser.GetAnime(session);
                                    if (anim != null)
                                    {
                                        var poster = anim.GetDefaultPosterDetailsNoBlanks(session);
                                        var fanart = anim.GetDefaultFanartDetailsNoBlanks(session);
                                        if (poster != null)
                                            pp.Thumb = poster.GenPoster();
                                        if (fanart != null)
                                            pp.Art = fanart.GenArt();
                                        if (poster != null)
                                            repeat = false;
                                    }
                                }
                                nn++;
                                if (repeat && (nn == 15))
                                    repeat = false;
                            } while (repeat);
                            dirs.Add(pp, info);
                        }
                    }
                    var repVids = new VideoLocalRepository();
                    var vids = repVids.GetVideosWithoutEpisode();
                    if (vids.Count > 0)
                    {
                        var pp = new Directory { Type = "show" };
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
                        var pp = new Directory { Type = "show" };
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
                return ret.GetStream();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }
        }


        public Stream GetMetadata(string UserId, string TypeId, string Id, string historyinfo)
        {
            try
            {
                var his = HistoryInfo.FromKey(historyinfo);
                if (WebOperationContext.Current != null)
                {
                    his.Key = WebOperationContext.Current.IncomingRequest.UriTemplateMatch.RequestUri.ToString();
                }
                int type;
                int.TryParse(TypeId, out type);
                var user = PlexHelper.GetJMMUser(UserId);
                switch ((JMMType)type)
                {
                    case JMMType.Group:
                        return GetItemsFromGroup(user.JMMUserID, Id, his);
                    case JMMType.GroupFilter:
                        return GetGroupsFromFilter(user.JMMUserID, Id, his);
                    case JMMType.GroupUnsort:
                        return GetUnsort(user.JMMUserID, his);
                    case JMMType.Serie:
                        return GetItemsFromSerie(user.JMMUserID, Id, his);
                    case JMMType.File:
                        return InternalGetFile(user.JMMUserID, Id, his);
                    case JMMType.Playlist:
                        return GetItemsFromPlaylist(user.JMMUserID, Id, his);
                }
                return new MemoryStream();
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
                return new MemoryStream();
            }
        }

        public Stream GetFile(string Id)
        {
            var user = PlexHelper.GetJMMUser("0");
            return InternalGetFile(user.JMMUserID, Id, new HistoryInfo());
        }

        public Stream GetUsers()
        {
            var gfs = new PlexContract_Users();
            try
            {
                gfs.Users = new List<PlexContract_User>();
                var repUsers = new JMMUserRepository();
                foreach (var us in repUsers.GetAll())
                {
                    var p = new PlexContract_User();
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

        public Stream Search(string UserId, string limit, string query)
        {
            var info = new HistoryInfo
            {
                Key = PlexHelper.ConstructSearchUrl(UserId, limit, query),
                Title = "Search for '" + query + "'"
            };

            var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, true));
            var repAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();
            int lim;
            if (!int.TryParse(limit, out lim))
                lim = 20;
            var user = PlexHelper.GetUser(UserId);
            if (user == null) return new MemoryStream();
            var ls = new List<Video>();
            var cnt = 0;
            var animes = repAnime.SearchByName(query);
            foreach (var anidb_anime in animes)
            {
                if (!user.AllowedAnime(anidb_anime)) continue;
                var ser = repSeries.GetByAnimeID(anidb_anime.AnimeID);
                if (ser != null)
                {
                    var cserie = ser.ToContract(ser.GetUserRecord(user.JMMUserID), true);
                    var v = PlexHelper.FromSerieWithPossibleReplacement(cserie, ser, anidb_anime, user.JMMUserID);
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
                    cnt++;
                    if (cnt == lim)
                        break;
                }
            }
            ret.MediaContainer.Childrens = PlexHelper.ConvertToDirectoryIfNotUnique(ls);
            return ret.GetStream();
        }

        public void ToggleWatchedStatusOnEpisode(string userid, string episodeid, string watchedstatus)
        {
            try
            {
                var aep = 0;
                var usid = 0;
                var wstatus = false;
                if (!int.TryParse(episodeid, out aep))
                    return;
                if (!int.TryParse(userid, out usid))
                    return;
                if (!bool.TryParse(watchedstatus, out wstatus))
                    return;

                var repEps = new AnimeEpisodeRepository();
                var ep = repEps.GetByID(aep);
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
            var serid = 0;
            var usid = 0;
            var vt = 0;
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
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByID(session, serid);
                var anime = ser.GetAnime();
                if (anime == null)
                    return;
                var msg = string.Format("Voting for anime: {0} - Value: {1}", anime.AnimeID, vvalue);
                logger.Info(msg);

                // lets save to the database and assume it will work
                var repVotes = new AniDB_VoteRepository();
                var dbVotes = repVotes.GetByEntity(anime.AnimeID);
                AniDB_Vote thisVote = null;
                foreach (var dbVote in dbVotes)
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

                var iVoteValue = 0;
                if (vvalue > 0)
                    iVoteValue = (int)(vvalue * 100);
                else
                    iVoteValue = (int)vvalue;

                msg = string.Format("Voting for anime Formatted: {0} - Value: {1}", anime.AnimeID, iVoteValue);
                logger.Info(msg);
                thisVote.VoteValue = iVoteValue;
                repVotes.Save(thisVote);
                var cmdVote = new CommandRequest_VoteAnime(anime.AnimeID, vt, Convert.ToDecimal(vvalue));
                cmdVote.Save();
            }
        }

        private Stream GetItemsFromPlaylist(int userid, string id, HistoryInfo info)
        {
            var PlaylistID = -1;
            int.TryParse(id, out PlaylistID);
            var playlistRepository = new PlaylistRepository();
            var repo = new AnimeEpisodeRepository();
            if (PlaylistID == 0)
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, false));
                    if (!ret.Init())
                        return new MemoryStream();
                    var retPlaylists = new List<Video>();
                    var playlists = playlistRepository.GetAll();
                    foreach (var playlist in playlists)
                    {
                        var dir = new Directory();
                        dir.Key = PlexHelper.ConstructPlaylistIdUrl(userid, playlist.PlaylistID);
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
                        retPlaylists.Add(dir, info);
                    }
                    retPlaylists = retPlaylists.OrderBy(a => a.Title).ToList();
                    ret.Childrens = retPlaylists;
                    return ret.GetStream();
                }
            }
            if (PlaylistID > 0)
            {
                //iOS Hack, since it uses the previous thumb, as overlay image on the episodes
                var iosHack = false;
                if (WebOperationContext.Current != null &&
                    WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
                {
                    var kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                    if (kh.Contains(" IOS"))
                        iosHack = true;
                }

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
                        var current = locals[0];
                        try
                        {
                            PlexHelper.PopulateVideo(v, current, JMMType.File, userid);
                            if (!string.IsNullOrEmpty(v.Duration))
                            {
                                vids.Add(v, info);
                                if (iosHack)
                                {
                                    v.Art = v.Thumb;
                                    v.Thumb = ret.MediaContainer.ParentThumb;
                                }
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

        private Stream GetUnsort(int userid, HistoryInfo info)
        {
            var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Video, info, true));
            if (!ret.Init())
                return new MemoryStream();
            var dirs = new List<Video>();
            var repVids = new VideoLocalRepository();
            var vids = repVids.GetVideosWithoutEpisode();
            foreach (var v in vids.OrderByDescending(a => a.DateTimeCreated))
            {
                var m = new Video();
                try
                {
                    PlexHelper.PopulateVideo(m, v, JMMType.File, userid);
                    m.GrandparentKey = null;
                    if (!string.IsNullOrEmpty(m.Duration))
                        dirs.Add(m, info);
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }
            ret.Childrens = dirs;
            return ret.GetStream();
        }

        private Stream InternalGetFile(int userid, string Id, HistoryInfo info)
        {
            int id;
            if (!int.TryParse(Id, out id))
                return new MemoryStream();
            var repVids = new VideoLocalRepository();
            var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.File, info, true));
            var vi = repVids.GetByID(id);
            if (vi == null)
                return new MemoryStream();
            var dirs = new List<Video>();
            var v2 = new Video();
            PlexHelper.PopulateVideo(v2, vi, JMMType.File, userid);
            dirs.Add(v2, info);
            ret.MediaContainer.Childrens = dirs;
            return ret.GetStream();
        }


        public Stream GetItemsFromGroup(int userid, string GroupId, HistoryInfo info)
        {
            var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, false));
            if (!ret.Init())
                return new MemoryStream();
            int groupID;
            int.TryParse(GroupId, out groupID);
            var retGroups = new List<Video>();
            if (groupID == -1)
                return new MemoryStream();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var repGroups = new AnimeGroupRepository();
                var grp = repGroups.GetByID(groupID);
                if (grp != null)
                {
                    var basegrp = grp.ToContract(grp.GetUserRecord(session, userid));
                    var sers2 = grp.GetSeries(session);
                    foreach (var grpChild in grp.GetChildGroups())
                    {
                        var v = StatsCache.Instance.StatPlexGroupsCache[userid][grpChild.AnimeGroupID];
                        if (v != null)
                        {
                            retGroups.Add(v.Clone(), info);
                        }
                    }
                    foreach (var ser in grp.GetSeries())
                    {
                        var cserie = ser.ToContract(ser.GetUserRecord(session, userid), true);
                        var v = PlexHelper.FromSerieWithPossibleReplacement(cserie, ser, ser.GetAnime(), userid);
                        v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                        v.Group = basegrp;
                        retGroups.Add(v, info);
                    }
                }
                ret.Childrens = PlexHelper.ConvertToDirectoryIfNotUnique(retGroups.OrderBy(a => a.AirDate).ToList());
                return ret.GetStream();
            }
        }


        public Stream GetItemsFromSerie(int userid, string SerieId, HistoryInfo info)
        {
            PlexObject ret = null;
            enEpisodeType? eptype = null;
            int serieID;
            if (SerieId.Contains("_"))
            {
                int ept;
                var ndata = SerieId.Split('_');
                if (!int.TryParse(ndata[0], out ept))
                    return new MemoryStream();
                eptype = (enEpisodeType)ept;
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
                var repSeries = new AnimeSeriesRepository();
                var ser = repSeries.GetByID(session, serieID);
                if (ser == null)
                    return new MemoryStream();
                var anime = ser.GetAnime();
                if (anime == null)
                    return new MemoryStream();
                var cseries = ser.ToContract(ser.GetUserRecord(userid), true);
                var fanart = anime.GetDefaultFanartDetailsNoBlanks(session);

                //iOS Hack, since it uses the previous thumb, as overlay image on the episodes
                var iosHack = false;
                if (WebOperationContext.Current != null &&
                    WebOperationContext.Current.IncomingRequest.Headers.AllKeys.Contains("X-Plex-Product"))
                {
                    var kh = WebOperationContext.Current.IncomingRequest.Headers.Get("X-Plex-Product").ToUpper();
                    if (kh.Contains(" IOS"))
                        iosHack = true;
                }

                var episodes = ser.GetAnimeEpisodes(session).Where(a => a.GetVideoLocals(session).Count > 0).ToList();
                if (eptype.HasValue)
                {
                    ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Episode, info, true));
                    if (!ret.Init())
                        return new MemoryStream();
                    ret.MediaContainer.LeafCount =
                        (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount.ToString();
                    episodes = episodes.Where(a => a.EpisodeTypeEnum == eptype.Value).ToList();
                }
                else
                {
                    ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, true));
                    if (!ret.Init())
                        return new MemoryStream();

                    ret.MediaContainer.LeafCount =
                        (cseries.WatchedEpisodeCount + cseries.UnwatchedEpisodeCount).ToString();
                    ret.MediaContainer.ViewedLeafCount = cseries.WatchedEpisodeCount.ToString();
                    var types = episodes.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    if (types.Count > 1)
                    {
                        var eps = new List<PlexEpisodeType>();
                        foreach (var ee in types)
                        {
                            var k2 = new PlexEpisodeType();
                            PlexEpisodeType.EpisodeTypeTranslated(k2, ee, (AnimeTypes)anime.AnimeType,
                                episodes.Count(a => a.EpisodeTypeEnum == ee));
                            eps.Add(k2);
                        }
                        var sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("Name", SortType.eString));
                        eps = Sorting.MultiSort(eps, sortCriteria);
                        var dirs = new List<Video>();
                        var converttoseason = true;

                        foreach (var ee in eps)
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
                            if ((ee.AnimeType == AnimeTypes.Movie) || (ee.AnimeType == AnimeTypes.OVA))
                            {
                                v = PlexHelper.MayReplaceVideo(v, ser, cseries, anime, JMMType.File, userid, false);
                            }
                            dirs.Add(v, info);
                            if (iosHack)
                            {
                                v.Thumb = ret.MediaContainer.ParentThumb;
                                v.ParentThumb = ret.MediaContainer.GrandparentThumb;
                                v.GrandparentThumb = ret.MediaContainer.GrandparentThumb;
                                v.ParentKey = v.GrandparentKey;
                            }
                        }
                        ret.Childrens = dirs;
                        return ret.GetStream();
                    }
                }
                var vids = new List<Video>();
                var nv = new Video();
                PlexHelper.FillSerie(nv, ser, anime, cseries, userid);
                foreach (var ep in episodes)
                {
                    var v = new Video();
                    var locals = ep.GetVideoLocals(session);
                    if ((locals == null) || (locals.Count == 0))
                        continue;
                    var aep = ep.AniDB_Episode;
                    if (aep == null)
                        continue;
                    var current = locals[0];
                    try
                    {
                        PlexHelper.PopulateVideo(v, current, ep, ser, cseries, anime, nv, JMMType.File, userid);
                        vids.Add(v, info);
                        if (iosHack)
                        {
                            v.Art = v.Thumb;
                            v.Thumb = ret.MediaContainer.ParentThumb;
                        }
                    }
                    catch (Exception e)
                    {
                        //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                    }
                }

                var sortCriteria2 = new List<SortPropOrFieldAndDirection>();
                sortCriteria2.Add(new SortPropOrFieldAndDirection("EpNumber", SortType.eInteger));
                vids = Sorting.MultiSort(vids, sortCriteria2);
                ret.Childrens = vids;

                return ret.GetStream();
            }
        }

        private Stream GetGroupsFromFilter(int userid, string GroupFilterId, HistoryInfo info)
        {
            var ret = new PlexObject(PlexHelper.NewMediaContainer(MediaContainerTypes.Show, info, false));
            if (!ret.Init())
                return new MemoryStream();

            //List<Joint> retGroups = new List<Joint>();
            var retGroups = new List<Video>();
            try
            {
                int groupFilterID;
                int.TryParse(GroupFilterId, out groupFilterID);
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    if (groupFilterID == -1)
                        return new MemoryStream();
                    var start = DateTime.Now;
                    var repGF = new GroupFilterRepository();

                    GroupFilter gf;

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
                    //Contract_GroupFilterExtended contract = gf.ToContractExtended(user);

                    var repGroups = new AnimeGroupRepository();
                    var allGrps = repGroups.GetAll(session);


                    var ts = DateTime.Now - start;
                    var msg = string.Format("Got groups for filter DB: {0} - {1} in {2} ms", gf.GroupFilterName,
                        allGrps.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    start = DateTime.Now;


                    if (StatsCache.Instance.StatUserGroupFilter.ContainsKey(userid) &&
                        StatsCache.Instance.StatUserGroupFilter[userid].ContainsKey(gf.GroupFilterID))
                    {
                        var groups = StatsCache.Instance.StatUserGroupFilter[userid][gf.GroupFilterID];

                        foreach (var grp in allGrps)
                        {
                            if (groups.Contains(grp.AnimeGroupID))
                            {
                                var v = StatsCache.Instance.StatPlexGroupsCache[userid][grp.AnimeGroupID];
                                if (v != null)
                                {
                                    v = v.Clone();

                                    retGroups.Add(v, info);
                                }
                            }
                        }
                    }
                    ts = DateTime.Now - start;
                    msg = string.Format("Got groups for filter EVAL: {0} - {1} in {2} ms", gf.GroupFilterName,
                        retGroups.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    if ((groupFilterID == -999) || (gf.SortCriteriaList.Count == 0))
                    {
                        ret.Childrens =
                            PlexHelper.ConvertToDirectoryIfNotUnique(retGroups.OrderBy(a => a.Group.SortName).ToList());
                        return ret.GetStream();
                    }
                    var grps = retGroups.Select(a => a.Group).ToList();
                    var sortCriteria = new List<SortPropOrFieldAndDirection>();
                    foreach (var g in gf.SortCriteriaList)
                    {
                        sortCriteria.Add(GroupFilterHelper.GetSortDescription(g.SortType, g.SortDirection));
                    }
                    grps = Sorting.MultiSort(grps, sortCriteria);
                    var joints2 = new List<Video>();
                    foreach (var gr in grps)
                    {
                        foreach (var j in retGroups)
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
                    ts = DateTime.Now - start;
                    msg = string.Format("Got groups final: {0} - {1} in {2} ms", gf.GroupFilterName,
                        retGroups.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
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