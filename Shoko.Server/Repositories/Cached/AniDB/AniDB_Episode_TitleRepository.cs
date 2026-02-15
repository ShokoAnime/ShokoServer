using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Abstractions.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Models.AniDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.AniDB;

public class AniDB_Episode_TitleRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<AniDB_Episode_Title, int>(databaseFactory)
{
    private PocoIndex<int, AniDB_Episode_Title, int>? _episodeIDs;

    protected override int SelectKey(AniDB_Episode_Title entity)
        => entity.AniDB_Episode_TitleID;

    public override void PopulateIndexes()
    {
        _episodeIDs = Cache.CreateIndex(a => a.AniDB_EpisodeID);
    }

    public IReadOnlyList<AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int episodeID, TitleLanguage language)
        => GetByEpisodeID(episodeID).Where(a => a.Language == language).ToList();

    public IReadOnlyList<AniDB_Episode_Title> GetByEpisodeID(int episodeID)
        => ReadLock(() => _episodeIDs!.GetMultiple(episodeID));
}
