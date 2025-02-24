#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Extensions;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<SVR_AniDB_Episode, int>(databaseFactory)
{
    private PocoIndex<int, SVR_AniDB_Episode, int>? _episodesIDs;

    private PocoIndex<int, SVR_AniDB_Episode, int>? _animeIDs;

    protected override int SelectKey(SVR_AniDB_Episode entity)
        => entity.AniDB_EpisodeID;

    public override void PopulateIndexes()
    {
        _episodesIDs = new PocoIndex<int, SVR_AniDB_Episode, int>(Cache, a => a.EpisodeID);
        _animeIDs = new PocoIndex<int, SVR_AniDB_Episode, int>(Cache, a => a.AnimeID);
    }

    public SVR_AniDB_Episode? GetByEpisodeID(int episodeID)
        => ReadLock(() => _episodesIDs!.GetOne(episodeID));

    public IReadOnlyList<SVR_AniDB_Episode> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public IReadOnlyList<SVR_AniDB_Episode> GetForDate(DateTime startDate, DateTime endDate)
        => ReadLock(() => Cache.Values.Where(a => a.GetAirDateAsDate() is { } date && date >= startDate && date <= endDate).ToList());

    public IReadOnlyList<SVR_AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeID, int episodeNumber)
        => GetByAnimeID(animeID)
            .Where(a => a.EpisodeNumber == episodeNumber && a.EpisodeTypeEnum == EpisodeType.Episode)
            .ToList();

    public IReadOnlyList<SVR_AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeID, EpisodeType episodeType, int episodeNumber)
        => GetByAnimeID(animeID)
            .Where(a => a.EpisodeNumber == episodeNumber && a.EpisodeTypeEnum == episodeType)
            .ToList();
}
