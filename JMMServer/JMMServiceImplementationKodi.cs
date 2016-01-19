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
using JMMServer.ImageDownload;
using JMMServer.Properties;
using JMMServer.Repositories;
using NLog;
using JMMServer.Kodi;
using Directory = JMMContracts.KodiContracts.Directory;

// ReSharper disable FunctionComplexityOverflow
namespace JMMServer
{
    public class JMMServiceImplementationKodi : IJMMServerKodi
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        
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
            JMMUser user = KodiHelper.GetUser(uid);
            if (user==null)
                return new MemoryStream();
            int userid = user.JMMUserID;
            KodiObject ret =new KodiObject(KodiHelper.NewMediaContainer("Anime", false));
            if (!ret.Init())
                return new MemoryStream();
            List<Video> dirs = new List<Video>();
            try
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {

                    GroupFilterRepository repGF = new GroupFilterRepository();
                    List<GroupFilter> allGfs = repGF.GetAll(session);
                    Dictionary<int, HashSet<int>> gstats = StatsCache.Instance.StatUserGroupFilter[userid];
                    foreach (GroupFilter gg in allGfs.ToArray())
                    {
                        if ((!StatsCache.Instance.StatUserGroupFilter.ContainsKey(userid)) ||
                            (!StatsCache.Instance.StatUserGroupFilter[userid].ContainsKey(gg.GroupFilterID)))
                        {
                            allGfs.Remove(gg);
                        }
                    }


                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    allGfs.Insert(0, new GroupFilter() {GroupFilterName = "All", GroupFilterID = -999});
                    foreach (GroupFilter gg in allGfs)
                    {

                        Random rnd = new Random(123456789);
                        Directory pp = new Directory();
                        pp.Key = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                        MainWindow.PathAddressKodi + "/GetMetadata/" + userid + "/" +
                                        (int) JMMType.GroupFilter + "/" + gg.GroupFilterID);
                        pp.PrimaryExtraKey = pp.Key;
                        pp.Title = gg.GroupFilterName;
                        HashSet<int> groups;
                        groups = gg.GroupFilterID == -999 ? new HashSet<int>(repGroups.GetAllTopLevelGroups(session).Select(a => a.AnimeGroupID)) : gstats[gg.GroupFilterID];
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
                            dirs.Add(pp);
                        }
                    }
                    VideoLocalRepository repVids = new VideoLocalRepository();
                    List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
                    if (vids.Count > 0)
                    {
                        JMMContracts.KodiContracts.Directory pp = new JMMContracts.KodiContracts.Directory();
                        pp.Key = pp.PrimaryExtraKey = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                     MainWindow.PathAddressKodi + "/GetMetadata/0/" + (int) JMMType.GroupUnsort + "/0");
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

        public System.IO.Stream GetMetadata(string UserId, string TypeId, string Id)
        {
            try
            {
                int type;
                int.TryParse(TypeId, out type);
                JMMUser user = KodiHelper.GetUser(UserId);
                switch ((JMMType) type)
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

        private System.IO.Stream GetUnsort(int userid)
        {
            KodiObject ret =new KodiObject(KodiHelper.NewMediaContainer("Unsort", true));
            if (!ret.Init())
                return new MemoryStream();
            List<Video> dirs= new List<Video>();
            ret.MediaContainer.ViewMode = "65586";
            ret.MediaContainer.ViewGroup = "video";
            VideoLocalRepository repVids = new VideoLocalRepository();
            List<VideoLocal> vids = repVids.GetVideosWithoutEpisode();
            foreach (VideoLocal v in vids.OrderByDescending(a => a.DateTimeCreated))
            {
                Video m = new Video();
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

        public System.IO.Stream GetFile(string Id)
        {
            JMMUser user = KodiHelper.GetUser("0");
            return InternalGetFile(user.JMMUserID, Id);
        }

        private System.IO.Stream InternalGetFile(int userid, string Id)
        {

            int id;
            if (!int.TryParse(Id, out id))
                return new MemoryStream();
            KodiObject ret =new KodiObject(KodiHelper.NewMediaContainer("Unsort", true));
            if (!ret.Init())
                return new MemoryStream();
            List<Video> dirs= new List<Video>();
            Video v = new Video();
            dirs.Add(v);
            VideoLocalRepository repVids = new VideoLocalRepository();
            VideoLocal vi = repVids.GetByID(id);
            if (vi == null)
                return new MemoryStream();
            KodiHelper.PopulateVideo(v,vi,JMMType.File,userid);
            ret.Childrens = dirs;
            ret.MediaContainer.Art = v.Art;
            return ret.GetStream();
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
            return KodiHelper.GetStreamFromXmlObject(gfs);
        }

        public System.IO.Stream Search(string UserId, string limit, string query)
        {
            return Search(UserId, limit, query, false);
        }

        public System.IO.Stream SearchTag(string UserId, string limit, string query)
        {
            return Search(UserId, limit, query, true);
        }

        public System.IO.Stream Search(string UserId, string limit, string query, bool searchTag)
        {
            KodiObject ret =new KodiObject(KodiHelper.NewMediaContainer("Search",false));
            ret.MediaContainer.Title2 = "Search Results for '" + query + "'...";
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
            AnimeSeriesRepository repSeries = new AnimeSeriesRepository();

            int lim;
            if (!int.TryParse(limit, out lim))
                lim = 100;
            JMMUser user = KodiHelper.GetUser(UserId);
            if (user == null) return new MemoryStream();
            List<Video> ls=new List<Video>();
            int cnt = 0;
            List<AniDB_Anime> animes;
            if (searchTag)
            {
                animes = repAnime.SearchByTag(query);
            }
            else
            {
                animes = repAnime.SearchByName(query);
            }
            foreach (AniDB_Anime anidb_anime in animes)
            {
                if (!user.AllowedAnime(anidb_anime)) continue;
                AnimeSeries ser = repSeries.GetByAnimeID(anidb_anime.AnimeID);
                if (ser != null)
                {
                    Contract_AnimeSeries cserie = ser.ToContract(ser.GetUserRecord(user.JMMUserID), true);
                    Video v = KodiHelper.FromSerieWithPossibleReplacement(cserie, ser, user.JMMUserID);

                    //proper naming 
                    v.OriginalTitle = "";
                    foreach (AniDB_Anime_Title title in anidb_anime.GetTitles())
                    {
                        if (title.TitleType == "official" || title.TitleType == "main")
                        {
                            v.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" + title.Title + "|";
                        }
                    }
                    v.OriginalTitle = v.OriginalTitle.Substring(0, v.OriginalTitle.Length - 1);
                    //proper naming end

                    //experiment
                    Characters c = new Characters();
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
            ret.MediaContainer.Childrens= ls;
            return ret.GetStream();
        }
       
        public System.IO.Stream GetItemsFromGroup(int userid, string GroupId)
        {
            KodiObject ret =new KodiObject(KodiHelper.NewMediaContainer("Groups",true));
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
                if (grp != null)
                {
                    Contract_AnimeGroup basegrp = grp.ToContract(grp.GetUserRecord(session, userid));
                    ret.MediaContainer.Title1 = ret.MediaContainer.Title2 = basegrp.GroupName;
                    List<AnimeSeries> sers2 = grp.GetSeries(session);
                    ret.MediaContainer.Art = KodiHelper.GetRandomFanartFromSeries(sers2, session);
                    foreach (AnimeGroup grpChild in grp.GetChildGroups())
                    {
                        Video v = StatsCache.Instance.StatKodiGroupsCache[userid][grpChild.AnimeGroupID];
                        v.Type = "show";
                        if (v != null)
                            retGroups.Add(v.Clone());
                    }
                    foreach (AnimeSeries ser in grp.GetSeries())
                    {
                        Contract_AnimeSeries cserie = ser.ToContract(ser.GetUserRecord(session, userid), true);
                        Video v = KodiHelper.FromSerieWithPossibleReplacement(cserie, ser, userid);
                        v.AirDate = ser.AirDate.HasValue ? ser.AirDate.Value : DateTime.MinValue;
                        v.Group = basegrp;
                        retGroups.Add(v);
                    }
                }
                ret.Childrens = retGroups.OrderBy(a => a.AirDate).ToList();
                return ret.GetStream();
            }
        }

        public System.IO.Stream GetItemsFromSerie(int userid, string SerieId)
        {
            KodiObject ret = new KodiObject(KodiHelper.NewMediaContainer("Series", true));
            if (!ret.Init())
                return new MemoryStream();
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

                ImageDetails fanart = anime.GetDefaultFanartDetailsNoBlanks(session);
                if (fanart != null)
                    ret.MediaContainer.Art = fanart.GenArt();
                ret.MediaContainer.Title2 = ret.MediaContainer.Title1 = anime.MainTitle;
                List<AnimeEpisode> episodes = ser.GetAnimeEpisodes(session).Where(a => a.GetVideoLocals(session).Count > 0).ToList();
                if (eptype.HasValue)
                {
                    episodes = episodes.Where(a => a.EpisodeTypeEnum == eptype.Value).ToList();
                }
                else
                {
                    List<enEpisodeType> types = episodes.Select(a => a.EpisodeTypeEnum).Distinct().ToList();
                    if (types.Count > 1)
                    {
                        List<KodiEpisodeType> eps = new List<KodiEpisodeType>();
                        foreach (enEpisodeType ee in types)
                        {
                            KodiEpisodeType k2 = new KodiEpisodeType();
                            KodiEpisodeType.EpisodeTypeTranslated(k2, ee, (AnimeTypes)anime.AnimeType, episodes.Count(a => a.EpisodeTypeEnum == ee));
                            eps.Add(k2);
                        }
                        List<SortPropOrFieldAndDirection> sortCriteria = new List<SortPropOrFieldAndDirection>();
                        sortCriteria.Add(new SortPropOrFieldAndDirection("Name", SortType.eString));
                        eps = Sorting.MultiSort(eps, sortCriteria);
                        List<Video> dirs= new List<Video>();

                        bool isCharacterSetup_ = false;

                        foreach (KodiEpisodeType ee in  eps)
                        {
                            Video v = new Directory();
                            v.Title = ee.Name;
                            v.Type = "season";
                            v.LeafCount = ee.Count.ToString();
                            v.ViewedLeafCount = "0";
                            v.Key = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressKodi + "/GetMetadata/" + userid + "/" + (int)JMMType.Serie + "/" + ee.Type + "_" + ser.AnimeSeriesID);
                            v.Thumb = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort),
                                MainWindow.PathAddressKodi + "/GetSupportImage/" + ee.Image);
                            if ((ee.AnimeType==AnimeTypes.Movie) || (ee.AnimeType==AnimeTypes.OVA))
                            {
                                v = KodiHelper.MayReplaceVideo((Directory)v, ser,anime,  JMMType.File, userid, false);
                            }

                            //proper naming 
                            v.OriginalTitle = "";
                            foreach (AniDB_Anime_Title title in anime.GetTitles())
                            {
                                if (title.TitleType == "official" || title.TitleType == "main")
                                {
                                    v.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" + title.Title + "|";
                                }
                            }
                            v.OriginalTitle = v.OriginalTitle.Substring(0, v.OriginalTitle.Length - 1);
                            //proper naming end

                            //experiment
                            if (!isCharacterSetup_)
                            {
                                Characters ch = new Characters();
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
                List<Video> vids=new List<Video>();
                Contract_AnimeSeries cseries = ser.ToContract(ser.GetUserRecord(userid), true);
                Video nv = KodiHelper.FromSerie(cseries, userid);
                KodiEpisodeType k = new KodiEpisodeType();
                if (eptype.HasValue)
                {
                    KodiEpisodeType.EpisodeTypeTranslated(k, (enEpisodeType) eptype.Value, (AnimeTypes) anime.AnimeType,
                        episodes.Count);
                }

                bool isCharacterSetup = false;

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
                            Characters c = new Characters();
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

                List<SortPropOrFieldAndDirection> sortCriteria2 = new List<SortPropOrFieldAndDirection>();
                sortCriteria2.Add(new SortPropOrFieldAndDirection("EpNumber", SortType.eInteger));
                vids= Sorting.MultiSort(vids, sortCriteria2);
                ret.Childrens = vids;

                return ret.GetStream();
            }
        }

        private System.IO.Stream GetGroupsFromFilter(int userid, string GroupFilterId)
        {
            KodiObject ret=new KodiObject(KodiHelper.NewMediaContainer("Filters",true));
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

                    AnimeGroupRepository repGroups = new AnimeGroupRepository();
                    List<AnimeGroup> allGrps = repGroups.GetAll(session);



                    
                    TimeSpan ts = DateTime.Now - start;
                    string msg = string.Format("Got groups for filter DB: {0} - {1} in {2} ms", gf.GroupFilterName,
                        allGrps.Count, ts.TotalMilliseconds);
                    logger.Info(msg);
                    start = DateTime.Now;



                    if ((StatsCache.Instance.StatUserGroupFilter.ContainsKey(userid)) &&
                        (StatsCache.Instance.StatUserGroupFilter[userid].ContainsKey(gf.GroupFilterID)))
                    {
                        HashSet<int> groups = StatsCache.Instance.StatUserGroupFilter[userid][gf.GroupFilterID];
                        var tas = StatsCache.Instance.StatKodiGroupsCache;
                        foreach (AnimeGroup grp in allGrps)
                        {
                            if (groups.Contains(grp.AnimeGroupID))
                            {
                                try {
                                    if (grp.GroupName == "Rockman.EXE")
                                    {
                                        int x = grp.MissingEpisodeCount;
                                    }
                                    Video v = StatsCache.Instance.StatKodiGroupsCache[userid][grp.AnimeGroupID];
                                    if (v != null)
                                        retGroups.Add(v.Clone());
                                }
                                catch(Exception e)
                                {
                                    int x = retGroups.Count;
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
                                //experiment
                                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                                AnimeSeriesRepository repSeries = new AnimeSeriesRepository();
                                AnimeGroup ag = repGroups.GetByID(gr.AnimeGroupID);
                                List<AnimeSeries> sers = ag.GetAllSeries();
                                AnimeSeries ser = sers[0];
                                AniDB_Anime anim = ser.GetAnime();

                                j.CharactersList = new List<Characters>();
                                Characters c = new Characters();
                                c.CharactersList = GetCharactersFromAniDB(anim);
                                j.CharactersList.Add(c);
                                //experimentEND

                                //proper naming 
                                j.OriginalTitle = "";
                                foreach (AniDB_Anime_Title title in anim.GetTitles())
                                {
                                    if (title.TitleType == "official" || title.TitleType == "main")
                                    {
                                        j.OriginalTitle += "{" + title.TitleType + ":" + title.Language + "}" + title.Title + "|";
                                    }
                                }
                                j.OriginalTitle = j.OriginalTitle.Substring(0, j.OriginalTitle.Length - 1);
                                //proper naming end

                                //community support

                                CrossRef_AniDB_TraktV2Repository repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                                List<CrossRef_AniDB_TraktV2> Trakt = repCrossRef.GetByAnimeID(anim.AnimeID);
                                if (Trakt != null)
                                {
                                    if (Trakt.Count > 0)
                                    {
                                        j.Trakt = Trakt[0].TraktID;
                                    }
                                }

                                CrossRef_AniDB_TvDBV2Repository repCrossRefV2 = new CrossRef_AniDB_TvDBV2Repository();
                                List<CrossRef_AniDB_TvDBV2> TvDB = repCrossRefV2.GetByAnimeID(anim.AnimeID);
                                if (TvDB != null)
                                {
                                    if (TvDB.Count > 0)
                                    {
                                        j.TvDB = TvDB[0].TvDBID.ToString();
                                    }
                                }

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
                AniDB_Anime anime = ser?.GetAnime();
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
        
        //experiment
        private List<Character> GetCharactersFromAniDB( AniDB_Anime anidb_anime)
        {

            List<Character> char_list = new List<Character>();
            foreach (AniDB_Anime_Character achar in anidb_anime.GetAnimeCharacters())
            {
                AniDB_Character x = achar.GetCharacter();
                Character c = new Character();
                c.CharID = x.AniDB_CharacterID;
                c.CharName = x.CharName;
                c.Description = x.CharDescription;
                c.Picture = KodiHelper.ServerUrl(int.Parse(ServerSettings.JMMServerPort), MainWindow.PathAddressREST + "/GetImage/2/" + c.CharID);
                c.SeiyuuName = "";
                c.SeiyuuPic = "";

                char_list.Add(c);
            }
            return char_list;
        }

        public void TraktScrobble(string slug, string traktid, string type, string progress, string status)
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
                    Providers.TraktTV.TraktTVHelper.Scrobble(Providers.TraktTV.ScrobblePlayingType.movie, slug, traktid, statusTraktV2, progressTrakt);
                    break;
                //2
                case (int)Providers.TraktTV.ScrobblePlayingType.episode:
                    Providers.TraktTV.TraktTVHelper.Scrobble(Providers.TraktTV.ScrobblePlayingType.episode, slug, traktid, statusTraktV2, progressTrakt);
                    break;
                //error
                default:
                    break;
            }
        }

    }

}
