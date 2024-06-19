using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_EpisodeRepository : BaseCachedRepository<SVR_AniDB_Episode, int>
{
    private PocoIndex<int, SVR_AniDB_Episode, int> EpisodesIds;
    private PocoIndex<int, SVR_AniDB_Episode, int> Animes;

    public override void PopulateIndexes()
    {
        EpisodesIds = new PocoIndex<int, SVR_AniDB_Episode, int>(Cache, a => a.EpisodeID);
        Animes = new PocoIndex<int, SVR_AniDB_Episode, int>(Cache, a => a.AnimeID);
    }

    protected override int SelectKey(SVR_AniDB_Episode entity)
    {
        return entity.AniDB_EpisodeID;
    }

    public override void RegenerateDb()
    {
    }

    public SVR_AniDB_Episode GetByEpisodeID(int id)
    {
        return ReadLock(() => EpisodesIds.GetOne(id));
    }

    public List<SVR_AniDB_Episode> GetByAnimeID(int id)
    {
        return ReadLock(() => Animes.GetMultiple(id));
    }

    public List<SVR_AniDB_Episode> GetForDate(DateTime startDate, DateTime endDate)
    {
        return ReadLock(() => Cache.Values.Where(a =>
        {
            var date = a.GetAirDateAsDate();
            return date.HasValue && date.Value >= startDate && date.Value <= endDate;
        }).ToList());
    }

    public List<SVR_AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeid, int epnumber)
    {
        return GetByAnimeID(animeid)
            .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == EpisodeType.Episode)
            .ToList();
    }

    public List<SVR_AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeid, EpisodeType epType, int epnumber)
    {
        return GetByAnimeID(animeid)
            .Where(a => a.EpisodeNumber == epnumber && a.GetEpisodeTypeEnum() == epType)
            .ToList();
    }

    public AniDB_EpisodeRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
