using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class AnimeSeriesRepository : BaseCachedRepository<SVR_AnimeSeries, int>
{
    private static Logger logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, SVR_AnimeSeries, int> AniDBIds;
    private PocoIndex<int, SVR_AnimeSeries, int> Groups;

    private ChangeTracker<int> Changes = new();

    public AnimeSeriesRepository()
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetBySeriesID(cr.AnimeSeriesID));
            Changes.Remove(cr.AnimeSeriesID);
        };
        EndDeleteCallback = cr =>
        {
            cr.DeleteFromFilters();
            if (cr.AnimeGroupID <= 0)
            {
                return;
            }

            logger.Trace("Updating group stats by group from AnimeSeriesRepository.Delete: {0}",
                cr.AnimeGroupID);
            var oldGroup = RepoFactory.AnimeGroup.GetByID(cr.AnimeGroupID);
            if (oldGroup != null)
            {
                RepoFactory.AnimeGroup.Save(oldGroup, true, true);
            }
        };
    }

    protected override int SelectKey(SVR_AnimeSeries entity)
    {
        return entity.AnimeSeriesID;
    }

    public override void PopulateIndexes()
    {
        Changes.AddOrUpdateRange(Cache.Keys);
        AniDBIds = Cache.CreateIndex(a => a.AniDB_ID);
        Groups = Cache.CreateIndex(a => a.AnimeGroupID);
    }

    public override void RegenerateDb()
    {
        try
        {
            var sers =
                Cache.Values.Where(
                        a => a.ContractVersion < SVR_AnimeSeries.CONTRACT_VERSION ||
                             a.Contract?.AniDBAnime?.AniDBAnime == null)
                    .ToList();
            var max = sers.Count;
            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, typeof(AnimeSeries).Name, " DbRegen");
            if (max <= 0)
            {
                return;
            }

            for (var i = 0; i < sers.Count; i++)
            {
                var s = sers[i];
                try
                {
                    Save(s, false, false, true);
                }
                catch
                {
                }

                if (i % 10 == 0)
                {
                    ServerState.Instance.ServerStartingStatus = string.Format(
                        Resources.Database_Validating, typeof(AnimeSeries).Name,
                        " DbRegen - " + i + "/" + max
                    );
                }
            }

            ServerState.Instance.ServerStartingStatus = string.Format(
                Resources.Database_Validating, typeof(AnimeSeries).Name,
                " DbRegen - " + max + "/" + max);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    public ChangeTracker<int> GetChangeTracker()
    {
        return Changes;
    }

    public override void Save(SVR_AnimeSeries obj)
    {
        Save(obj, false);
    }

    public void Save(SVR_AnimeSeries obj, bool onlyupdatestats)
    {
        Save(obj, true, onlyupdatestats);
    }

    public void Save(SVR_AnimeSeries obj, bool updateGroups, bool onlyupdatestats, bool skipgroupfilters = false,
        bool alsoupdateepisodes = false)
    {
        var animeID = obj.GetAnime()?.MainTitle ?? obj.AniDB_ID.ToString();
        logger.Trace("Saving Series {ID}", animeID);
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        var newSeries = false;
        SVR_AnimeGroup oldGroup = null;
        // Updated Now
        obj.DateTimeUpdated = DateTime.Now;
        var isMigrating = false;
        if (obj.AnimeSeriesID == 0)
        {
            newSeries = true; // a new series
        }
        else
        {
            // get the old version from the DB
            SVR_AnimeSeries oldSeries;
            logger.Trace("Saving Series {ID} | Waiting for Database Lock", animeID);
            lock (GlobalDBLock)
            {
                sw.Stop();
                logger.Trace("Saving Series {ID} | Got Database Lock in {Time:ss.fff}", animeID, sw.Elapsed);
                sw.Restart();
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                oldSeries = session.Get<SVR_AnimeSeries>(obj.AnimeSeriesID);
                sw.Stop();
                logger.Trace("Saving Series {ID} | Got Series from Database in {Time:ss.fff}", animeID, sw.Elapsed);
                sw.Restart();
            }

            if (oldSeries != null)
            {
                // means we are moving series to a different group
                if (oldSeries.AnimeGroupID != obj.AnimeGroupID)
                {
                    logger.Trace("Saving Series {ID} | Group ID is different. Moving to new group", animeID);
                    oldGroup = RepoFactory.AnimeGroup.GetByID(oldSeries.AnimeGroupID);
                    var newGroup = RepoFactory.AnimeGroup.GetByID(obj.AnimeGroupID);
                    if (newGroup is { GroupName: "AAA Migrating Groups AAA" })
                    {
                        isMigrating = true;
                    }

                    newSeries = true;
                }
            }
            else
            {
                // should not happen, but if it does, recover
                newSeries = true;
                logger.Trace("Saving Series {ID} | Unable to get series from database, attempting to make new record", animeID);
            }
        }

        if (newSeries && !isMigrating)
        {
            sw.Stop();
            logger.Trace("Saving Series {ID} | New Series added. Need to save first to get an ID", animeID);
            sw.Restart();
            obj.Contract = null;
            base.Save(obj);
            sw.Stop();
            logger.Trace("Saving Series {ID} | Saved new series in {Time:ss.fff}", animeID, sw.Elapsed);
            sw.Restart();
        }

        var seasons = obj.GetAnime()?.Contract?.Stat_AllSeasons;
        if (seasons == null || seasons.Count == 0)
        {
            sw.Stop();
            logger.Trace("Saving Series {ID} | AniDB_Anime Contract is invalid or Seasons not generated. Regenerating", animeID);
            sw.Restart();
            var anime = obj.GetAnime();
            if (anime != null)
            {
                RepoFactory.AniDB_Anime.Save(anime, true);
            }

            sw.Stop();
            logger.Trace("Saving Series {ID} | Regenerated AniDB_Anime Contract in {Time:ss.fff}", animeID, sw.Elapsed);
            sw.Restart();
        }

        sw.Stop();
        logger.Trace("Saving Series {ID} | Updating Series Contract", animeID);
        sw.Restart();
        var types = obj.UpdateContract(onlyupdatestats);
        sw.Stop();
        logger.Trace("Saving Series {ID} | Updated Series Contract in {Time:ss.fff}", animeID, sw.Elapsed);
        sw.Restart();
        logger.Trace("Saving Series {ID} | Saving Series to Database", animeID);
        base.Save(obj);
        sw.Stop();
        logger.Trace("Saving Series {ID} | Saved Series to Database in {Time:ss.fff}", animeID, sw.Elapsed);
        sw.Restart();

        if (updateGroups && !isMigrating)
        {
            logger.Trace("Saving Series {ID} | Also Updating Group {GroupID}", animeID, obj.AnimeGroupID);
            var grp = RepoFactory.AnimeGroup.GetByID(obj.AnimeGroupID);
            if (grp != null)
            {
                RepoFactory.AnimeGroup.Save(grp, true, true);
            }
            else
                logger.Trace("Saving Series {ID} | Group {GroupID} was not found. Not Updating", animeID,
                    obj.AnimeGroupID);
            
            sw.Stop();
            logger.Trace("Saving Series {ID} | Updated Group {GroupID} in {Time:ss.fff}", animeID, obj.AnimeGroupID,
                sw.Elapsed);
            sw.Restart();

            // Last ditch to make sure we aren't just updating the same group twice (shouldn't be)
            if (oldGroup != null && grp?.AnimeGroupID != oldGroup.AnimeGroupID)
            {
                logger.Trace("Saving Series {ID} | Also Updating previous group {GroupID}", animeID,
                    oldGroup.AnimeGroupID);
                RepoFactory.AnimeGroup.Save(oldGroup, true, true);
                sw.Stop();
                logger.Trace("Saving Series {ID} | Updated old group {GroupID} in {Time:ss.fff}", animeID,
                    oldGroup.AnimeGroupID, sw.Elapsed);
                sw.Restart();
            }
        }

        if (!skipgroupfilters && !isMigrating)
        {
            sw.Stop();
            logger.Trace("Saving Series {ID} | Updating Group Filters", animeID);
            sw.Restart();
            var endyear = obj.Contract?.AniDBAnime?.AniDBAnime?.EndYear ?? 0;
            if (endyear == 0)
            {
                endyear = DateTime.Today.Year;
            }

            var startyear = obj.Contract?.AniDBAnime?.AniDBAnime?.BeginYear ?? 0;
            if (endyear < startyear)
            {
                endyear = startyear;
            }

            HashSet<int> allyears = null;
            if (startyear != 0)
            {
                allyears = startyear == endyear
                    ? new HashSet<int> { startyear }
                    : new HashSet<int>(Enumerable.Range(startyear, endyear - startyear + 1));
            }

            // Reinit this in case it was updated in the contract
            seasons = obj.Contract?.AniDBAnime?.Stat_AllSeasons;
            //This call will create extra years or tags if the Group have a new year or tag
            logger.Trace("Saving Series {ID} | Updating Group Filters for Years ({Years}) and Seasons ({Seasons})",
                animeID, string.Join(",", allyears.OrderBy(a => a)), string.Join(",", seasons));
            RepoFactory.GroupFilter.CreateOrVerifyDirectoryFilters(false,
                obj.Contract?.AniDBAnime?.AniDBAnime?.GetAllTags(), allyears, seasons);

            // Update other existing filters
            obj.UpdateGroupFilters(types);
            sw.Stop();
            logger.Trace("Saving Series {ID} | Updated Group Filters in {Time:ss.fff}", animeID, sw.Elapsed);
            sw.Restart();
        }

        Changes.AddOrUpdate(obj.AnimeSeriesID);

        if (alsoupdateepisodes)
        {
            sw.Stop();
            logger.Trace("Saving Series {ID} | Updating Episodes", animeID);
            sw.Restart();
            var eps = RepoFactory.AnimeEpisode.GetBySeriesID(obj.AnimeSeriesID);
            RepoFactory.AnimeEpisode.Save(eps);
            sw.Stop();
            logger.Trace("Saving Series {ID} | Updated Episodes in {Time:ss.fff}", animeID, sw.Elapsed);
            sw.Restart();
        }
        sw.Stop();
        totalSw.Stop();
        logger.Trace("Saving Series {ID} | Finished Saving in {Time:ss.fff}", animeID, totalSw.Elapsed);
    }

    public void UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeSeries> seriesBatch)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (seriesBatch == null)
        {
            throw new ArgumentNullException(nameof(seriesBatch));
        }

        if (seriesBatch.Count == 0)
        {
            return;
        }

        foreach (var series in seriesBatch)
        {
            session.Update(series);
            UpdateCache(series);
            Changes.AddOrUpdate(series.AnimeSeriesID);
        }
    }

    public SVR_AnimeSeries GetByAnimeID(int id)
    {
        return ReadLock(() => AniDBIds.GetOne(id));
    }

    public List<SVR_AnimeSeries> GetByGroupID(int groupid)
    {
        return ReadLock(() => Groups.GetMultiple(groupid));
    }

    public List<SVR_AnimeSeries> GetWithMissingEpisodes()
    {
        return ReadLock(() => Cache.Values.Where(a => a.MissingEpisodeCountGroups > 0)
            .OrderByDescending(a => a.EpisodeAddedDate)
            .ToList());
    }

    public List<SVR_AnimeSeries> GetMostRecentlyAdded(int maxResults, int userID)
    {
        var user = RepoFactory.JMMUser.GetByID(userID);
        return ReadLock(() => user == null
            ? Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList()
            : Cache.Values.Where(a => user.AllowedSeries(a)).OrderByDescending(a => a.DateTimeCreated).Take(maxResults)
                .ToList());
    }
}
