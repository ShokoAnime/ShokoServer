using System;
using System.Collections;
using System.Collections.Generic;
using JMMServer.Databases;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;

namespace JMMServer.Repositories
{
    public class AnimeEpisodeRepository
    {
        public void Save(AnimeEpisode obj)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                Save(session, obj);
            }
        }

        public void Save(ISession session, AnimeEpisode obj)
        {
            // populate the database
            using (var transaction = session.BeginTransaction())
            {
                session.SaveOrUpdate(obj);
                transaction.Commit();
            }
        }

        public AnimeEpisode GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByID(session, id);
            }
        }

        public AnimeEpisode GetByID(ISession session, int id)
        {
            return session.Get<AnimeEpisode>(id);
        }

        public List<AnimeEpisode> GetBySeriesID(int seriesid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetBySeriesID(session, seriesid);
            }
        }

        public List<AnimeEpisode> GetBySeriesID(ISession session, int seriesid)
        {
            var eps = session
                .CreateCriteria(typeof(AnimeEpisode))
                .Add(Restrictions.Eq("AnimeSeriesID", seriesid))
                .List<AnimeEpisode>();

            return new List<AnimeEpisode>(eps);
        }

        public AnimeEpisode GetByAniDBEpisodeID(int epid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAniDBEpisodeID(session, epid);
            }
        }

        public AnimeEpisode GetByAniDBEpisodeID(ISession session, int epid)
        {
            AnimeEpisode obj = session
                .CreateCriteria(typeof(AnimeEpisode))
                .Add(Restrictions.Eq("AniDB_EpisodeID", epid))
                .UniqueResult<AnimeEpisode>();

            return obj;
        }

        public List<AnimeEpisode> GetByAniEpisodeIDAndSeriesID(int epid, int seriesid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByAniEpisodeIDAndSeriesID(session, epid, seriesid);
            }
        }

        public List<AnimeEpisode> GetByAniEpisodeIDAndSeriesID(ISession session, int epid, int seriesid)
        {
            var eps = session
                .CreateCriteria(typeof(AnimeEpisode))
                .Add(Restrictions.Eq("AniDB_EpisodeID", epid))
                .Add(Restrictions.Eq("AnimeSeriesID", seriesid))
                .List<AnimeEpisode>();

            return new List<AnimeEpisode>(eps);
        }

        /// <summary>
        /// Get all the AnimeEpisode records associate with an AniDB_File record
        /// AnimeEpisode.AniDB_EpisodeID -> AniDB_Episode.EpisodeID
        /// AniDB_Episode.EpisodeID -> CrossRef_File_Episode.EpisodeID
        /// CrossRef_File_Episode.Hash -> VideoLocal.Hash
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public List<AnimeEpisode> GetByHash(ISession session, string hash)
        {
            var eps =
                session.CreateQuery(
                    "Select ae FROM AnimeEpisode as ae, CrossRef_File_Episode as xref WHERE ae.AniDB_EpisodeID = xref.EpisodeID AND xref.Hash= :Hash")
                    .SetParameter("Hash", hash)
                    .List<AnimeEpisode>();

            return new List<AnimeEpisode>(eps);
        }

        public List<AnimeEpisode> GetByHash(string hash)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByHash(session, hash);
            }
        }

        public List<AnimeEpisode> GetEpisodesWithMultipleFiles(bool ignoreVariations)
        {
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
                    ArrayList results = DatabaseHelper.GetData(sql);

                    foreach (object[] res in results)
                    {
                        int animeEpisodeID = int.Parse(res[0].ToString());
                        AnimeEpisode ep = session.Get<AnimeEpisode>(animeEpisodeID);
                        if (ep != null)
                            epList.Add(ep);
                    }

                    return epList;
                }
                else
                {
                    string sql = "FROM AnimeEpisode x WHERE x.AniDB_EpisodeID IN " +
                                 "(Select xref.EpisodeID FROM CrossRef_File_Episode xref WHERE xref.Hash IN " +
                                 "(Select vl.Hash from VideoLocal vl ";

                    if (ignoreVariations)
                        sql += " where IsVariation = 0 ";

                    sql += ") GROUP BY xref.EpisodeID HAVING COUNT(xref.EpisodeID) > 1)";

                    var eps = session.CreateQuery(sql)
                        .List<AnimeEpisode>();

                    return new List<AnimeEpisode>(eps);
                }
            }
        }

        public List<AnimeEpisode> GetUnwatchedEpisodes(int seriesid, int userid)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps =
                    session.CreateQuery(
                        "FROM AnimeEpisode x WHERE x.AnimeEpisodeID NOT IN (SELECT AnimeEpisodeID FROM AnimeEpisode_User WHERE AnimeSeriesID = :AnimeSeriesID AND JMMUserID = :JMMUserID) AND x.AnimeSeriesID = :AnimeSeriesID")
                        .SetParameter("AnimeSeriesID", seriesid)
                        .SetParameter("JMMUserID", userid)
                        .List<AnimeEpisode>();

                return new List<AnimeEpisode>(eps);
            }
        }

        public List<AnimeEpisode> GetMostRecentlyAdded(int seriesID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var eps = session
                    .CreateCriteria(typeof(AnimeEpisode))
                    .Add(Restrictions.Eq("AnimeSeriesID", seriesID))
                    .AddOrder(Order.Desc("DateTimeCreated"))
                    .SetMaxResults(1)
                    .List<AnimeEpisode>();

                return new List<AnimeEpisode>(eps);
            }
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
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}