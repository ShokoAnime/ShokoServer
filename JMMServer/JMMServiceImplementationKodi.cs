using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.ServiceModel.Web;
using AniDBAPI;
using BinaryNorthwest;
using JMMContracts;
using JMMContracts.KodiContracts;
using JMMServer.Commands;
using JMMServer.Entities;
using JMMServer.Kodi;
using JMMServer.Properties;
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;
using NLog;
using Directory = JMMContracts.KodiContracts.Directory;
using Stream = System.IO.Stream;

// ReSharper disable FunctionComplexityOverflow

namespace JMMServer
{
    public class JMMServiceImplementationKodi : IJMMServerKodi
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

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
            var user = KodiHelper.GetUser(uid);
            if (user == null)
                return new MemoryStream();
            var userid = user.JMMUserID;
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Anime", false));
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
                        var pp = new Directory();
                        pp.Key = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                            MainWindow.PathAddressKodi + "/GetMetadata/" + userid + "/" +
                            (int)JMMType.GroupFilter + "/" + gg.GroupFilterID);
                        pp.PrimaryExtraKey = pp.Key;
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
                            dirs.Add(pp);
                        }
                    }
                    var repVids = new VideoLocalRepository();
                    var vids = repVids.GetVideosWithoutEpisode();
                    if (vids.Count > 0)
                    {
                        var pp = new Directory();
                        pp.Key = pp.PrimaryExtraKey = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                            MainWindow.PathAddressKodi + "/GetMetadata/0/" + (int)JMMType.GroupUnsort + "/0");
                        pp.Title = "Unsort";
                        pp.Thumb = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                            MainWindow.PathAddressKodi + "/GetSupportImage/plex_unsort.png");
                        pp.LeafCount = vids.Count.ToString();
                        pp.ViewedLeafCount = "0";
                        dirs.Add(pp);
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

        public Stream GetMetadata(string UserId, string TypeId, string Id)
        {
            try
            {
                int type;
                int.TryParse(TypeId, out type);
                var user = KodiHelper.GetUser(UserId);
                switch ((JMMType)type)
                {
                    case JMMType.Group:
                        return GetItemsFromGroup(user.JMMUserID, Id);
                    case JMMType.GroupFilter:
                        return GetGroupsFromFilter(user.JMMUserID, Id);
                    case JMMType.GroupUnsort:
                        return GetUnsort(user.JMMUserID);
                    case JMMType.Serie:
                        return GetItemsFromSerie(user.JMMUserID, Id);
                    case JMMType.File:
                        return InternalGetFile(user.JMMUserID, Id);
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
            var user = KodiHelper.GetUser("0");
            return InternalGetFile(user.JMMUserID, Id);
        }

        public Stream GetUsers()
        {
            var gfs = new KodiContract_Users();
            try
            {
                gfs.Users = new List<KodiContract_User>();
                var repUsers = new JMMUserRepository();
                foreach (var us in repUsers.GetAll())
                {
                    var p = new KodiContract_User();
                    p.id = us.JMMUserID.ToString();
                    p.name = us.Username;
                    gfs.Users.Add(p);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
            return KodiHelper.GetStreamFromXmlObject(gfs);
        }

        public Stream Search(string UserId, string limit, string query)
        {
            return Search(UserId, limit, query, false);
        }

        public Stream SearchTag(string UserId, string limit, string query)
        {
            return Search(UserId, limit, query, true);
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

        public Stream VoteAnime(string userid, string objectid, string votevalue, string votetype)
        {
            var rsp = new Respond();
            rsp.code = 500;

            var objid = 0;
            var usid = 0;
            var vt = 0;
            double vvalue = 0;
            if (!int.TryParse(objectid, out objid))
                return KodiHelper.GetStreamFromXmlObject(rsp);
            if (!int.TryParse(userid, out usid))
                return KodiHelper.GetStreamFromXmlObject(rsp);
            if (!int.TryParse(votetype, out vt))
                return KodiHelper.GetStreamFromXmlObject(rsp);
            if (!double.TryParse(votevalue, NumberStyles.Any, CultureInfo.InvariantCulture, out vvalue))
                return KodiHelper.GetStreamFromXmlObject(rsp);
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                if (vt == (int)enAniDBVoteType.Episode)
                {
                    var repEpisodes = new AnimeEpisodeRepository();
                    var ep = repEpisodes.GetByID(session, objid);
                    var anime = ep.GetAnimeSeries().GetAnime();
                    if (anime == null)
                    {
                        rsp.code = 404;
                        return KodiHelper.GetStreamFromXmlObject(rsp);
                    }
                    var msg = string.Format("Voting for anime episode: {0} - Value: {1}", ep.AnimeEpisodeID, vvalue);
                    logger.Info(msg);

                    // lets save to the database and assume it will work
                    var repVotes = new AniDB_VoteRepository();
                    var dbVotes = repVotes.GetByEntity(ep.AnimeEpisodeID);
                    AniDB_Vote thisVote = null;
                    foreach (var dbVote in dbVotes)
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

                    var iVoteValue = 0;
                    if (vvalue > 0)
                        iVoteValue = (int)(vvalue * 100);
                    else
                        iVoteValue = (int)vvalue;

                    msg = string.Format("Voting for anime episode Formatted: {0} - Value: {1}", ep.AnimeEpisodeID,
                        iVoteValue);
                    logger.Info(msg);
                    thisVote.VoteValue = iVoteValue;
                    repVotes.Save(thisVote);
                    var cmdVote = new CommandRequest_VoteAnime(anime.AnimeID, vt, Convert.ToDecimal(vvalue));
                    cmdVote.Save();
                }

                if (vt == (int)enAniDBVoteType.Anime)
                {
                    var repSeries = new AnimeSeriesRepository();
                    var ser = repSeries.GetByID(session, objid);
                    var anime = ser.GetAnime();
                    if (anime == null)
                    {
                        rsp.code = 404;
                        return KodiHelper.GetStreamFromXmlObject(rsp);
                    }
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
                rsp.code = 200;
                return KodiHelper.GetStreamFromXmlObject(rsp);
            }
        }

        public Stream TraktScrobble(string animeId, string type, string progress, string status)
        {
            var rsp = new Respond();

            int typeTrakt;
            int statusTrakt;
            var statusTraktV2 = ScrobblePlayingStatus.Start;
            float progressTrakt;

            int.TryParse(status, out statusTrakt);

            switch (statusTrakt)
            {
                case (int)ScrobblePlayingStatus.Start:
                    statusTraktV2 = ScrobblePlayingStatus.Start;
                    break;
                case (int)ScrobblePlayingStatus.Pause:
                    statusTraktV2 = ScrobblePlayingStatus.Pause;
                    break;
                case (int)ScrobblePlayingStatus.Stop:
                    statusTraktV2 = ScrobblePlayingStatus.Stop;
                    break;
            }

            float.TryParse(progress, out progressTrakt);
            progressTrakt = progressTrakt / 10;

            rsp.code = 404;

            int.TryParse(type, out typeTrakt);
            switch (typeTrakt)
            {
                //1
                case (int)ScrobblePlayingType.movie:
                    rsp.code = TraktTVHelper.Scrobble(ScrobblePlayingType.movie, animeId, statusTraktV2, progressTrakt);
                    break;
                //2
                case (int)ScrobblePlayingType.episode:
                    rsp.code = TraktTVHelper.Scrobble(ScrobblePlayingType.episode, animeId, statusTraktV2, progressTrakt);
                    break;
                //error
                default:
                    rsp.code = 500;
                    break;
            }

            return KodiHelper.GetStreamFromXmlObject(rsp);
        }

        private Stream GetUnsort(int userid)
        {
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Unsort", true));
            if (!ret.Init())
                return new MemoryStream();
            var dirs = new List<Video>();
            ret.MediaContainer.ViewMode = "65586";
            ret.MediaContainer.ViewGroup = "video";
            var repVids = new VideoLocalRepository();
            var vids = repVids.GetVideosWithoutEpisode();
            foreach (var v in vids.OrderByDescending(a => a.DateTimeCreated))
            {
                var m = new Video();
                try
                {
                    KodiHelper.PopulateVideo(m, v, JMMType.File, userid);
                    if (!string.IsNullOrEmpty(m.Duration))
                    {
                        dirs.Add(m);
                    }
                }
                catch (Exception e)
                {
                    //Fast fix if file do not exist, and still is in db. (Xml Serialization of video info will fail on null)
                }
            }
            ret.Childrens = dirs;
            return ret.GetStream();
        }

        private Stream InternalGetFile(int userid, string Id)
        {
            int id;
            if (!int.TryParse(Id, out id))
                return new MemoryStream();
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Unsort", true));
            if (!ret.Init())
                return new MemoryStream();
            var dirs = new List<Video>();
            var v = new Video();
            dirs.Add(v);
            var repVids = new VideoLocalRepository();
            var vi = repVids.GetByID(id);
            if (vi == null)
                return new MemoryStream();
            KodiHelper.PopulateVideo(v, vi, JMMType.File, userid);
            ret.Childrens = dirs;
            ret.MediaContainer.Art = v.Art;
            return ret.GetStream();
        }

        public Stream Search(string UserId, string limit, string query, bool searchTag)
        {
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Search", false));
            ret.MediaContainer.Title2 = "Search Results for '" + query + "'...";
            var repAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();

            int lim;
            if (!int.TryParse(limit, out lim))
                lim = 100;
            var user = KodiHelper.GetUser(UserId);
            if (user == null) return new MemoryStream();
            var ls = new List<Video>();
            var cnt = 0;
            List<AniDB_Anime> animes;
            if (searchTag)
            {
                animes = repAnime.SearchByTag(query);
            }
            else
            {
                animes = repAnime.SearchByName(query);
            }
            foreach (var anidb_anime in animes)
            {
                if (!user.AllowedAnime(anidb_anime)) continue;
                var ser = repSeries.GetByAnimeID(anidb_anime.AnimeID);
                if (ser != null)
                {
                    var cserie = ser.ToContract(ser.GetUserRecord(user.JMMUserID), true);
                    var v = KodiHelper.FromSerieWithPossibleReplacement(cserie, ser, user.JMMUserID);

                    //proper naming 
                    v.OriginalTitle = "";
                    foreach (var title in anidb_anime.GetTitles())
                    {
                        if (title.TitleType == "official" || title.TitleType == "main")
                        {
                            v.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" + title.Title + "|";
                        }
                    }
                    v.OriginalTitle = v.OriginalTitle.Substring(0, v.OriginalTitle.Length - 1);
                    //proper naming end

                    //experiment
                    var c = new Characters();
                    c.CharactersList = new List<Character>();
                    c.CharactersList = GetCharactersFromAniDB(anidb_anime);
                    v.CharactersList = new List<Characters>();
                    v.CharactersList.Add(c);
                    //experiment END

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

                    ls.Add(v);
                    cnt++;
                    if (cnt == lim)
                        break;
                }
            }
            ret.MediaContainer.Childrens = ls;
            return ret.GetStream();
        }

        public Stream GetItemsFromGroup(int userid, string GroupId)
        {
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Groups", true));
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
                    ret.MediaContainer.Title1 = ret.MediaContainer.Title2 = basegrp.GroupName;
                    var sers2 = grp.GetSeries(session);
                    ret.MediaContainer.Art = KodiHelper.GetRandomFanartFromSeries(sers2, session);
                    foreach (var grpChild in grp.GetChildGroups())
                    {
                        var v = StatsCache.Instance.StatKodiGroupsCache[userid][grpChild.AnimeGroupID];
                        v.Type = "show";
                        if (v != null)
                            retGroups.Add(v.Clone());
                    }
                    foreach (var ser in grp.GetSeries())
                    {
                        var cserie = ser.ToContract(ser.GetUserRecord(session, userid), true);
                        var v = KodiHelper.FromSerieWithPossibleReplacement(cserie, ser, userid);
                        v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                        v.Group = basegrp;
                        v.totalLocal = ser.GetAnimeEpisodesCountWithVideoLocal();
                        retGroups.Add(v);
                    }
                }
                ret.Childrens = retGroups.OrderBy(a => a.AirDate).ToList();
                return ret.GetStream();
            }
        }

        public Stream GetItemsFromSerie(int userid, string SerieId)
        {
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Series", true));
            if (!ret.Init())
                return new MemoryStream();
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

                var fanart = anime.GetDefaultFanartDetailsNoBlanks(session);
                if (fanart != null)
                    ret.MediaContainer.Art = fanart.GenArt();
                ret.MediaContainer.Title2 = ret.MediaContainer.Title1 = anime.MainTitle;
                var episodes = ser.GetAnimeEpisodes(session).Where(a => a.GetVideoLocals(session).Count > 0).ToList();
                if (eptype.HasValue)
                {
                    episodes = episodes.Where(a => a.EpisodeTypeEnum == eptype.Value).ToList();
                }
                else
                {
                    var types = episodes.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    if (types.Count > 1)
                    {
                        var eps = new List<KodiEpisodeType>();
                        foreach (var ee in types)
                        {
                            var k2 = new KodiEpisodeType();
                            KodiEpisodeType.EpisodeTypeTranslated(k2, ee, (AnimeTypes)anime.AnimeType,
                                episodes.Count(a => a.EpisodeTypeEnum == ee));
                            eps.Add(k2);
                        }
                        var sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("Name", SortType.eString));
                        eps = Sorting.MultiSort(eps, sortCriteria);
                        var dirs = new List<Video>();

                        var isCharacterSetup_ = false;

                        foreach (var ee in eps)
                        {
                            Video v = new Directory();
                            v.Title = ee.Name;
                            v.Type = "season";
                            v.LeafCount = ee.Count.ToString();
                            v.ViewedLeafCount = "0";
                            v.Key = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                MainWindow.PathAddressKodi + "/GetMetadata/" + userid + "/" + (int)JMMType.Serie + "/" +
                                ee.Type + "_" + ser.AnimeSeriesID);
                            v.Thumb = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                MainWindow.PathAddressKodi + "/GetSupportImage/" + ee.Image);
                            if ((ee.AnimeType == AnimeTypes.Movie) || (ee.AnimeType == AnimeTypes.OVA))
                            {
                                v = KodiHelper.MayReplaceVideo((Directory)v, ser, anime, JMMType.File, userid, false);
                            }

                            //proper naming 
                            v.OriginalTitle = "";
                            foreach (var title in anime.GetTitles())
                            {
                                if (title.TitleType == "official" || title.TitleType == "main")
                                {
                                    v.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" + title.Title +
                                                       "|";
                                }
                            }
                            v.OriginalTitle = v.OriginalTitle.Substring(0, v.OriginalTitle.Length - 1);
                            //proper naming end

                            //experiment
                            if (!isCharacterSetup_)
                            {
                                var ch = new Characters();
                                ch.CharactersList = new List<Character>();
                                ch.CharactersList = GetCharactersFromAniDB(anime);
                                v.CharactersList = new List<Characters>();
                                v.CharactersList.Add(ch);
                                isCharacterSetup_ = true;
                            }
                            //experimentEND

                            dirs.Add(v);
                        }
                        ret.Childrens = dirs;
                        return ret.GetStream();
                    }
                }
                var vids = new List<Video>();
                var cseries = ser.ToContract(ser.GetUserRecord(userid), true);
                Video nv = KodiHelper.FromSerie(cseries, userid);
                var k = new KodiEpisodeType();
                if (eptype.HasValue)
                {
                    KodiEpisodeType.EpisodeTypeTranslated(k, eptype.Value, (AnimeTypes)anime.AnimeType,
                        episodes.Count);
                }

                var isCharacterSetup = false;

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
                        KodiHelper.PopulateVideo(v, current, ep, ser, anime, nv, JMMType.File, userid);
                        if (eptype.HasValue)
                        {
                            v.ParentTitle = k.Name;
                        }

                        //experiment
                        if (!isCharacterSetup)
                        {
                            var c = new Characters();
                            c.CharactersList = new List<Character>();
                            c.CharactersList = GetCharactersFromAniDB(anime);
                            v.CharactersList = new List<Characters>();
                            v.CharactersList.Add(c);
                            isCharacterSetup = true;
                        }
                        //experimentEND

                        vids.Add(v);
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

        private Stream GetGroupsFromFilter(int userid, string GroupFilterId)
        {
            var ret = new KodiObject(KodiHelper.NewMediaContainer("Filters", true));
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
                    ret.MediaContainer.Title2 = ret.MediaContainer.Title1 = gf.GroupFilterName;
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
                        var tas = StatsCache.Instance.StatKodiGroupsCache;
                        foreach (var grp in allGrps)
                        {
                            if (groups.Contains(grp.AnimeGroupID))
                            {
                                try
                                {
                                    //if (grp.GroupName == "Rockman.EXE")
                                    //{
                                    //    int x = grp.MissingEpisodeCount;
                                    //}
                                    var v = StatsCache.Instance.StatKodiGroupsCache[userid][grp.AnimeGroupID];
                                    if (v != null)
                                    {
                                        //proper naming
                                        var anim = grp.Anime[0];
                                        v.OriginalTitle = "";
                                        foreach (var title in anim.GetTitles())
                                        {
                                            if (title.TitleType == "official" || title.TitleType == "main")
                                            {
                                                v.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" +
                                                                   title.Title + "|";
                                            }
                                        }
                                        v.OriginalTitle = v.OriginalTitle.Substring(0, v.OriginalTitle.Length - 1);
                                        //proper naming end
                                        var sers = grp.GetAllSeries();
                                        var ser = sers[0];
                                        v.totalLocal = ser.GetAnimeEpisodesCountWithVideoLocal();
                                        if (!string.IsNullOrEmpty(anim.AllTags))
                                        {
                                            v.Tags = new List<Tag> { new Tag { Value = anim.AllTags.Replace("|", ",") } };
                                        }
                                        v.Rating = (anim.Rating / 100F).ToString(CultureInfo.InvariantCulture);
                                        v.Year = "" + anim.BeginYear;

                                        retGroups.Add(v.Clone());
                                    }
                                }
                                catch (Exception e)
                                {
                                    var x = retGroups.Count;
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
                        ret.Childrens = retGroups.OrderBy(a => a.Group.SortName).ToList();
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
                                //experiment
                                var repAnime = new AniDB_AnimeRepository();
                                var repSeries = new AnimeSeriesRepository();
                                var ag = repGroups.GetByID(gr.AnimeGroupID);
                                var sers = ag.GetAllSeries();
                                var ser = sers[0];
                                var anim = ser.GetAnime();

                                j.CharactersList = new List<Characters>();
                                var c = new Characters();
                                c.CharactersList = GetCharactersFromAniDB(anim);
                                j.CharactersList.Add(c);
                                //experimentEND

                                //proper naming 
                                j.OriginalTitle = "";
                                foreach (var title in anim.GetTitles())
                                {
                                    if (title.TitleType == "official" || title.TitleType == "main")
                                    {
                                        j.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" +
                                                           title.Title + "|";
                                    }
                                }
                                j.OriginalTitle = j.OriginalTitle.Substring(0, j.OriginalTitle.Length - 1);
                                //proper naming end

                                j.totalLocal = ser.GetAnimeEpisodesCountWithVideoLocal();

                                if (!string.IsNullOrEmpty(anim.AllTags))
                                {
                                    j.Tags = new List<Tag> { new Tag { Value = anim.AllTags.Replace("|", ",") } };
                                }
                                j.Rating = (anim.Rating / 100F).ToString(CultureInfo.InvariantCulture);
                                j.Year = "" + anim.BeginYear;

                                //community support

                                //CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                                //List<CrossRef_AniDB_TraktV2> Trakt = repCrossRef.GetByAnimeID(anim.AnimeID);
                                //if (Trakt != null)
                                //{
                                //    if (Trakt.Count > 0)
                                //    {
                                //        j.Trakt = Trakt[0].TraktID;
                                //    }
                                //}

                                //CrossRef_AniDB_TvDBV2Repository repCrossRefV2 = new CrossRef_AniDB_TvDBV2Repository();
                                //List<CrossRef_AniDB_TvDBV2> TvDB = repCrossRefV2.GetByAnimeID(anim.AnimeID);
                                //if (TvDB != null)
                                //{
                                //    if (TvDB.Count > 0)
                                //    {
                                //        j.TvDB = TvDB[0].TvDBID.ToString();
                                //    }
                                //}

                                //community support END

                                joints2.Add(j);
                                retGroups.Remove(j);
                                break;
                            }
                        }
                    }
                    ret.Childrens = joints2;
                    ret.MediaContainer.Art = KodiHelper.GetRandomFanartFromVideoList(ret.Childrens);
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

        //experiment
        private List<Character> GetCharactersFromAniDB(AniDB_Anime anidb_anime)
        {
            var char_list = new List<Character>();
            foreach (var achar in anidb_anime.GetAnimeCharacters())
            {
                var x = achar.GetCharacter();
                var c = new Character();
                c.CharID = x.AniDB_CharacterID;
                c.CharName = x.CharName;
                c.Description = x.CharDescription;
                c.Picture = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                    MainWindow.PathAddressREST + "/GetImage/2/" + c.CharID);
                var seiyuu_tmp = x.GetSeiyuu();
                if (seiyuu_tmp != null)
                {
                    c.SeiyuuName = seiyuu_tmp.SeiyuuName;
                    c.SeiyuuPic = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                        MainWindow.PathAddressREST + "/GetImage/3/" + x.GetSeiyuu().AniDB_SeiyuuID);
                }
                else
                {
                    c.SeiyuuName = "";
                    c.SeiyuuPic = "";
                }

                char_list.Add(c);
            }
            return char_list;
        }
    }
}