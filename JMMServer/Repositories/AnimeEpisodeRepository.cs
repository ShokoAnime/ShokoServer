using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using JMMServer.PlexAndKodi;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AnimeEpisodeRepository
    {
        private static PocoCache<int, AnimeEpisode> Cache;
        private static PocoIndex<int, AnimeEpisode, int> Series;
        private static PocoIndex<int, AnimeEpisode, int> EpisodeIDs;

        public static void InitCache()
        {
            string t = "AnimeEpisodes";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            AnimeEpisodeRepository repo = new AnimeEpisodeRepository();

            Cache = new PocoCache<int, AnimeEpisode>(repo.InternalGetAll(), a => a.AnimeEpisodeID);
            Series = Cache.CreateIndex(a => a.AnimeSeriesID);
            EpisodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);

            int cnt = 0;
            List<AnimeEpisode> grps =
                Cache.Values.Where(a => a.PlexContractVersion < AnimeEpisode.PLEXCONTRACT_VERSION).ToList();
            int max = grps.Count;
            foreach (AnimeEpisode g in grps)
            {
                repo.Save(g);
                cnt++;
                if (cnt%10 == 0)
                {
                    ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                        " DbRegen - " + cnt + "/" + max);
                }
            }
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t,
                " DbRegen - " + max + "/" + max);
        }

        private List<AnimeEpisode> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var grps = session
                    .CreateCriteria(typeof(AnimeEpisode))
                    .List<AnimeEpisode>();

                return new List<AnimeEpisode>(grps);
            }
        }

        private void UpdatePlexContract(AnimeEpisode e)
        {
            e.PlexContract = Helper.GenerateVideoFromAnimeEpisode(e);
        }

        public void Save(AnimeEpisode obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Save(session, obj);
            }
        }

        public void Save(ISession session, AnimeEpisode obj)
        {
            lock (obj)
            {
                if (obj.AnimeEpisodeID == 0)
                {
                    obj.PlexContract = null;
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                }
                UpdatePlexContract(obj);
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
                Cache.Update(obj);
            }
        }

        public AnimeEpisode GetByID(int id)
        {
            return Cache.Get(id);
        }



        public List<AnimeEpisode> GetAll()
        {
            return Cache.Values.ToList();
        }

        public List<AnimeEpisode> GetBySeriesID(int seriesid)
        {
            return Series.GetMultiple(seriesid);
        }


        public AnimeEpisode GetByAniDBEpisodeID(int epid)
        {
            //AniDB_Episode may not unique for the series, Example with Toriko Episode 1 and One Piece 492, same AniDBEpisodeID in two shows.
            return EpisodeIDs.GetOne(epid);
        }


        /// <summary>
        /// Get all the AnimeEpisode records associate with an AniDB_File record
        /// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
        /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
        /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>

        public List<AnimeEpisode> GetByHash(string hash)
        {
            return new CrossRef_File_EpisodeRepository().GetByHash(hash).Select(a => GetByAniDBEpisodeID(a.EpisodeID)).Where(a => a != null).ToList();
            /*
            return
                session.CreateQuery(
                    "Select ae.AnimeEpisodeID FROM AnimeEpisode as ae, CrossRef_File_Episode as xref WHERE ae.AniDB_EpisodeID = xref.EpisodeID AND xref.Hash= :Hash")
                    .SetParameter("Hash", hash)
                    .List<int>().Select(GetByID).Where(a => a != null).ToList();*/
        }

        public List<AnimeEpisode> GetEpisodesWithMultipleFiles(bool ignoreVariations)
        {

            List<string> hashes = ignoreVariations ? new VideoLocalRepository().GetAll().Where(a=>a.IsVariation==0).Select(a => a.Hash).Where(a => a != string.Empty).ToList() : new VideoLocalRepository().GetAll().Select(a => a.Hash).Where(a => a != string.Empty).ToList();
            return new CrossRef_File_EpisodeRepository().GetAll()
                .Where(a => hashes.Contains(a.Hash))
                .GroupBy(a => a.EpisodeID)
                .Where(a => a.Count() > 1)
                .Select(a => GetByAniDBEpisodeID(a.Key))
                .ToList();
            /*

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                //FROM AnimeEpisode x WHERE x.AniDB_EpisodeID IN (Select xref.EpisodeID FROM CrossRef_File_Episode xref WHERE xref.Hash IN (Select vl.Hash from VideoLocal vl) GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)


                //FROM AnimeEpisode x INNER JOIN (select xref.EpisodeID as EpisodeID from CrossRef_File_Episode xref inner join VideoLocal vl ON xref.Hash = vl.Hash group by xref.EpisodeID  having count(xref.EpisodeID)>1) g ON g.EpisodeID = x.AniDB_EpisodeID

                if (ServerSettings.DatabaseType.Trim()
                    .Equals(Constants.DatabaseType.MySQL, StringComparison.InvariantCultureIgnoreCase))
                {
                    // work around for MySQL performance issue when handling sub queries
                    List<AnimeEpisode> epList = new List<AnimeEpisode>();
                    string sql = "Select x.AnimeEpisodeID " +
                                 "FROM AnimeEpisode x " +
                                 "INNER JOIN  " +
                                 "(select xref.EpisodeID as EpisodeID " +
                                 "from CrossRef_File_Episode xref " +
                                 "inner join VideoLocal vl ON xref.Hash = vl.Hash ";

                    if (ignoreVariations)
                        sql += " where IsVariation = 0 ";

                    sql += "group by xref.EpisodeID  having count(xref.EpisodeID)>1) " +
                           "g ON g.EpisodeID = x.AniDB_EpisodeID " +
                           " ";
                    ArrayList results = DatabaseExtensions.Instance.GetData(sql);

                    foreach (object[] res in results)
                    {
                        int animeEpisodeID = int.Parse(res[0].ToString());
                        AnimeEpisode ep = GetByID(animeEpisodeID);
                        if (ep != null)
                            epList.Add(ep);
                    }

                    return epList;
                }
                else
                {
                    string sql = "SELECT x.AnimeEpisodeID FROM AnimeEpisode x WHERE x.AniDB_EpisodeID IN " +
                                 "(Select xref.EpisodeID FROM CrossRef_File_Episode xref WHERE xref.Hash IN " +
                                 "(Select vl.Hash from VideoLocal vl ";

                    if (ignoreVariations)
                        sql += " where IsVariation = 0 ";

                    sql += ") GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)";

                    return session.CreateQuery(sql).List<int>().Select(GetByID).Where(a => a != null).ToList();
                    ;
                }

            }*/
        }

        public List<AnimeEpisode> GetUnwatchedEpisodes(int seriesid, int userid)
        {
            List<int> eps =
                new AnimeEpisode_UserRepository().GetByUserIDAndSeriesID(userid, seriesid).Where(a=>a.WatchedDate.HasValue)
                    .Select(a => a.AnimeEpisodeID)
                    .ToList();
            return GetBySeriesID(seriesid).Where(a => !eps.Contains(a.AnimeEpisodeID)).ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return
                    session.CreateQuery(
                        "SELECT x.AnimeEpisodeID FROM AnimeEpisode x WHERE x.AnimeEpisodeID NOT IN (SELECT AnimeEpisodeID FROM AnimeEpisode_User WHERE AnimeSeriesID = :AnimeSeriesID AND JMMUserID = :JMMUserID) AND x.AnimeSeriesID = :AnimeSeriesID")
                        .SetParameter("AnimeSeriesID", seriesid)
                        .SetParameter("JMMUserID", userid)
                        .List<int>().Select(GetByID).Where(a => a != null).ToList();
            }*/
        }

        public List<AnimeEpisode> GetMostRecentlyAdded(int seriesID)
        {
            return GetBySeriesID(seriesID).OrderByDescending(a => a.DateTimeCreated).ToList();
        }

        public void Delete(int id)
        {
            AnimeEpisode cr = GetByID(id);
            if (cr != null)
            {
                // delete user records
                AnimeEpisode_UserRepository repUsers = new AnimeEpisode_UserRepository();
                foreach (AnimeEpisode_User epuser in repUsers.GetByEpisodeID(id))
                    repUsers.Delete(epuser.AnimeEpisode_UserID);
            }

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}