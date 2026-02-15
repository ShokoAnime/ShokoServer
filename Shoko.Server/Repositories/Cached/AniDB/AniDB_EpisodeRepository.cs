using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_EpisodeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Episode, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Episode, int>? _episodesIDs;

    private PocoIndex<int, AniDB_Episode, int>? _animeIDs;

    protected override int SelectKey(AniDB_Episode entity)
        => entity.AniDB_EpisodeID;

    public override void PopulateIndexes()
    {
        _episodesIDs = Cache.CreateIndex(a => a.EpisodeID);
        _animeIDs = Cache.CreateIndex(a => a.AnimeID);
    }

    public AniDB_Episode? GetByEpisodeID(int episodeID)
        => ReadLock(() => _episodesIDs!.GetOne(episodeID));

    public IReadOnlyList<AniDB_Episode> GetByAnimeID(int animeID)
        => ReadLock(() => _animeIDs!.GetMultiple(animeID));

    public IReadOnlyList<AniDB_Episode> GetForDate(DateTime startDate, DateTime endDate)
        => ReadLock(() => Cache.Values.Where(a => a.GetAirDateAsDate() is { } date && date >= startDate && date <= endDate).ToList());

    public IReadOnlyList<AniDB_Episode> GetByAnimeIDAndEpisodeNumber(int animeID, int episodeNumber)
        => GetByAnimeID(animeID)
            .Where(a => a.EpisodeNumber == episodeNumber && a.EpisodeType is EpisodeType.Episode)
            .ToList();

    public IReadOnlyList<AniDB_Episode> GetByAnimeIDAndEpisodeTypeNumber(int animeID, EpisodeType episodeType, int episodeNumber)
        => GetByAnimeID(animeID)
            .Where(a => a.EpisodeNumber == episodeNumber && a.EpisodeType == episodeType)
            .ToList();
}
