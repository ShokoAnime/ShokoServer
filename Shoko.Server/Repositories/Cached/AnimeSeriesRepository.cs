using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NHibernate;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Commons.Properties;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;
using Shoko.Server.Tasks;
using Shoko.Server.Utilities;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class AnimeSeriesRepository : BaseCachedRepository<SVR_AnimeSeries, int>
{
    private static readonly Logger logger = LogManager.GetCurrentClassLogger();

    private PocoIndex<int, SVR_AnimeSeries, int>? AniDBIds;
    private PocoIndex<int, SVR_AnimeSeries, int>? Groups;

    private readonly ChangeTracker<int> Changes = new();

    public AnimeSeriesRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
        BeginDeleteCallback = cr =>
        {
            RepoFactory.AnimeSeries_User.Delete(RepoFactory.AnimeSeries_User.GetBySeriesID(cr.AnimeSeriesID));
            Changes.Remove(cr.AnimeSeriesID);
        };
        EndDeleteCallback = cr =>
        {
            if (cr.AnimeGroupID <= 0)
            {
                return;
            }

            logger.Trace("Updating group stats by group from AnimeSeriesRepository.Delete: {0}",
                cr.AnimeGroupID);
            var oldGroup = RepoFactory.AnimeGroup.GetByID(cr.AnimeGroupID);
            if (oldGroup != null)
            {
                RepoFactory.AnimeGroup.Save(oldGroup, true);
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
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(AnimeSeries), " Database Regeneration - Caching Titles & Overview");
            foreach (var series in Cache.Values.ToList())
            {
                series.ResetPreferredTitle();
                series.ResetPreferredOverview();
                series.ResetAnimeTitles();
            }

            var sers = Cache.Values.Where(a => a.AnimeGroupID == 0 || RepoFactory.AnimeGroup.GetByID(a.AnimeGroupID) == null).ToList();
            var max = sers.Count;
            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(AnimeSeries), " Database Regeneration - Ensuring Groups Exist");

            var groupCreator = Utils.ServiceContainer.GetRequiredService<AnimeGroupCreator>();
            for (var i = 0; i < max; i++)
            {
                var s = sers[i];
                try
                {
                    var group = groupCreator.GetOrCreateSingleGroupForSeries(s);
                    s.AnimeGroupID = group.AnimeGroupID;
                    Save(s, false, true);
                }
                catch
                {
                    // ignore
                }

                if (i % 10 != 0) continue;
                ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(AnimeSeries),
                    " DbRegen - Ensuring Groups Exist - " + i + "/" + max);
            }

            ServerState.Instance.ServerStartingStatus = string.Format(Resources.Database_Validating, nameof(AnimeSeries),
                " DbRegen - Ensuring Groups Exist - " + max + "/" + max);
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

    public void Save(SVR_AnimeSeries obj, bool updateGroups, bool onlyupdatestats, bool alsoupdateepisodes = false)
    {
        var animeID = obj.AniDB_Anime?.MainTitle ?? obj.AniDB_ID.ToString();
        logger.Trace($"Saving Series {animeID}");
        var totalSw = Stopwatch.StartNew();
        var sw = Stopwatch.StartNew();
        var newSeries = false;
        SVR_AnimeGroup? oldGroup = null;
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
            logger.Trace($"Saving Series {animeID} | Waiting for Database Lock");
            var oldSeries = Lock(obj.AnimeSeriesID, animeID, sw, (animeSeriesID, id, s) =>
            {
                s.Stop();
                logger.Trace($"Saving Series {id} | Got Database Lock in {s.Elapsed.TotalSeconds:0.00###}s");
                s.Restart();
                using var session = _databaseFactory.SessionFactory.OpenSession();
                var series = session.Get<SVR_AnimeSeries>(animeSeriesID);
                s.Stop();
                logger.Trace($"Saving Series {id} | Got Series from Database in {s.Elapsed.TotalSeconds:0.00###}s");
                s.Restart();
                return series;
            });

            if (oldSeries != null)
            {
                // means we are moving series to a different group
                if (oldSeries.AnimeGroupID != obj.AnimeGroupID)
                {
                    logger.Trace($"Saving Series {animeID} | Group ID is different. Moving to new group");
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
                logger.Trace(
                    $"Saving Series {animeID} | Unable to get series from database, attempting to make new record");
            }
        }

        if (newSeries && !isMigrating)
        {
            sw.Stop();
            logger.Trace($"Saving Series {animeID} | New Series added. Need to save first to get an ID");
            sw.Restart();
            base.Save(obj);
            sw.Stop();
            logger.Trace($"Saving Series {animeID} | Saved new series in {sw.Elapsed.TotalSeconds:0.00###}s");
            sw.Restart();
        }

        var seasons = obj.AniDB_Anime?.Seasons;
        if (seasons == null || !seasons.Any()) RegenerateSeasons(obj, sw, animeID);

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Saving Series to Database");
        sw.Restart();
        base.Save(obj);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Saved Series to Database in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        if (updateGroups && !isMigrating) UpdateGroups(obj, animeID, sw, oldGroup!);

        Changes.AddOrUpdate(obj.AnimeSeriesID);

        if (alsoupdateepisodes) UpdateEpisodes(obj, sw, animeID);

        sw.Stop();
        totalSw.Stop();
        logger.Trace($"Saving Series {animeID} | Finished Saving in {totalSw.Elapsed.TotalSeconds:0.00###}s");
    }

    private static void RegenerateSeasons(SVR_AnimeSeries obj, Stopwatch sw, string animeID)
    {
        sw.Stop();
        logger.Trace(
            $"Saving Series {animeID} | AniDB_Anime Contract is invalid or Seasons not generated. Regenerating");
        sw.Restart();
        var anime = obj.AniDB_Anime;
        if (anime != null)
        {
            RepoFactory.AniDB_Anime.Save(anime, true);
        }

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Regenerated AniDB_Anime Contract in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateEpisodes(SVR_AnimeSeries obj, Stopwatch sw, string animeID)
    {
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updating Episodes");
        sw.Restart();
        var eps = RepoFactory.AnimeEpisode.GetBySeriesID(obj.AnimeSeriesID);
        RepoFactory.AnimeEpisode.Save(eps);
        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Episodes in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();
    }

    private static void UpdateGroups(SVR_AnimeSeries obj, string animeID, Stopwatch sw, SVR_AnimeGroup oldGroup)
    {
        logger.Trace($"Saving Series {animeID} | Also Updating Group {obj.AnimeGroupID}");
        var grp = RepoFactory.AnimeGroup.GetByID(obj.AnimeGroupID);
        if (grp != null)
        {
            RepoFactory.AnimeGroup.Save(grp, true);
        }
        else
            logger.Trace($"Saving Series {animeID} | Group {obj.AnimeGroupID} was not found. Not Updating");

        sw.Stop();
        logger.Trace($"Saving Series {animeID} | Updated Group {obj.AnimeGroupID} in {sw.Elapsed.TotalSeconds:0.00###}s");
        sw.Restart();

        // Last ditch to make sure we aren't just updating the same group twice (shouldn't be)
        if (oldGroup != null && grp?.AnimeGroupID != oldGroup.AnimeGroupID)
        {
            logger.Trace($"Saving Series {animeID} | Also Updating previous group {oldGroup.AnimeGroupID}");
            RepoFactory.AnimeGroup.Save(oldGroup, true);
            sw.Stop();
            logger.Trace(
                $"Saving Series {animeID} | Updated old group {oldGroup.AnimeGroupID} in {sw.Elapsed.TotalSeconds:0.00###}s");
            sw.Restart();
        }
    }

    public async Task UpdateBatch(ISessionWrapper session, IReadOnlyCollection<SVR_AnimeSeries> seriesBatch)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(seriesBatch);

        if (seriesBatch.Count == 0)
        {
            return;
        }

        foreach (var series in seriesBatch)
        {
            await Lock(async () => await session.UpdateAsync(series));
            UpdateCache(series);
            Changes.AddOrUpdate(series.AnimeSeriesID);
        }
    }

    public SVR_AnimeSeries? GetByAnimeID(int id)
    {
        return ReadLock(() => AniDBIds!.GetOne(id));
    }

    public List<SVR_AnimeSeries> GetByGroupID(int groupid)
    {
        return ReadLock(() => Groups!.GetMultiple(groupid));
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

    private const string IgnoreVariationsQuery =
        @"SELECT DISTINCT ani.AnimeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.IsVariation = 0 AND vl.Hash != '' GROUP BY ani.AnimeID, ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";
    private const string CountVariationsQuery =
        @"SELECT DISTINCT ani.AnimeID FROM VideoLocal AS vl JOIN CrossRef_File_Episode ani ON vl.Hash = ani.Hash WHERE vl.Hash != '' GROUP BY ani.AnimeID, ani.EpisodeID HAVING COUNT(ani.EpisodeID) > 1";

    public List<SVR_AnimeSeries> GetWithMultipleReleases(bool ignoreVariations)
    {
        var ids = Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();

            var query = ignoreVariations ? IgnoreVariationsQuery : CountVariationsQuery;
            return session.CreateSQLQuery(query)
                .AddScalar("AnimeID", NHibernateUtil.Int32)
                .List<int>();
        });

        return ids
            .Distinct()
            .Select(GetByAnimeID)
            .WhereNotNull()
            .ToList();
    }

    public IEnumerable<int> GetAllYears()
    {
        var anime = RepoFactory.AnimeSeries.GetAll().Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AniDB_ID)).Where(a => a?.AirDate != null).ToList();
        if (anime.Count == 0) yield break;
        var minDate = anime.Min(a => a.AirDate!.Value);
        var maxDate = anime.Max(o => o.EndDate ?? DateTime.Today);

        for (var year = minDate.Year; year <= maxDate.Year; year++)
        {
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd = new DateTime(year, 12, 31);

            if (anime.Any(o => o.AirDate <= yearEnd && (o.EndDate >= yearStart || o.EndDate == null)))
            {
                yield return year;
            }
        }
    }

    public SortedSet<(int Year, AnimeSeason Season)> GetAllSeasons()
    {
        var anime = GetAll().Select(a => RepoFactory.AniDB_Anime.GetByAnimeID(a.AniDB_ID)).Where(a => a?.AirDate != null).ToList();
        return GetAllSeasons(anime);
    }

    public static SortedSet<(int Year, AnimeSeason Season)> GetAllSeasons(IEnumerable<AniDB_Anime> anime)
    {
        var seasons = new SortedSet<(int Year, AnimeSeason Season)>();
        foreach (var current in anime)
        {
            var beginYear = current.AirDate!.Value.Year;
            var endYear = current.EndDate?.Year ?? DateTime.Today.Year;
            for (var year = beginYear; year <= endYear; year++)
            {
                if (beginYear < year && year < endYear)
                {
                    seasons.Add((year, AnimeSeason.Winter));
                    seasons.Add((year, AnimeSeason.Spring));
                    seasons.Add((year, AnimeSeason.Summer));
                    seasons.Add((year, AnimeSeason.Fall));
                    continue;
                }

                if (!seasons.Contains((year, AnimeSeason.Winter)) && current.IsInSeason(AnimeSeason.Winter, year)) seasons.Add((year, AnimeSeason.Winter));
                if (!seasons.Contains((year, AnimeSeason.Spring)) && current.IsInSeason(AnimeSeason.Spring, year)) seasons.Add((year, AnimeSeason.Spring));
                if (!seasons.Contains((year, AnimeSeason.Summer)) && current.IsInSeason(AnimeSeason.Summer, year)) seasons.Add((year, AnimeSeason.Summer));
                if (!seasons.Contains((year, AnimeSeason.Fall)) && current.IsInSeason(AnimeSeason.Fall, year)) seasons.Add((year, AnimeSeason.Fall));
            }
        }

        return seasons;
    }
}
