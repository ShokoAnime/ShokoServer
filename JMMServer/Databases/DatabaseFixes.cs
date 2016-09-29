using System;
using System.Collections.Generic;
using System.Linq;
using AniDBAPI;
using JMMServer.Entities;
using JMMServer.Repositories;
using JMMServer.Repositories.Cached;
using JMMServer.Repositories.Direct;
using JMMServer.Repositories.NHibernate;
using NLog;

namespace JMMServer.Databases
{
    public class DatabaseFixes
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static List<Tuple<IDatabase,DatabaseCommand>> Fixes = new List<Tuple<IDatabase, DatabaseCommand>>();

        public static void ExecuteDatabaseFixes()
        {
            foreach (Tuple<IDatabase, DatabaseCommand> t in Fixes)
            {
                try
                {
                    t.Item2.DatabaseFix();
                    t.Item1.AddVersion(t.Item2.Version.ToString(),t.Item2.Revision.ToString(),t.Item2.CommandName);
                }
                catch (Exception e)
                {
                    throw new DatabaseCommandException(e.ToString(),t.Item2);
                }
            }
        }

        public static void AddFix(IDatabase db, DatabaseCommand cmd)
        {
            Fixes.Add(new Tuple<IDatabase, DatabaseCommand>(db,cmd));
        }

        public static void InitFixes()
        {
            Fixes = new List<Tuple<IDatabase, DatabaseCommand>>();
        }

        public static void DeleteSerieUsersWithoutSeries()
        {
            //DB Fix Series not deleting series_user
            HashSet<int> list = new HashSet<int>(RepoFactory.AnimeSeries.Cache.Keys);
            RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.Cache.Values.Where(a => !list.Contains(a.AnimeSeriesID)).ToList());
        }
        public static void FixHashes()
        {
            try
            {
                foreach (VideoLocal vid in RepoFactory.VideoLocal.GetAll())
                {
                    bool fixedHash = false;
                    if (vid.CRC32.Equals("00000000"))
                    {
                        vid.CRC32 = null;
                        fixedHash = true;
                    }
                    if (vid.MD5.Equals("00000000000000000000000000000000"))
                    {
                        vid.MD5 = null;
                        fixedHash = true;
                    }
                    if (vid.SHA1.Equals("0000000000000000000000000000000000000000"))
                    {
                        vid.SHA1 = null;
                        fixedHash = true;
                    }
                    if (fixedHash)
                    {
                        RepoFactory.VideoLocal.Save(vid, false);
                        logger.Info("Fixed hashes on file: {0}", vid.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,ex.ToString());
            }
        }

        public static void RemoveOldMovieDBImageRecords()
        {
            try
            {
                
                RepoFactory.MovieDB_Fanart.Delete(RepoFactory.MovieDB_Fanart.GetAll());
                RepoFactory.MovieDB_Poster.Delete(RepoFactory.MovieDB_Poster.GetAll());
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Could not RemoveOldMovieDBImageRecords: " + ex.ToString());
            }
        }


        public static void FixContinueWatchingGroupFilter_20160406()
        {
            // group filters
          
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // check if it already exists
                List<GroupFilter> lockedGFs = RepoFactory.GroupFilter.GetLockedGroupFilters();

                if (lockedGFs != null)
                {
                    // if it already exists we can leave
                    foreach (GroupFilter gf in lockedGFs)
                    {
                        if (gf.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            gf.FilterType = (int) GroupFilterType.ContinueWatching;
                            RepoFactory.GroupFilter.Save(gf);
                        }
                    }
                }
            }
        }

        public static void MigrateTraktLinks_V1_to_V2()
        {
            try
            {



                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    List<CrossRef_AniDB_Trakt> xrefsTrakt = RepoFactory.CrossRef_AniDB_Trakt.GetAll();
                    foreach (CrossRef_AniDB_Trakt xrefTrakt in xrefsTrakt)
                    {
                        CrossRef_AniDB_TraktV2 xrefNew = new CrossRef_AniDB_TraktV2();
                        xrefNew.AnimeID = xrefTrakt.AnimeID;
                        xrefNew.CrossRefSource = xrefTrakt.CrossRefSource;
                        xrefNew.TraktID = xrefTrakt.TraktID;
                        xrefNew.TraktSeasonNumber = xrefTrakt.TraktSeasonNumber;

                        Trakt_Show show = xrefTrakt.GetByTraktShow(session);
                        if (show != null)
                            xrefNew.TraktTitle = show.Title;

                        // determine start ep type
                        if (xrefTrakt.TraktSeasonNumber == 0)
                            xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Special;
                        else
                            xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Episode;

                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TraktStartEpisodeNumber = 1;

                        RepoFactory.CrossRef_AniDB_TraktV2.Save(xrefNew);
                    }

                    // create cross ref's for specials
                    foreach (CrossRef_AniDB_Trakt xrefTrakt in xrefsTrakt)
                    {
                        AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xrefTrakt.AnimeID);
                        if (anime == null) continue;

                        Trakt_Show show = xrefTrakt.GetByTraktShow(session);
                        if (show == null) continue;

                        // this anime has specials
                        if (anime.EpisodeCountSpecial <= 0) continue;

                        // this Trakt series has a season 0 (specials)
                        List<int> seasons = RepoFactory.Trakt_Episode.GetSeasonNumbersForSeries(show.Trakt_ShowID);
                        if (!seasons.Contains(0)) continue;

                        //make sure we are not doubling up
                        CrossRef_AniDB_TraktV2 temp = RepoFactory.CrossRef_AniDB_TraktV2.GetByTraktID(xrefTrakt.TraktID, 0, 1,
                            xrefTrakt.AnimeID,
                            (int) enEpisodeType.Special, 1);
                        if (temp != null) continue;

                        CrossRef_AniDB_TraktV2 xrefNew = new CrossRef_AniDB_TraktV2();
                        xrefNew.AnimeID = xrefTrakt.AnimeID;
                        xrefNew.CrossRefSource = xrefTrakt.CrossRefSource;
                        xrefNew.TraktID = xrefTrakt.TraktID;
                        xrefNew.TraktSeasonNumber = 0;
                        xrefNew.TraktStartEpisodeNumber = 1;
                        xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Special;
                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TraktTitle = show.Title;

                        RepoFactory.CrossRef_AniDB_TraktV2.Save(xrefNew);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Could not MigrateTraktLinks_V1_to_V2: " + ex.ToString());
            }
        }

        public static void MigrateTvDBLinks_V1_to_V2()
        {
            try
            {
                
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    ISessionWrapper sessionWrapper = session.Wrap();
                    List<CrossRef_AniDB_TvDB> xrefsTvDB = RepoFactory.CrossRef_AniDB_TvDB.GetAll();
                    foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
                    {
                        CrossRef_AniDB_TvDBV2 xrefNew = new CrossRef_AniDB_TvDBV2();
                        xrefNew.AnimeID = xrefTvDB.AnimeID;
                        xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
                        xrefNew.TvDBID = xrefTvDB.TvDBID;
                        xrefNew.TvDBSeasonNumber = xrefTvDB.TvDBSeasonNumber;

                        TvDB_Series ser = xrefTvDB.GetTvDBSeries(sessionWrapper);
                        if (ser != null)
                            xrefNew.TvDBTitle = ser.SeriesName;

                        // determine start ep type
                        if (xrefTvDB.TvDBSeasonNumber == 0)
                            xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Special;
                        else
                            xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Episode;

                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TvDBStartEpisodeNumber = 1;

                        RepoFactory.CrossRef_AniDB_TvDBV2.Save(xrefNew);
                    }

                    // create cross ref's for specials
                    foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
                    {
                        AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xrefTvDB.AnimeID);
                        if (anime == null) continue;

                        // this anime has specials
                        if (anime.EpisodeCountSpecial <= 0) continue;

                        // this tvdb series has a season 0 (specials)
                        List<int> seasons = RepoFactory.TvDB_Episode.GetSeasonNumbersForSeries(xrefTvDB.TvDBID);
                        if (!seasons.Contains(0)) continue;

                        //make sure we are not doubling up
                        CrossRef_AniDB_TvDBV2 temp = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(xrefTvDB.TvDBID, 0, 1,
                            xrefTvDB.AnimeID,
                            (int) enEpisodeType.Special, 1);
                        if (temp != null) continue;

                        CrossRef_AniDB_TvDBV2 xrefNew = new CrossRef_AniDB_TvDBV2();
                        xrefNew.AnimeID = xrefTvDB.AnimeID;
                        xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
                        xrefNew.TvDBID = xrefTvDB.TvDBID;
                        xrefNew.TvDBSeasonNumber = 0;
                        xrefNew.TvDBStartEpisodeNumber = 1;
                        xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Special;
                        xrefNew.AniDBStartEpisodeNumber = 1;

                        TvDB_Series ser = xrefTvDB.GetTvDBSeries(sessionWrapper);
                        if (ser != null)
                            xrefNew.TvDBTitle = ser.SeriesName;

                        RepoFactory.CrossRef_AniDB_TvDBV2.Save(xrefNew);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Could not MigrateTvDBLinks_V1_to_V2: " + ex.ToString());
            }
        }

        public static void FixDuplicateTraktLinks()
        {

            // delete all Trakt link duplicates

            List<CrossRef_AniDB_Trakt> xrefsTraktProcessed = new List<CrossRef_AniDB_Trakt>();
            List<CrossRef_AniDB_Trakt> xrefsTraktToBeDeleted = new List<CrossRef_AniDB_Trakt>();

            List<CrossRef_AniDB_Trakt> xrefsTrakt = RepoFactory.CrossRef_AniDB_Trakt.GetAll();
            foreach (CrossRef_AniDB_Trakt xrefTrakt in xrefsTrakt)
            {
                bool deleteXref = false;
                foreach (CrossRef_AniDB_Trakt xref in xrefsTraktProcessed)
                {
                    if (xref.TraktID == xrefTrakt.TraktID && xref.TraktSeasonNumber == xrefTrakt.TraktSeasonNumber)
                    {
                        xrefsTraktToBeDeleted.Add(xrefTrakt);
                        deleteXref = true;
                    }
                }
                if (!deleteXref)
                    xrefsTraktProcessed.Add(xrefTrakt);
            }


            foreach (CrossRef_AniDB_Trakt xref in xrefsTraktToBeDeleted)
            {
                string msg = "";
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime != null) msg = anime.MainTitle;

                logger.Warn("Deleting Trakt Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg,
                    xref.TraktID,
                    xref.TraktSeasonNumber);
                RepoFactory.CrossRef_AniDB_Trakt.Delete(xref.CrossRef_AniDB_TraktID);
            }
        }

        public static void FixDuplicateTvDBLinks()
        {

            // delete all TvDB link duplicates


            List<CrossRef_AniDB_TvDB> xrefsTvDBProcessed = new List<CrossRef_AniDB_TvDB>();
            List<CrossRef_AniDB_TvDB> xrefsTvDBToBeDeleted = new List<CrossRef_AniDB_TvDB>();

            List<CrossRef_AniDB_TvDB> xrefsTvDB = RepoFactory.CrossRef_AniDB_TvDB.GetAll();
            foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
            {
                bool deleteXref = false;
                foreach (CrossRef_AniDB_TvDB xref in xrefsTvDBProcessed)
                {
                    if (xref.TvDBID == xrefTvDB.TvDBID && xref.TvDBSeasonNumber == xrefTvDB.TvDBSeasonNumber)
                    {
                        xrefsTvDBToBeDeleted.Add(xrefTvDB);
                        deleteXref = true;
                    }
                }
                if (!deleteXref)
                    xrefsTvDBProcessed.Add(xrefTvDB);
            }


            foreach (CrossRef_AniDB_TvDB xref in xrefsTvDBToBeDeleted)
            {
                string msg = "";
                AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime != null) msg = anime.MainTitle;

                logger.Warn("Deleting TvDB Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg,
                    xref.TvDBID,
                    xref.TvDBSeasonNumber);
                RepoFactory.CrossRef_AniDB_TvDB.Delete(xref.CrossRef_AniDB_TvDBID);
            }
        }

        public static void PopulateTagWeight()
        {
            try
            {
                foreach (AniDB_Anime_Tag atag in RepoFactory.AniDB_Anime_Tag.GetAll())
                {
                    atag.Weight = 0;
                    RepoFactory.AniDB_Anime_Tag.Save(atag);
                }
            }
            catch (Exception ex)
            {
                logger.Error( ex,"Could not PopulateTagWeight: " + ex.ToString());
            }
        }
    }
}