using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Extensions;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_Languages_AniDB_FileRepository : BaseCachedRepository<CrossRef_Languages_AniDB_File, int>
{
    private PocoIndex<int, CrossRef_Languages_AniDB_File, int> _fileIDs;

    public List<CrossRef_Languages_AniDB_File> GetByFileID(int id)
    {
        return ReadLock(() => _fileIDs.GetMultiple(id));
    }

    public HashSet<string> GetLanguagesForGroup(SVR_AnimeGroup group)
    {
        return ReadLock(() =>
        {
            return group.AllSeries.Select(a => a.AniDB_Anime).WhereNotNull().SelectMany(a => RepoFactory.CrossRef_File_Episode.GetByAnimeID(a.AnimeID))
                .Select(a => RepoFactory.AniDB_File.GetByHash(a.Hash)).WhereNotNull().SelectMany(a => GetByFileID(a.FileID)).Select(a => a.LanguageName)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);
        });
    }

    public HashSet<string> GetLanguagesForAnime(int animeID)
    {
        return ReadLock(() =>
        {
            return RepoFactory.CrossRef_File_Episode.GetByAnimeID(animeID)?.Select(a => RepoFactory.AniDB_File.GetByHash(a.Hash)).WhereNotNull()
                .SelectMany(a => GetByFileID(a.FileID)).Select(a => a.LanguageName).ToHashSet(StringComparer.InvariantCultureIgnoreCase) ?? [];
        });
    }

    public override void PopulateIndexes()
    {
        _fileIDs = Cache.CreateIndex(a => a.FileID);
    }

    public override void RegenerateDb() { }

    protected override int SelectKey(CrossRef_Languages_AniDB_File entity) => entity.CrossRef_Languages_AniDB_FileID;

    public CrossRef_Languages_AniDB_FileRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
