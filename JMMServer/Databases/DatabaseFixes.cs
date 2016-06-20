using System;
using System.Collections.Generic;
using AniDBAPI;
using JMMServer.Entities;
using JMMServer.Repositories;
using NLog;

namespace JMMServer.Databases
{
    public class DatabaseFixes
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static List<Action> Fixes = new List<Action>();

        public static void ExecuteDatabaseFixes()
        {
            foreach (Action a in Fixes)
            {
                a();
            }
        }

        public static void InitFixes()
        {
            Fixes = new List<Action>();
        }

        public static void FixHashes()
        {
            try
            {
                VideoLocalRepository repVids = new VideoLocalRepository();

                foreach (VideoLocal vid in repVids.GetAll())
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
                        repVids.Save(vid, false);
                        logger.Info("Fixed hashes on file: {0}", vid.FullServerPath);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException(ex.ToString(), ex);
            }
        }

        public static void RemoveOldMovieDBImageRecords()
        {
            try
            {
                MovieDB_FanartRepository repFanart = new MovieDB_FanartRepository();
                foreach (MovieDB_Fanart fanart in repFanart.GetAll())
                {
                    repFanart.Delete(fanart.MovieDB_FanartID);
                }

                MovieDB_PosterRepository repPoster = new MovieDB_PosterRepository();
                foreach (MovieDB_Poster poster in repPoster.GetAll())
                {
                    repPoster.Delete(poster.MovieDB_PosterID);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not RemoveOldMovieDBImageRecords: " + ex.ToString(), ex);
            }
        }

        public static void FixContinueWatchingGroupFilter_20160406()
        {
            // group filters
            GroupFilterRepository repFilters = new GroupFilterRepository();

            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // check if it already exists
                List<GroupFilter> lockedGFs = repFilters.GetLockedGroupFilters(session);

                if (lockedGFs != null)
                {
                    // if it already exists we can leave
                    foreach (GroupFilter gf in lockedGFs)
                    {
                        if (gf.GroupFilterName.Equals(Constants.GroupFilterName.ContinueWatching,
                            StringComparison.InvariantCultureIgnoreCase))
                        {
                            gf.FilterType = (int) GroupFilterType.ContinueWatching;
                            repFilters.Save(gf);
                        }
                    }
                }
            }
        }

        public static void MigrateTraktLinks_V1_to_V2()
        {
            try
            {
                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                Trakt_EpisodeRepository repEps = new Trakt_EpisodeRepository();
                Trakt_ShowRepository repShows = new Trakt_ShowRepository();

                CrossRef_AniDB_TraktRepository repCrossRefTrakt = new CrossRef_AniDB_TraktRepository();
                CrossRef_AniDB_TraktV2Repository repCrossRefTraktNew = new CrossRef_AniDB_TraktV2Repository();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    List<CrossRef_AniDB_Trakt> xrefsTrakt = repCrossRefTrakt.GetAll();
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

                        repCrossRefTraktNew.Save(xrefNew);
                    }

                    // create cross ref's for specials
                    foreach (CrossRef_AniDB_Trakt xrefTrakt in xrefsTrakt)
                    {
                        AniDB_Anime anime = repAnime.GetByAnimeID(xrefTrakt.AnimeID);
                        if (anime == null) continue;

                        Trakt_Show show = xrefTrakt.GetByTraktShow(session);
                        if (show == null) continue;

                        // this anime has specials
                        if (anime.EpisodeCountSpecial <= 0) continue;

                        // this Trakt series has a season 0 (specials)
                        List<int> seasons = repEps.GetSeasonNumbersForSeries(show.Trakt_ShowID);
                        if (!seasons.Contains(0)) continue;

                        //make sure we are not doubling up
                        CrossRef_AniDB_TraktV2 temp = repCrossRefTraktNew.GetByTraktID(xrefTrakt.TraktID, 0, 1,
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

                        repCrossRefTraktNew.Save(xrefNew);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not MigrateTraktLinks_V1_to_V2: " + ex.ToString(), ex);
            }
        }

        public static void MigrateTvDBLinks_V1_to_V2()
        {
            try
            {
                AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();
                TvDB_EpisodeRepository repEps = new TvDB_EpisodeRepository();

                CrossRef_AniDB_TvDBRepository repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();
                CrossRef_AniDB_TvDBV2Repository repCrossRefTvDBNew = new CrossRef_AniDB_TvDBV2Repository();

                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    List<CrossRef_AniDB_TvDB> xrefsTvDB = repCrossRefTvDB.GetAll();
                    foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
                    {
                        CrossRef_AniDB_TvDBV2 xrefNew = new CrossRef_AniDB_TvDBV2();
                        xrefNew.AnimeID = xrefTvDB.AnimeID;
                        xrefNew.CrossRefSource = xrefTvDB.CrossRefSource;
                        xrefNew.TvDBID = xrefTvDB.TvDBID;
                        xrefNew.TvDBSeasonNumber = xrefTvDB.TvDBSeasonNumber;

                        TvDB_Series ser = xrefTvDB.GetTvDBSeries(session);
                        if (ser != null)
                            xrefNew.TvDBTitle = ser.SeriesName;

                        // determine start ep type
                        if (xrefTvDB.TvDBSeasonNumber == 0)
                            xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Special;
                        else
                            xrefNew.AniDBStartEpisodeType = (int) enEpisodeType.Episode;

                        xrefNew.AniDBStartEpisodeNumber = 1;
                        xrefNew.TvDBStartEpisodeNumber = 1;

                        repCrossRefTvDBNew.Save(xrefNew);
                    }

                    // create cross ref's for specials
                    foreach (CrossRef_AniDB_TvDB xrefTvDB in xrefsTvDB)
                    {
                        AniDB_Anime anime = repAnime.GetByAnimeID(xrefTvDB.AnimeID);
                        if (anime == null) continue;

                        // this anime has specials
                        if (anime.EpisodeCountSpecial <= 0) continue;

                        // this tvdb series has a season 0 (specials)
                        List<int> seasons = repEps.GetSeasonNumbersForSeries(xrefTvDB.TvDBID);
                        if (!seasons.Contains(0)) continue;

                        //make sure we are not doubling up
                        CrossRef_AniDB_TvDBV2 temp = repCrossRefTvDBNew.GetByTvDBID(xrefTvDB.TvDBID, 0, 1,
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

                        TvDB_Series ser = xrefTvDB.GetTvDBSeries(session);
                        if (ser != null)
                            xrefNew.TvDBTitle = ser.SeriesName;

                        repCrossRefTvDBNew.Save(xrefNew);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not MigrateTvDBLinks_V1_to_V2: " + ex.ToString(), ex);
            }
        }

        public static void FixDuplicateTraktLinks()
        {
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

            // delete all Trakt link duplicates
            CrossRef_AniDB_TraktRepository repCrossRefTrakt = new CrossRef_AniDB_TraktRepository();

            List<CrossRef_AniDB_Trakt> xrefsTraktProcessed = new List<CrossRef_AniDB_Trakt>();
            List<CrossRef_AniDB_Trakt> xrefsTraktToBeDeleted = new List<CrossRef_AniDB_Trakt>();

            List<CrossRef_AniDB_Trakt> xrefsTrakt = repCrossRefTrakt.GetAll();
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
                AniDB_Anime anime = repAnime.GetByAnimeID(xref.AnimeID);
                if (anime != null) msg = anime.MainTitle;

                logger.Warn("Deleting Trakt Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg,
                    xref.TraktID,
                    xref.TraktSeasonNumber);
                repCrossRefTrakt.Delete(xref.CrossRef_AniDB_TraktID);
            }
        }

        public static void FixDuplicateTvDBLinks()
        {
            AniDB_AnimeRepository repAnime = new AniDB_AnimeRepository();

            // delete all TvDB link duplicates
            CrossRef_AniDB_TvDBRepository repCrossRefTvDB = new CrossRef_AniDB_TvDBRepository();

            List<CrossRef_AniDB_TvDB> xrefsTvDBProcessed = new List<CrossRef_AniDB_TvDB>();
            List<CrossRef_AniDB_TvDB> xrefsTvDBToBeDeleted = new List<CrossRef_AniDB_TvDB>();

            List<CrossRef_AniDB_TvDB> xrefsTvDB = repCrossRefTvDB.GetAll();
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
                AniDB_Anime anime = repAnime.GetByAnimeID(xref.AnimeID);
                if (anime != null) msg = anime.MainTitle;

                logger.Warn("Deleting TvDB Link because of a duplicate: {0} ({1}) - {2}/{3}", xref.AnimeID, msg,
                    xref.TvDBID,
                    xref.TvDBSeasonNumber);
                repCrossRefTvDB.Delete(xref.CrossRef_AniDB_TvDBID);
            }
        }

        public static void PopulateTagWeight()
        {
            try
            {
                AniDB_Anime_TagRepository repTags = new AniDB_Anime_TagRepository();
                foreach (AniDB_Anime_Tag atag in repTags.GetAll())
                {
                    atag.Weight = 0;
                    repTags.Save(atag);
                }
            }
            catch (Exception ex)
            {
                logger.ErrorException("Could not PopulateTagWeight: " + ex.ToString(), ex);
            }
        }
    }
}