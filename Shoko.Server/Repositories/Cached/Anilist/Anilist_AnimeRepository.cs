using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Extensions;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.Anilist;

#nullable enable
namespace Shoko.Server.Repositories.Cached.Anilist;

public class Anilist_AnimeRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<Anilist_Anime, int>(databaseFactory)
{
    private PocoIndex<int, Anilist_Anime, int>? _anilistAnimeIDs;

    protected override int SelectKey(Anilist_Anime entity)
        => entity.Anilist_AnimeID;

    public override void PopulateIndexes()
    {
        _anilistAnimeIDs = Cache.CreateIndex(a => a.AnilistAnimeID);
    }

    public Anilist_Anime? GetByAnilistAnimeID(int anilistAnimeId)
        => _anilistAnimeIDs!.GetOne(anilistAnimeId);

    public IReadOnlyList<string> GetAllGenres()
    {
        var localAnimeIds = RepoFactory.AnimeSeries.GetAll()
            .SelectMany(s => s.AnilistAnimeCrossReferences)
            .Select(xref => xref.AnilistAnimeID)
            .ToHashSet();
        return Cache.GetAll()
            .Where(a => localAnimeIds.Contains(a.AnilistAnimeID))
            .SelectMany(a => a.Genres)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Except([""])
            .Order()
            .ToList();
    }

    public IReadOnlyList<string> GetAllTags()
    {
        var localAnimeIds = RepoFactory.AnimeSeries.GetAll()
            .SelectMany(s => s.AnilistAnimeCrossReferences)
            .Select(xref => xref.AnilistAnimeID)
            .ToHashSet();
        return Cache.GetAll()
            .Where(a => localAnimeIds.Contains(a.AnilistAnimeID))
            .SelectMany(a => a.Tags)
            .Select(tag => tag.Tag?.Name)
            .WhereNotNull()
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Except([""])
            .Order()
            .ToList();
    }
}
